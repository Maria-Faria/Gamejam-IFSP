using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelike;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the monsters: ghost materials (body + smoke shell), smoke/mote particle systems, an animator
// controller per monster, attack components, effect prefabs (bolts, spit, laser, telegraph, puffs), one prefab
// per monster, the MonsterCatalog (Assets/Monsters/Resources) and the MonsterTest scene (the game's player,
// copied from SampleScene, among all the monsters in a lab room).
// Everything metric (sizes, reach, ranges, speeds) is scaled by MonsterScale.Factor, so the monsters keep
// their size classes relative to this game's player (the FBX import scale is set by MonsterImporter).
// Menu: Roguelike/Build Monsters   (batch: -executeMethod MonsterSetup.BuildAll)
public static class MonsterSetup
{
    const string GeneratedDir = "Assets/Monsters/Generated";
    public const string PrefabDir = "Assets/Prefabs/Monsters";
    public const string CatalogPath = "Assets/Monsters/Resources/" + MonsterCatalog.ResourceName + ".asset";
    const string ScenePath = "Assets/Scenes/MonsterTest.unity";
    const string GameScenePath = "Assets/Scenes/SampleScene.unity";
    const string LabDir = "Assets/Levels/L1_Lab";
    const float S = MonsterScale.Factor;

    // size class of each monster relative to the player, and whether it is a boss
    static readonly Dictionary<string, (float rel, MonsterCatalog.SizeClass cls, bool boss)> Classes =
        new Dictionary<string, (float, MonsterCatalog.SizeClass, bool)>
        {
            { "BallShadow", (0.5f, MonsterCatalog.SizeClass.Tiny, false) },
            { "TwistedBall", (0.5f, MonsterCatalog.SizeClass.Tiny, false) },
            { "Regular", (0.75f, MonsterCatalog.SizeClass.Small, false) },
            { "ThreeEyed", (0.75f, MonsterCatalog.SizeClass.Small, false) },
            { "Ghost", (0.75f, MonsterCatalog.SizeClass.Small, false) },
            { "Claw", (0.75f, MonsterCatalog.SizeClass.Small, false) },
            { "Crescent", (1f, MonsterCatalog.SizeClass.Medium, false) },
            { "Ghoul", (1f, MonsterCatalog.SizeClass.Medium, false) },
            { "Horned", (1f, MonsterCatalog.SizeClass.Medium, false) },
            { "Muscle", (1f, MonsterCatalog.SizeClass.Medium, false) },
            { "Fat", (1.7f, MonsterCatalog.SizeClass.Boss, true) },
            { "MuscleBoss", (1.8f, MonsterCatalog.SizeClass.Boss, true) },
            { "SkullFace", (2f, MonsterCatalog.SizeClass.Boss, true) },
        };

    // serialized fields in metres (or m/s, m/s^2) that MetricScale multiplies by S, per declaring type
    static readonly Dictionary<System.Type, string[]> MetricFields = new Dictionary<System.Type, string[]>
    {
        { typeof(Monster), new[] { "aggroRange", "moveSpeed" } },
        { typeof(MonsterAttack), new[] { "minRange", "maxRange" } },
        { typeof(BulletPatternAttack), new[] { "speed", "height", "wallSpacing", "wallDistance", "waveAmplitude" } },
        { typeof(ChargeAttack), new[] { "speed", "maxDistance", "overshoot", "wallShotSpeed", "wallShotHeight" } },
        { typeof(JumpSmashAttack), new[] { "maxLeap", "landingShotSpeed" } },
        { typeof(LaserAttack), new[] { "length", "beamRadius" } },
        { typeof(LungeAttack), new[] { "dashDistance" } },
        { typeof(MeleeAttack), new[] { "lunge" } },
        { typeof(ProjectileAttack), new[] { "speed", "gravity", "splash", "cruiseHeight" } },
        { typeof(RadialShotAttack), new[] { "speed", "height" } },
        { typeof(SpinAttack), new[] { "moveSpeed", "shotSpeed", "shotHeight" } },
        { typeof(SpitAttack), new[] { "lobGravity", "pelletSpeed", "cruiseHeight" } },
        { typeof(TeleportAttack), new[] { "minDistance", "maxDistance" } },
        { typeof(ChargerBrain), new[] { "keepAt" } },
        { typeof(DartBrain), new[] { "keepAt" } },
        { typeof(FatBoss), new[] { "keepAt", "crowdedAt" } },
        { typeof(MuscleBoss), new[] { "spinRange" } },
        { typeof(RangedBrain), new[] { "keepAt", "hop" } },
        { typeof(SkirmisherBrain), new[] { "keepAt" } },
        { typeof(SkullFaceBoss), new[] { "keepAt" } },
        { typeof(WanderBrain), new[] { "wanderRadius", "rushRange", "giveUpRange" } },
        { typeof(Projectile), new[] { "radius", "returnAfter", "burstSpeed" } },
    };

    /// Multiplies the metric serialized fields of `c` (and of its base classes) by S. Call once per component,
    /// after all its values are set.
    static void MetricScale(Component c)
    {
        var so = new SerializedObject(c);
        for (var t = c.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
        {
            if (!MetricFields.TryGetValue(t, out var fields))
                continue;
            foreach (var f in fields)
            {
                var p = so.FindProperty(f);
                if (p == null) { Debug.LogError($"[MonsterSetup] {t.Name} has no field {f}"); continue; }
                if (p.propertyType == SerializedPropertyType.Float) p.floatValue *= S;
                else if (p.propertyType == SerializedPropertyType.Vector2) p.vector2Value *= S;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // How a monster looks: smoke colours, shell, particles. Variety between monsters comes from here.
    class Look
    {
        public Color tint = Color.white, smoke, smokeDark, rim, glow;
        public float glowThreshold = 0.55f;
        public float inflate = 0.045f, shellOpacity = 0.6f, threshold = 0.42f, bottom = 0.6f, top = 0.1f, flow = 0.35f;
        public float breathe = 0.02f, warp = 0.02f, hem = 0.8f;
        public float wispRate = 18f, wispSize = 0.35f, wispRise = 0.15f;
        public bool trail;       // smoke pouring from the bottom (floaters)
        public bool groundFog;   // smoke pooling round the feet (walkers)
        public int motes;        // little dark orbs floating around
    }

    class Spec
    {
        public string name, folder, display;
        public float health, speed, aggro;
        public Look look;
        public System.Action<GameObject, Monster, Ctx> attacks;
        public System.Action<GameObject, Ctx> brain;        // movement/attack pattern
    }

    // shared generated assets the attack setup needs
    class Ctx
    {
        public Dictionary<string, Projectile> bolts = new Dictionary<string, Projectile>();
        public Dictionary<Projectile, GameObject> flashes = new Dictionary<Projectile, GameObject>();   // muzzle flash per bolt
        public GameObject puff, landing, telegraph, orb, wallImpact;
        public LineRenderer beam;
        public MonsterImporter.Meta meta;
    }

    static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

    static readonly Spec[] Monsters =
    {
        new Spec { name = "ball_shadow", folder = "BallShadow", display = "BallShadow", health = 20, speed = 1.4f, aggro = 10,
            look = new Look { smoke = C(.40f, .30f, .58f), smokeDark = C(.09f, .06f, .14f), rim = C(.45f, .3f, .8f), glow = C(2.6f, 1.6f, 4.2f),
                              inflate = .07f, shellOpacity = .7f, threshold = .38f, bottom = .3f, top = .9f, breathe = .03f, warp = .03f, hem = .3f,
                              wispRate = 26, wispSize = .45f, wispRise = .3f, motes = 7 },
            attacks = (go, m, ctx) => Set(Lunge(go, 8, 1.8f), "minRange", 0f, "maxRange", 1.3f),
            brain = (go, ctx) => go.AddComponent<WanderBrain>() },
        new Spec { name = "twisted_ball", folder = "TwistedBall", display = "TwistedBall", health = 20, speed = 1.5f, aggro = 10,
            look = new Look { smoke = C(.52f, .28f, .62f), smokeDark = C(.12f, .06f, .15f), rim = C(.6f, .3f, .85f),
                              glow = C(2.8f, 1.5f, 4.4f), inflate = .06f, shellOpacity = .65f, threshold = .4f, bottom = .2f, top = .8f,
                              breathe = .035f, warp = .035f, hem = .3f, wispRate = 24, wispSize = .4f, wispRise = .35f, motes = 6 },
            attacks = (go, m, ctx) => Lunge(go, 8, 2.8f),
            brain = (go, ctx) => Set(go.AddComponent<WanderBrain>(), "rushRange", 4.5f, "rushSpeed", 2.4f) },
        new Spec { name = "regular", folder = "Regular", display = "Regular", health = 40, speed = 1.2f, aggro = 12,
            look = new Look { smoke = C(.36f, .30f, .52f), smokeDark = C(.08f, .07f, .12f), rim = C(.4f, .32f, .7f), glow = C(1.6f, 2.0f, 3.4f),
                              inflate = .04f, shellOpacity = .55f, bottom = 1.2f, top = .1f, hem = .9f, wispRate = 18, trail = true },
            attacks = (go, m, ctx) => Shoot(go, ctx.bolts["Violet"], new[] { "Muzzle.R" }, 8, 8f, 0, 2.2f),
            brain = (go, ctx) => go.AddComponent<RangedBrain>() },
        new Spec { name = "3_eyed", folder = "ThreeEyed", display = "ThreeEyed", health = 40, speed = 1.2f, aggro = 12,
            look = new Look { smoke = C(.30f, .32f, .55f), smokeDark = C(.07f, .08f, .13f), rim = C(.35f, .4f, .8f), glow = C(1.4f, 2.2f, 3.8f),
                              inflate = .04f, shellOpacity = .55f, bottom = 1.1f, top = .15f, hem = .9f, wispRate = 18, trail = true },
            attacks = (go, m, ctx) =>
            {
                Set(Shoot(go, ctx.bolts["Cyan"], new[] { "Muzzle.L", "Muzzle.R" }, 6, 9f, 3, 2.6f), "fromMiddle", true, "fanAngle", 14f);
                Set(Add<TeleportAttack>(go, "Teleport", 0, 0f, 30f, 6f), "smokePuff", ctx.puff, "minDistance", 5f, "maxDistance", 8f,
                    "burstRadius", 0f);
            },
            brain = (go, ctx) => Set(go.AddComponent<RangedBrain>(), "keepAt", new Vector2(5f, 9f), "stayFor", new Vector2(4f, 7f)) },
        new Spec { name = "ghost", folder = "Ghost", display = "Ghost", health = 40, speed = 1.6f, aggro = 12,
            look = new Look { smoke = C(.45f, .40f, .56f), smokeDark = C(.11f, .10f, .14f), rim = C(.5f, .45f, .75f), glow = C(2.0f, 2.2f, 3.2f),
                              inflate = .045f, shellOpacity = .6f, bottom = 1.4f, top = .1f, hem = 1.2f, warp = .025f, wispRate = 16, trail = true },
            attacks = (go, m, ctx) => Melee(go, "Punch", new[] { "Hand.R" }, 10, 1.3f, 1.6f, .35f),
            brain = (go, ctx) => go.AddComponent<ChaseBrain>() },
        new Spec { name = "crescent", folder = "Crescent", display = "Crescent", health = 60, speed = 1.1f, aggro = 13,
            look = new Look { smoke = C(.42f, .30f, .62f), smokeDark = C(.09f, .06f, .14f), rim = C(.5f, .32f, .85f), glow = C(2.6f, 1.6f, 4.0f),
                              inflate = .04f, shellOpacity = .55f, bottom = 1.2f, top = .3f, hem = .9f, wispRate = 22, trail = true },
            attacks = (go, m, ctx) => Set(Shoot(go, ctx.bolts["Boomerang"], new[] { "Muzzle.L" }, 8, 9f, 0, 3.2f),
                                          "minRange", 2f, "maxRange", 7f, "cruiseHeight", .6f),
            brain = (go, ctx) => Set(go.AddComponent<RangedBrain>(), "keepAt", new Vector2(5f, 8f), "hop", 3f) },
        new Spec { name = "ghoul", folder = "Ghoul", display = "Ghoul", health = 60, speed = 1.3f, aggro = 13,
            look = new Look { smoke = C(.26f, .22f, .42f), smokeDark = C(.05f, .04f, .09f), rim = C(.3f, .26f, .6f), glow = C(2.0f, 2.0f, 3.0f),
                              inflate = .05f, shellOpacity = .65f, bottom = 1.5f, top = .05f, hem = 1.3f, flow = .25f, wispRate = 22, trail = true },
            attacks = (go, m, ctx) => Melee(go, "Punch", new[] { "Hand.R" }, 12, 1.6f, 1.8f, .5f),
            brain = (go, ctx) => Set(go.AddComponent<ChaseBrain>(), "circleSpeed", .3f) },
        new Spec { name = "horned", folder = "Horned", display = "Horned", health = 60, speed = 1.5f, aggro = 13,
            look = new Look { smoke = C(.46f, .24f, .46f), smokeDark = C(.10f, .05f, .10f), rim = C(.6f, .3f, .7f), glow = C(2.6f, 1.8f, 3.6f),
                              inflate = .045f, shellOpacity = .6f, bottom = 1.2f, top = .2f, hem = 1f, wispRate = 20, trail = true },
            attacks = (go, m, ctx) =>
            {
                Melee(go, "Punch", new[] { "Hand.L", "Hand.R" }, 9, 1.6f, 2.2f, .45f);
                Set(Shoot(go, ctx.bolts["Plum"], new[] { "Muzzle.R" }, 7, 9f, 2, 1.4f), "minRange", 2.5f, "maxRange", 9f, "fanAngle", 8f);
            },
            brain = (go, ctx) => go.AddComponent<SkirmisherBrain>() },
        new Spec { name = "claw", folder = "Claw", display = "Claw", health = 45, speed = 2.6f, aggro = 13,
            look = new Look { smoke = C(.42f, .24f, .48f), smokeDark = C(.09f, .05f, .11f), rim = C(.6f, .28f, .7f), glow = C(3.0f, 1.2f, 2.6f),
                              inflate = .045f, shellOpacity = .6f, bottom = 1.3f, top = .25f, hem = 1.1f, warp = .03f, wispRate = 24, trail = true },
            attacks = (go, m, ctx) =>
            {
                Set(Melee(go, "Slash", new[] { "Hand.L", "Hand.R" }, 9, 1.5f, 1.8f, .6f), "handRadius", .4f);
                Set(Add<RadialShotAttack>(go, "Shoot", 6, 0f, 6f, 2.6f), "projectile", ctx.bolts["Rose"], "directions", 8,
                    "speed", 6.5f, "muzzleFlash", ctx.flashes[ctx.bolts["Rose"]]);
                Set(Add<TeleportAttack>(go, "Teleport", 0, 0f, 30f, 4f), "smokePuff", ctx.puff, "minDistance", 1.5f, "maxDistance", 3f,
                    "burstRadius", 0f);
            },
            brain = (go, ctx) => go.AddComponent<DartBrain>() },
        new Spec { name = "fat", folder = "Fat", display = "Fat", health = 250, speed = 1.1f, aggro = 15,
            look = new Look { smoke = C(.36f, .33f, .44f), smokeDark = C(.08f, .07f, .10f), rim = C(.42f, .36f, .6f), glow = C(1.8f, 1.8f, 2.6f),
                              glowThreshold = .7f, inflate = .045f, shellOpacity = .6f, bottom = .5f, top = .3f, hem = .15f, breathe = .025f,
                              wispRate = 30, wispSize = .3f, groundFog = true, motes = 3 },
            attacks = (go, m, ctx) =>
            {
                // flails while it shuffles in on the target, then stops
                var flail = Melee(go, "Flail", new[] { "Hand.L", "Hand.R" }, 6, 4.5f, 4f, 3f);
                Set(flail, "handRadius", .3f, "reach", .75f, "advanceFor", .6f, "trackTarget", 1f);
                var jump = Add<JumpSmashAttack>(go, "Jump", 25, 2.5f, 7f, 6f);
                Set(jump, "telegraph", ctx.telegraph); Set(jump, "landingEffect", ctx.landing);
                Set(jump, "airTime", EventGap(ctx.meta, "Jump", "TakeOff", "Land"));
                Set(jump, "landingShot", ctx.bolts["Spit"], "landingShotChance", .5f, "landingShotScale", 1.3f);
                // half the spits hatch a shadow ball where they land
                var ball = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/BallShadow.prefab");
                Set(ctx.bolts["Summon"], "summon", ball, "maxSummons", 3);
                PrefabUtility.SavePrefabAsset(ctx.bolts["Summon"].gameObject);
                var spit = Add<SpitAttack>(go, "Spit", 9, 3f, 10f, 4f);
                Set(spit, "summonGlob", ctx.bolts["Summon"], "pellet", ctx.bolts["Spit"], "muzzleFlash", ctx.flashes[ctx.bolts["Spit"]]);
            },
            brain = (go, ctx) => Set(go.AddComponent<FatBoss>(), "title", "The Bloated One", "enrageEffect", ctx.puff) },
        new Spec { name = "muscle", folder = "Muscle", display = "Muscle", health = 80, speed = 1.3f, aggro = 13,
            look = new Look { smoke = C(.40f, .34f, .62f), smokeDark = C(.08f, .07f, .13f), rim = C(.45f, .4f, .85f), glow = C(1.4f, 2.4f, 4.2f),
                              inflate = .06f, shellOpacity = .7f, threshold = .37f, bottom = .4f, top = .4f, hem = .1f, warp = .025f,
                              wispRate = 26, wispSize = .3f, wispRise = .25f, groundFog = true },
            attacks = (go, m, ctx) =>
            {
                Set(Add<ChargeAttack>(go, "Charge", 15, 3f, 10f, 4f), "impactEffect", ctx.wallImpact);
                Melee(go, "Punch", new[] { "Hand.R" }, 12, 1.6f, 1.6f, .5f);
            },
            brain = (go, ctx) => go.AddComponent<ChargerBrain>() },
        new Spec { name = "muscle_boss", folder = "MuscleBoss", display = "MuscleBoss", health = 320, speed = 1.3f, aggro = 16,
            look = new Look { smoke = C(.38f, .30f, .60f), smokeDark = C(.08f, .06f, .13f), rim = C(.5f, .38f, .85f), glow = C(2.4f, 1.6f, 4.2f),
                              inflate = .07f, shellOpacity = .75f, threshold = .35f, bottom = .4f, top = .6f, hem = .1f, warp = .025f,
                              wispRate = 44, wispSize = .3f, wispRise = .3f, groundFog = true, motes = 4 },
            attacks = (go, m, ctx) =>
            {
                // the charge: slamming into a wall sends bolts flying all eight ways
                Set(Add<ChargeAttack>(go, "Charge", 25, 3f, 14f, 6f), "impactEffect", ctx.wallImpact,
                    "wallShot", ctx.bolts["Purple"], "wallShotCount", 8, "wallShotDamage", 8f);
                // the spin: bolts north, east, south and west, twice, a beat apart
                Set(Add<SpinAttack>(go, "Spin", 8, 0f, 3f, 7f), "shot", ctx.bolts["Purple"], "volleys", 2, "firstVolley", .6f,
                    "volleyInterval", 1f);
                // the barrage: two walls of bullets sweeping across the target
                Set(Add<BulletPatternAttack>(go, "Cast", 6, 0f, 14f, 8f), "pattern", (int)BulletPatternAttack.Pattern.Wall,
                    "projectile", ctx.bolts["Purple"], "repeats", 2, "interval", 1.2f, "speed", 4f, "spawnPuff", ctx.puff);
            },
            brain = (go, ctx) => Set(go.AddComponent<MuscleBoss>(), "title", "The Brute", "enrageEffect", ctx.wallImpact) },
        new Spec { name = "skull_face", folder = "SkullFace", display = "SkullFace", health = 350, speed = 1.5f, aggro = 18,
            look = new Look { smoke = C(.38f, .28f, .56f), smokeDark = C(.07f, .05f, .11f), rim = C(.45f, .3f, .8f), glow = C(2.6f, 1.5f, 4.4f),
                              inflate = .05f, shellOpacity = .65f, bottom = 1.6f, top = .4f, hem = 1f, wispRate = 30, wispSize = .3f,
                              trail = true, motes = 4 },
            attacks = (go, m, ctx) =>
            {
                var laser = Add<LaserAttack>(go, "ChargedLaser", 5, 3f, 16f, 8f);
                Set(laser, "chargeOrb", ctx.orb); Set(laser, "beam", ctx.beam);
                var tp = Add<TeleportAttack>(go, "Teleport", 10, 0f, 30f, 9f);
                Set(tp, "smokePuff", ctx.puff); Set(tp, "weight", .6f);
                var spin = Add<ProjectileAttack>(go, "SpinShoot", 7, 0f, 10f, 6f);
                Set(spin, "projectile", ctx.bolts["Purple"]); SetStrings(spin, "sockets", "Muzzle.L", "Muzzle.R");
                Set(spin, "aim", (int)ProjectileAttack.Aim.Outward); Set(spin, "speed", 7f); Set(spin, "projectileScale", 1.4f);
                Set(spin, "cruiseHeight", .6f);
                var bomb = Add<ProjectileAttack>(go, "Throw", 10, 2f, 12f, 7f);
                Set(bomb, "projectile", ctx.bolts["Bomb"], "aim", (int)ProjectileAttack.Aim.Lob, "speed", 7f, "gravity", 12f, "splash", 1.2f,
                    "projectileScale", 1.5f);
                SetStrings(bomb, "sockets", "Muzzle.R");
                BulletPatternAttack Pattern(BulletPatternAttack.Pattern kind, int repeats, float interval, float speed) =>
                    Set(Add<BulletPatternAttack>(go, "Cast", 6, 0f, 14f, 6f), "pattern", (int)kind, "projectile", ctx.bolts["Purple"],
                        "repeats", repeats, "interval", interval, "speed", speed, "spawnPuff", ctx.puff);
                Pattern(BulletPatternAttack.Pattern.Cross, 24, .14f, 5f);
                Pattern(BulletPatternAttack.Pattern.Flower, 4, .9f, 3.6f);
            },
            brain = (go, ctx) => Set(go.AddComponent<SkullFaceBoss>(), "title", "The Hollow Skull", "enrageEffect", ctx.puff) },
    };

    [MenuItem("Roguelike/Build Monsters")]
    public static void BuildAll()
    {
        // the build replaces the open scene: offer to save it first
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        AssetDatabase.Refresh();
        Directory.CreateDirectory(GeneratedDir);
        Directory.CreateDirectory(PrefabDir);
        var ctx = BuildEffects();
        var prefabs = new List<GameObject>();
        foreach (var spec in Monsters)
        {
            var prefab = BuildMonster(spec, ctx);
            if (prefab) prefabs.Add(prefab);
        }
        BuildCatalog(prefabs);
        BuildScene(prefabs, ctx);
        AssetDatabase.SaveAssets();
        if (!Application.isBatchMode)
            EditorSceneManager.OpenScene(ScenePath);
        Debug.Log($"[MonsterSetup] built {prefabs.Count} monster prefabs");
    }

    // ------------------------------------------------------------------------------------------ textures

    static Texture2D MakeTexture(string name, int w, int h, System.Func<float, float, float> alpha)
    {
        string path = $"{GeneratedDir}/{name}.png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float a = Mathf.Clamp01(alpha((x + 0.5f) / w, (y + 0.5f) / h));
            px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // GLSL-style smoothstep (Mathf.SmoothStep interpolates between its first two arguments instead)
    static float Smooth(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    static float Fbm(float x, float y)
    {
        float v = 0, a = .5f, f = 1;
        for (int i = 0; i < 4; i++) { v += a * Mathf.PerlinNoise(x * f + 17.3f * i, y * f + 5.1f * i); a *= .5f; f *= 2.1f; }
        return v / .9375f;
    }

    static Texture2D SmokePuffTexture() => MakeTexture("SmokePuff", 128, 128, (u, v) =>
    {
        float dx = u * 2 - 1, dy = v * 2 - 1;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        float ang = Mathf.Atan2(dy, dx) + r * 2.4f;                 // a curl, like the swirls in the reference art
        float n = Fbm(Mathf.Cos(ang) * r * 3f + 4f, Mathf.Sin(ang) * r * 3f + 4f);
        float body = Mathf.Pow(Mathf.Clamp01(1 - r), 1.2f);
        return body * Smooth(0.35f, 0.85f, n * 0.9f + body * 0.45f);
    });

    static Texture2D SoftDotTexture() => MakeTexture("SoftDot", 64, 64, (u, v) =>
    {
        float dx = u * 2 - 1, dy = v * 2 - 1;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        return Smooth(1f, 0.45f, r);
    });

    static Texture2D RingTexture() => MakeTexture("Ring", 128, 128, (u, v) =>
    {
        float dx = u * 2 - 1, dy = v * 2 - 1;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        float ring = Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.06f, 2f));
        return r > 1f ? 0f : ring + 0.18f * Mathf.Clamp01(1 - r);
    });

    static Texture2D BeamTexture() => MakeTexture("Beam", 32, 32, (u, v) => Mathf.Exp(-Mathf.Pow((v - 0.5f) / 0.2f, 2f)));

    // ------------------------------------------------------------------------------------------ materials

    static Material Mat(string name, string shader, System.Action<Material> setup)
    {
        string path = $"{GeneratedDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = Shader.Find(shader);
        if (sh == null) Debug.LogError($"[MonsterSetup] shader {shader} not found");
        if (m == null)
        {
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        else
        {
            m.shader = sh;
        }
        setup(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material ParticleMat(string name, Texture tex, Color tint, bool additive) => Mat(name, "Roguelike/SmokeParticle", m =>
    {
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
    });

    // ------------------------------------------------------------------------------------------ particles

    static ParticleSystem NewSystem(Transform parent, string name, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.playOnAwake = true;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.maxParticleSize = 2f;
        return ps;
    }

    static Gradient Fade(float peak, float peakAt = 0.3f)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, peakAt), new GradientAlphaKey(peak * 0.6f, 0.7f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    static void Velocity(ParticleSystem ps, Vector3 v, ParticleSystemSimulationSpace space, float orbitalY = 0f)
    {
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = space;
        vel.x = new ParticleSystem.MinMaxCurve(v.x);
        vel.y = new ParticleSystem.MinMaxCurve(v.y);
        vel.z = new ParticleSystem.MinMaxCurve(v.z);
        if (orbitalY != 0f)
        {
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(orbitalY * 0.7f, orbitalY * 1.3f);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
        }
    }

    static void Noise(ParticleSystem ps, float strength, float frequency, float scroll)
    {
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = strength;
        noise.frequency = frequency;
        noise.scrollSpeed = scroll;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;
    }

    static void Grow(ParticleSystem ps, float from, float to)
    {
        var s = ps.sizeOverLifetime;
        s.enabled = true;
        s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
    }

    static void Spin(ParticleSystem ps, float degPerSec)
    {
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-degPerSec * Mathf.Deg2Rad, degPerSec * Mathf.Deg2Rad);
    }

    static void Colors(ParticleSystem ps, Color a, Color b, float peakAlpha)
    {
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Fade(peakAlpha);
    }

    /// Wisps curling off the whole surface, the fog of the reference art.
    static void AddWisps(GameObject go, SkinnedMeshRenderer smr, Look look, float H, Material mat)
    {
        var ps = NewSystem(go.transform, "Smoke", mat);
        var main = ps.main;
        main.duration = 4f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f * H, 0.12f * H);
        main.startSize = new ParticleSystem.MinMaxCurve(look.wispSize * 0.6f * H, look.wispSize * 1.1f * H);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // trails behind when the monster moves
        main.maxParticles = 160;
        main.prewarm = true;
        var em = ps.emission;
        em.rateOverTime = look.wispRate;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
        shape.skinnedMeshRenderer = smr;
        shape.normalOffset = 0.02f * H;
        Velocity(ps, new Vector3(0f, look.wispRise * H, 0f), ParticleSystemSimulationSpace.World);
        Noise(ps, 0.25f * H, 0.8f, 0.4f);
        Grow(ps, 0.5f, 1.4f);
        Spin(ps, 45f);
        Colors(ps, look.smoke, Color.Lerp(look.smoke, look.smokeDark, 0.6f), 0.65f);
    }

    /// Thick smoke pouring out of the bottom instead of legs/tail (image 1 -> 2).
    static void AddTrail(GameObject go, Look look, float H, float bottom, Material mat)
    {
        var hips = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Hips");
        var ps = NewSystem(hips ? hips : go.transform, "SmokeTrail", mat);
        ps.transform.position = go.transform.position + Vector3.up * (bottom + 0.12f * H);
        ps.transform.rotation = Quaternion.Euler(90f, 0f, 0f);     // cone opens downward
        var main = ps.main;
        main.duration = 4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f * H, 0.25f * H);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f * H, 0.45f * H);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        main.prewarm = true;
        main.gravityModifier = -0.01f;
        var em = ps.emission;
        em.rateOverTime = 22f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.16f * H;
        Noise(ps, 0.3f * H, 0.9f, 0.5f);
        Grow(ps, 0.8f, 0.3f);
        Spin(ps, 60f);
        Colors(ps, Color.Lerp(look.smoke, look.smokeDark, 0.35f), look.smokeDark, 0.75f);
    }

    /// Low smoke pooling around the feet of the walkers.
    static void AddGroundFog(GameObject go, Look look, float H, float width, Material mat)
    {
        var ps = NewSystem(go.transform, "GroundFog", mat);
        ps.transform.localPosition = Vector3.up * 0.05f * H;
        ps.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var main = ps.main;
        main.duration = 4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.1f * H);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f * H, 0.45f * H);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        main.prewarm = true;
        var em = ps.emission;
        em.rateOverTime = 14f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = width * 0.45f;
        shape.radiusThickness = 0.5f;
        Noise(ps, 0.2f * H, 0.6f, 0.3f);
        Grow(ps, 0.6f, 1.2f);
        Spin(ps, 30f);
        Colors(ps, look.smoke, look.smokeDark, 0.45f);
    }

    /// Small dark orbs drifting around the monster (image 5 -> 6).
    static void AddMotes(GameObject go, Look look, float H, float width, Material mat)
    {
        var ps = NewSystem(go.transform, "Motes", mat);
        ps.transform.localPosition = Vector3.up * H * 0.55f;
        var main = ps.main;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f * H, 0.1f * H);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = look.motes * 2;
        main.prewarm = true;
        var em = ps.emission;
        em.rateOverTime = look.motes / 4f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(width, H) * 0.6f;
        shape.radiusThickness = 0.2f;
        Velocity(ps, new Vector3(0f, 0.05f * H, 0f), ParticleSystemSimulationSpace.Local, 0.5f);
        Noise(ps, 0.05f * H, 0.5f, 0.2f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Fade(1f, 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(look.smokeDark, look.smoke, 0.35f), Color.Lerp(look.smokeDark, look.smoke, 0.6f));
    }

    static ParticleSystem Burst(GameObject parent, string name, Material mat, int count, Color a, Color b,
                                float speed, float size, float life, ParticleSystemShapeType shapeType, float radius)
    {
        var ps = NewSystem(parent.transform, name, mat);
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = shapeType;
        shape.radius = radius;
        Noise(ps, 0.3f, 1f, 0.5f);
        Grow(ps, 0.6f, 1.5f);
        Spin(ps, 90f);
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.12f;
        limit.limit = 0.2f;
        Colors(ps, a, b, 0.8f);
        return ps;
    }

    // ------------------------------------------------------------------------------------------ effects

    class BoltSpec
    {
        public string key, mesh;
        public Color core, rim, eyes, smoke = C(.42f, .32f, .6f);
        public float radius = .12f, lifetime = 4f, spin, returnAfter, halo = 1f, opacity = .85f, smokeRate = 18f;

        public BoltSpec(string key, string mesh, Color core, Color rim, Color eyes)
        {
            this.key = key; this.mesh = mesh; this.core = core; this.rim = rim; this.eyes = eyes;
        }
    }

    static Projectile MakeBolt(BoltSpec b, Ctx ctx, Material smokeMat, Texture2D dot)
    {
        string meshPath = $"{MonsterImporter.ProjectileDir}{b.mesh}.fbx";
        var mesh = AssetDatabase.LoadAllAssetsAtPath(meshPath).OfType<Mesh>().FirstOrDefault();
        if (mesh == null)
            Debug.LogError($"[MonsterSetup] {meshPath} missing, run Tools/make_projectiles.py");
        float size = mesh ? Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z) : .3f;
        var go = new GameObject($"Bolt_{b.key}");
        var body = new GameObject("Body");
        body.transform.SetParent(go.transform, false);
        body.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = body.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sharedMaterials = new[]
        {
            Mat($"Bolt_{b.key}", "Roguelike/GhostBolt", m =>
            {
                // a thick streak of darkness with only a faint coloured edge, not a glowing light
                m.SetColor("_CoreColor", b.core * 0.06f);
                m.SetColor("_RimColor", b.rim * 0.22f);
                m.SetFloat("_Density", b.mesh == "Orb" || b.mesh == "Glob" ? 0.8f : 0.3f);
                m.SetFloat("_TailWhip", 0.08f);
                m.SetColor("_EyeColor", b.eyes * 0.12f);
                m.SetFloat("_Opacity", b.opacity);
                m.SetFloat("_Size", size);
            }),
            Mat($"BoltSmoke_{b.key}", "Roguelike/GhostSmoke", m =>
            {
                m.SetColor("_SmokeColor", Color.Lerp(b.smoke * 0.35f, b.core * 0.15f, 0.4f));
                m.SetColor("_SmokeDark", C(.03f, .02f, .05f));
                m.SetFloat("_Opacity", .55f);
                m.SetFloat("_Inflate", .12f);
                m.SetFloat("_NoiseScale", 3f);
                m.SetFloat("_FlowSpeed", 1.2f);
                m.SetFloat("_Threshold", .38f);
                m.SetFloat("_BottomBoost", 0f);
                m.SetFloat("_TopBoost", 0f);
                m.SetFloat("_Size", size);
                m.SetFloat("_BreatheAmount", .06f);
                m.SetFloat("_BreatheSpeed", 6f);
                m.SetFloat("_WarpAmount", .08f);
                m.SetFloat("_WarpSpeed", 2f);
                m.SetFloat("_HemSway", 0f);
            }),
        };
        var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(halo.GetComponent<Collider>());
        halo.name = "Halo";
        halo.transform.SetParent(go.transform, false);
        halo.transform.localScale = Vector3.one * size * 2.2f * b.halo;
        halo.AddComponent<Billboard>();
        var haloRenderer = halo.GetComponent<MeshRenderer>();
        haloRenderer.sharedMaterial = ParticleMat($"BoltHalo_{b.key}", dot, b.rim * 0.05f, true);
        haloRenderer.shadowCastingMode = ShadowCastingMode.Off;
        // a trail of smoke puffs left hanging in the air
        var trail = NewSystem(go.transform, "SmokeTrail", smokeMat);
        var main = trail.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size * 0.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        var em = trail.emission;
        em.rateOverTime = 0f;
        em.rateOverDistance = b.smokeRate;
        var shape = trail.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = size * 0.2f;
        Noise(trail, 0.15f, 1.5f, 0.5f);
        Grow(trail, 0.7f, 1.6f);
        Spin(trail, 60f);
        Colors(trail, b.smoke * 0.35f, C(.03f, .02f, .05f), 0.7f);
        var p = go.AddComponent<Projectile>();
        Set(p, "radius", b.radius, "lifetime", b.lifetime, "spin", b.spin, "returnAfter", b.returnAfter);
        Set(p, "impactEffect", ctx.puff);
        return SavePrefab(go, $"Bolt_{b.key}").GetComponent<Projectile>();
    }

    static GameObject SavePrefab(GameObject go, string name)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{GeneratedDir}/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    static Ctx BuildEffects()
    {
        var ctx = new Ctx();
        var puffTex = SmokePuffTexture();
        var dot = SoftDotTexture();
        var ring = RingTexture();
        var beamTex = BeamTexture();
        var smokeMat = ParticleMat("SmokeParticle", puffTex, Color.white, false);
        var moteMat = ParticleMat("Mote", dot, Color.white, false);
        var white = Texture2D.whiteTexture;

        // smoke puff (teleport, bolt impacts)
        var puff = new GameObject("SmokePuff");
        Burst(puff, "Puff", smokeMat, 24, C(.42f, .32f, .6f), C(.1f, .08f, .15f), 1.4f, 0.45f, 1.1f, ParticleSystemShapeType.Sphere, 0.2f);
        ctx.puff = SavePrefab(puff, "SmokePuff");

        // ring of dust/smoke where the fat one lands
        var landing = new GameObject("LandingSmoke");
        var lps = Burst(landing, "Ring", smokeMat, 40, C(.4f, .36f, .48f), C(.12f, .1f, .14f), 3.5f, 0.6f, 1.2f, ParticleSystemShapeType.Circle, 0.4f);
        lps.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        ctx.landing = SavePrefab(landing, "LandingSmoke");

        var impact = new GameObject("WallImpact");
        Burst(impact, "Dust", smokeMat, 30, C(.45f, .4f, .55f), C(.15f, .12f, .18f), 2.5f, 0.4f, 1f, ParticleSystemShapeType.Hemisphere, 0.2f);
        ctx.wallImpact = SavePrefab(impact, "WallImpact");

        // landing telegraph: a glowing ring on the floor
        var telegraph = new GameObject("JumpTelegraph");
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(telegraph.transform, false);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.GetComponent<MeshRenderer>().sharedMaterial = ParticleMat("Telegraph", ring, new Color(2.2f, .5f, 1.2f, .9f), true);
        quad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        ctx.telegraph = SavePrefab(telegraph, "JumpTelegraph");

        // projectiles: little ghosts of smoke (Tools/make_projectiles.py meshes, Roguelike/GhostBolt + a smoke
        // shell), a glow round them, a trail of smoke, a puff on impact, and a muzzle flash for the shooter
        var specs = new[]
        {
            //            key          mesh     core                           rim                            eyes
            new BoltSpec("Violet",    "Orb",   new Color(1.0f, .45f, 2.2f), new Color(2.0f, 1.2f, 4.0f), new Color(4f, 3.6f, 5f)),
            new BoltSpec("Cyan",      "Orb",   new Color(.4f, 1.2f, 2.4f), new Color(1.2f, 2.6f, 4.0f), new Color(3.6f, 4.6f, 5f)),
            new BoltSpec("Plum",      "Orb",   new Color(1.8f, .4f, 1.4f), new Color(3.4f, 1.2f, 2.8f), new Color(5f, 3.6f, 4.4f)),
            new BoltSpec("Purple",    "Orb",   new Color(1.4f, .35f, 2.4f), new Color(2.6f, .9f, 4.2f), new Color(4.4f, 3.2f, 5f)),
            new BoltSpec("Rose",      "Orb",   new Color(2.0f, .4f, .9f), new Color(3.6f, 1.2f, 2.0f), new Color(5f, 3.8f, 4f)),
            new BoltSpec("Boomerang", "Blade", new Color(1.4f, 1.0f, 2.6f), new Color(2.6f, 2.0f, 4.4f), new Color(4.4f, 4f, 5f))
                { radius = .2f, lifetime = 6f, spin = 900f, returnAfter = 6f, halo = 1.4f },
            new BoltSpec("Spit",      "Glob",  new Color(.5f, 1.6f, .3f), new Color(1.2f, 3.0f, .7f), new Color(3f, 5f, 2f))
                { smoke = C(.3f, .42f, .22f) },
            new BoltSpec("Summon",    "Glob",  new Color(.55f, .3f, 1.0f), new Color(1.4f, .9f, 2.6f), new Color(3f, 2.4f, 4.4f))
                { radius = .16f, lifetime = 5f, halo = 1.3f },
            new BoltSpec("Bomb",      "Bomb",  new Color(.5f, .25f, .9f), new Color(1.4f, .7f, 2.6f), new Color(4.6f, 2.2f, 5f))
                { radius = .2f, lifetime = 5f, halo = 1.6f, opacity = .85f, smokeRate = 30f },
        };
        foreach (var b in specs)
            ctx.bolts[b.key] = MakeBolt(b, ctx, smokeMat, dot);
        // the bomb bursts into a ring of eight bolts
        Set(ctx.bolts["Bomb"], "burstInto", ctx.bolts["Purple"], "burstCount", 8, "burstSpeed", 5f, "burstDamage", 6f);
        foreach (var bolt in ctx.bolts.Values)
        {
            MetricScale(bolt);
            PrefabUtility.SavePrefabAsset(bolt.gameObject);
        }
        foreach (var b in specs)
        {
            // muzzle flash: sparks of the bolt's colour bursting off the hand, and a wisp of smoke
            var flash = new GameObject($"MuzzleFlash_{b.key}");
            var sparks = Burst(flash, "Sparks", ParticleMat($"Spark_{b.key}", dot, b.rim, true), 24, Color.white, Color.white,
                               2.8f, 0.2f, 0.35f, ParticleSystemShapeType.Sphere, 0.03f);
            var sparkMain = sparks.main;
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            var sparkGrow = sparks.sizeOverLifetime;
            sparkGrow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var glow = Burst(flash, "Glow", ParticleMat($"FlashGlow_{b.key}", dot, b.rim * 0.6f, true), 1, Color.white, Color.white,
                             0f, 0.9f, 0.18f, ParticleSystemShapeType.Sphere, 0.001f);
            var glowGrow = glow.sizeOverLifetime;
            glowGrow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.4f));
            Burst(flash, "Smoke", smokeMat, 6, b.smoke, C(.1f, .08f, .15f), 0.6f, 0.35f, 0.7f, ParticleSystemShapeType.Sphere, 0.05f);
            ctx.flashes[ctx.bolts[b.key]] = SavePrefab(flash, $"MuzzleFlash_{b.key}");
        }

        // laser: charge orb + beam
        var orb = new GameObject("LaserChargeOrb");
        var orbCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(orbCore.GetComponent<Collider>());
        orbCore.transform.SetParent(orb.transform, false);
        orbCore.GetComponent<MeshRenderer>().sharedMaterial = ParticleMat("LaserOrb", white, new Color(2.4f, .8f, 4f), true);
        var orbHalo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(orbHalo.GetComponent<Collider>());
        orbHalo.transform.SetParent(orb.transform, false);
        orbHalo.transform.localScale = Vector3.one * 3f;
        orbHalo.AddComponent<Billboard>();
        orbHalo.GetComponent<MeshRenderer>().sharedMaterial = ParticleMat("LaserOrbHalo", dot, new Color(1.6f, .5f, 2.8f, .8f), true);
        var light = new GameObject("Light").AddComponent<Light>();
        light.transform.SetParent(orb.transform, false);
        light.type = LightType.Point;
        light.color = new Color(.7f, .4f, 1f);
        light.intensity = 4f;
        light.range = 4f * S;
        ctx.orb = SavePrefab(orb, "LaserChargeOrb");

        var beam = new GameObject("LaserBeam");
        var line = beam.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.numCapVertices = 6;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.sharedMaterial = ParticleMat("LaserBeam", beamTex, new Color(2.6f, 1.0f, 4.4f), true);
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.startWidth = line.endWidth = 0.5f * S;
        ctx.beam = SavePrefab(beam, "LaserBeam").GetComponent<LineRenderer>();

        return ctx;
    }

    // ------------------------------------------------------------------------------------------ monsters

    static T Add<T>(GameObject go, string state, float damage, float min, float max, float cooldown) where T : MonsterAttack
    {
        var a = go.AddComponent<T>();
        Set(a, "state", state);
        Set(a, "damage", damage);
        Set(a, "minRange", min);
        Set(a, "maxRange", max);
        Set(a, "cooldown", cooldown);
        return a;
    }

    static ProjectileAttack Shoot(GameObject go, Projectile bolt, string[] sockets, float damage, float speed, int count, float cooldown)
    {
        var a = Add<ProjectileAttack>(go, "Shoot", damage, 1.5f, 10f, cooldown);
        Set(a, "projectile", bolt);
        SetStrings(a, "sockets", sockets);
        Set(a, "speed", speed);
        Set(a, "count", Mathf.Max(1, count));
        return a;
    }

    static MeleeAttack Melee(GameObject go, string state, string[] hands, float damage, float range, float cooldown, float lunge)
    {
        var a = Add<MeleeAttack>(go, state, damage, 0f, range, cooldown);
        SetStrings(a, "hands", hands);
        Set(a, "lunge", lunge);
        return a;
    }

    static LungeAttack Lunge(GameObject go, float damage, float dash)
    {
        var a = Add<LungeAttack>(go, "Lunge", damage, 0.4f, 3f, 2.2f);
        Set(a, "dashDistance", dash);
        return a;
    }

    static float EventGap(MonsterImporter.Meta meta, string clip, string from, string to)
    {
        var c = meta?.clips?.FirstOrDefault(x => x.name == clip);
        var a = c?.events?.FirstOrDefault(e => e.name == from);
        var b = c?.events?.FirstOrDefault(e => e.name == to);
        return a != null && b != null ? (b.time - a.time) * c.frames / 30f : 0.67f;
    }

    /// Sets serialized fields: Set(component, "field", value, "field2", value2, ...). Returns the component.
    static T Set<T>(T target, string field, object value, params object[] more) where T : Object
    {
        Set((Object)target, field, value);
        for (int i = 0; i + 1 < more.Length; i += 2)
            Set((Object)target, (string)more[i], more[i + 1]);
        return target;
    }

    static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"[MonsterSetup] {target.GetType().Name} has no field {field}");
            return;
        }
        switch (value)
        {
            case float f: p.floatValue = f; break;
            case int i when p.propertyType == SerializedPropertyType.Enum: p.enumValueIndex = i; break;
            case int i: p.intValue = i; break;
            case Vector2 v: p.vector2Value = v; break;
            case Vector3 v3: p.vector3Value = v3; break;
            case bool b: p.boolValue = b; break;
            case string s: p.stringValue = s; break;
            case Object o: p.objectReferenceValue = o; break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetStrings(Object target, string field, params string[] values)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).stringValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static AnimationClip[] Clips(string fbx) =>
        AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();

    static AnimatorController BuildController(string path, AnimationClip[] clips)
    {
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        var idleClip = clips.FirstOrDefault(c => c.name == "Idle");
        var idle = sm.AddState("Idle", new Vector3(300f, 0f, 0f));
        idle.motion = idleClip;
        sm.defaultState = idle;
        int row = 1;
        foreach (var clip in clips.Where(c => c != idleClip))
        {
            var st = sm.AddState(clip.name, new Vector3(550f, row++ * 70f - 140f, 0f));
            st.motion = clip;
            if (clip.isLooping)
                continue;                  // loops (Charge, Spin) are left by the attack code
            var back = st.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.hasFixedDuration = true;
            back.duration = 0.15f;
        }
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static Material BodyMaterial(Spec spec, string dir, Look look, float H) => Mat($"{spec.display}_Body", "Roguelike/Ghostly", m =>
    {
        Texture2D Tex(string kind) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/Textures/{spec.name}_{kind}.png");
        m.SetTexture("_BaseMap", Tex("albedo"));
        m.SetTexture("_BumpMap", Tex("normal"));
        m.SetTexture("_MetallicGlossMap", Tex("metallicSmoothness"));
        m.SetColor("_BaseColor", look.tint);
        m.SetFloat("_Opacity", 0.975f);
        m.SetFloat("_Size", H);
        m.SetFloat("_BreatheAmount", look.breathe);
        m.SetFloat("_WarpAmount", look.warp);
        m.SetFloat("_HemSway", look.hem);
        m.SetColor("_RimColor", look.rim);
        m.SetColor("_GlowColor", look.glow);
        m.SetFloat("_GlowThreshold", look.glowThreshold);
    });

    static Material ShellMaterial(Spec spec, Look look, float H) => Mat($"{spec.display}_Smoke", "Roguelike/GhostSmoke", m =>
    {
        m.SetColor("_SmokeColor", look.smoke);
        m.SetColor("_SmokeDark", look.smokeDark);
        m.SetFloat("_Opacity", look.shellOpacity);
        m.SetFloat("_Inflate", look.inflate);
        m.SetFloat("_Threshold", look.threshold);
        m.SetFloat("_BottomBoost", look.bottom);
        m.SetFloat("_TopBoost", look.top);
        m.SetFloat("_FlowSpeed", look.flow);
        m.SetFloat("_Size", H);
        m.SetFloat("_BreatheAmount", look.breathe);
        m.SetFloat("_WarpAmount", look.warp * 1.4f);
        m.SetFloat("_HemSway", look.hem);
    });

    static GameObject BuildMonster(Spec spec, Ctx ctx)
    {
        string dir = $"Assets/Monsters/{spec.folder}";
        string fbx = $"{dir}/{spec.name}.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (model == null)
        {
            Debug.LogError($"[MonsterSetup] {fbx} missing — run Tools/make_monsters.py");
            return null;
        }
        var meta = MonsterImporter.LoadMeta(fbx);
        ctx.meta = meta;
        // the FBX is imported at MonsterScale.Factor (MonsterImporter): the sizes in the clips.json are unscaled
        float H = meta.height * S, W = meta.size * S, hover = meta.hover * S;
        var look = spec.look;

        var clips = Clips(fbx);
        var controller = BuildController($"{dir}/{spec.name}.controller", clips);
        var smokeMat = AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedDir}/SmokeParticle.mat");
        var moteMat = AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedDir}/Mote.mat");

        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.name = spec.display;
        var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (animator.avatar == null || !animator.avatar.isValid)
            Debug.LogError($"[MonsterSetup] {spec.display}: generic avatar missing");

        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
        smr.sharedMaterials = new[] { BodyMaterial(spec, dir, look, H), ShellMaterial(spec, look, H) };
        smr.updateWhenOffscreen = true;           // the jump/teleport carry the mesh far from its bind bounds
        smr.shadowCastingMode = ShadowCastingMode.On;

        var body = go.AddComponent<CapsuleCollider>();   // hurtbox, and keeps the player out of the monster
        float hgt = H - hover;
        body.radius = Mathf.Min(W * 0.3f, hgt * 0.5f);
        body.height = Mathf.Max(hgt, body.radius * 2f);
        body.center = new Vector3(0f, hover + hgt * 0.5f, 0f);

        go.AddComponent<Health>();
        Set(go.GetComponent<Health>(), "maxHealth", spec.health);
        var monster = go.AddComponent<Monster>();
        Set(monster, "size", H);
        Set(monster, "moveSpeed", spec.speed);
        Set(monster, "aggroRange", spec.aggro);
        Set(monster, "deathEffect", ctx.puff);
        go.AddComponent<GhostlyLook>();
        spec.attacks(go, monster, ctx);
        foreach (var shot in go.GetComponents<ProjectileAttack>())
        {
            var bolt = (Projectile)new SerializedObject(shot).FindProperty("projectile").objectReferenceValue;
            if (bolt && ctx.flashes.TryGetValue(bolt, out var flash)) Set(shot, "muzzleFlash", flash);
        }
        spec.brain?.Invoke(go, ctx);
        MetricScale(monster);
        foreach (var c in go.GetComponents<MonsterAttack>()) MetricScale(c);
        foreach (var c in go.GetComponents<MonsterBrain>()) MetricScale(c);
        // game side: what it drops when killed (bosses always try to drop a power-up)
        Set(go.AddComponent<MonsterDrops>(), "powerUpDropChance", Classes[spec.display].boss ? 1f : 0.08f);

        AddWisps(go, smr, look, H, smokeMat);
        if (look.trail) AddTrail(go, look, H, hover, smokeMat);
        if (look.groundFog) AddGroundFog(go, look, H, W, smokeMat);
        if (look.motes > 0) AddMotes(go, look, H, W, moteMat);

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{spec.display}.prefab");
        Object.DestroyImmediate(go);
        Debug.Log($"[MonsterSetup] {spec.display}: height {H:0.00} m, width {W:0.00} m, clips {string.Join(", ", clips.Select(c => $"{c.name}({c.length:0.00}s{(c.isLooping ? " loop" : "")}, {c.events.Length} ev)"))}");
        return prefab;
    }

    // ------------------------------------------------------------------------------------------ catalog

    /// Assets/Monsters/Resources/MonsterCatalog.asset: every monster prefab with its size class and boss flag.
    static void BuildCatalog(List<GameObject> prefabs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
        var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<MonsterCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.SetEntries(prefabs.Select(p =>
        {
            var c = Classes[p.name];
            var boss = p.GetComponent<BossBrain>();
            return new MonsterCatalog.Entry
            {
                name = p.name,
                prefab = p,
                sizeClass = c.cls,
                relativeSize = c.rel,
                height = p.GetComponent<Monster>().Size,
                isBoss = c.boss,
                title = boss ? boss.Title : "",
                maxHealth = p.GetComponent<Health>().Max,
            };
        }));
        EditorUtility.SetDirty(catalog);
    }

    // ------------------------------------------------------------------------------------------ scenes

    /// MonsterTest: a lab room (about the size of a game room) with a few pillars to hide behind and charge
    /// into, every monster spread round it (the bosses at the far end), and the game's player.
    static void BuildScene(List<GameObject> prefabs, Ctx ctx)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        BuildRoom(18, true);
        var player = GamePlayer(ctx, new Vector3(0f, 0f, -14f));
        var small = prefabs.Where(p => !IsBoss(p)).ToList();
        var bosses = prefabs.Where(IsBoss).ToList();
        for (int i = 0; i < small.Count; i++)
        {
            float a = -150f + 300f * i / Mathf.Max(1, small.Count - 1);
            Place(small[i], Quaternion.Euler(0f, a, 0f) * Vector3.back * 8f + Vector3.forward * 1f, player.transform.position);
        }
        for (int i = 0; i < bosses.Count; i++)
            Place(bosses[i], new Vector3((i - (bosses.Count - 1) * 0.5f) * 10f, 0f, 12.5f - Mathf.Abs(i - (bosses.Count - 1) * 0.5f) * 1.5f),
                  player.transform.position);
        FinishScene(scene, ScenePath, player, new Vector3(0f, 14f, -12f));
    }

    static bool IsBoss(GameObject prefab) => Classes.TryGetValue(prefab.name, out var c) && c.boss;

    static void Place(GameObject prefab, Vector3 at, Vector3 lookAt)
    {
        var m = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        m.transform.position = at;
        var look = Monster.Flat(lookAt - at);
        if (look.sqrMagnitude > 1e-4f) m.transform.rotation = Quaternion.LookRotation(look);
    }

    /// The game's player: a copy of the PlayerMovement object of SampleScene (model, shooting, PlayerHealth),
    /// with the MonsterPlayerHealth bridge already on it. Falls back to the cylinder TestPlayer.
    static GameObject GamePlayer(Ctx ctx, Vector3 at)
    {
        GameObject copy = null;
        if (File.Exists(GameScenePath))
        {
            var active = EditorSceneManager.GetActiveScene();
            var game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            var source = game.GetRootGameObjects().Select(g => g.GetComponentInChildren<PlayerMovement>(true)).FirstOrDefault(p => p);
            if (source)
            {
                copy = Object.Instantiate(source.gameObject);
                copy.name = source.gameObject.name;
                EditorSceneManager.MoveGameObjectToScene(copy, active);
            }
            EditorSceneManager.CloseScene(game, true);
            EditorSceneManager.SetActiveScene(active);
        }
        if (copy == null)
        {
            Debug.LogWarning("[MonsterSetup] no PlayerMovement in SampleScene: using the cylinder TestPlayer");
            return TestPlayerObject(ctx, at);
        }
        copy.transform.position = at;
        copy.transform.rotation = Quaternion.identity;
        if (!copy.GetComponent<PlayerHealth>()) copy.AddComponent<PlayerHealth>();
        if (!copy.GetComponent<MonsterPlayerHealth>()) copy.AddComponent<MonsterPlayerHealth>();
        return copy;
    }

    static GameObject TestPlayerObject(Ctx ctx, Vector3 at)
    {
        var go = new GameObject("Player") { tag = "Player" };
        go.transform.position = at;
        var cc = go.AddComponent<CharacterController>();
        cc.center = new Vector3(0f, 0.5f, 0f) * MonsterScale.PlayerSize;
        cc.height = MonsterScale.PlayerSize;
        cc.radius = 0.23f * MonsterScale.PlayerSize;
        Set(go.AddComponent<Health>(), "maxHealth", 100f);
        var bodyMat = Mat("TestPlayer", "Universal Render Pipeline/Lit", m => m.SetColor("_BaseColor", new Color(0.2f, 0.75f, 0.8f)));
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.name = "Body";
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = cc.center;
        body.transform.localScale = new Vector3(cc.radius * 2f, cc.height * 0.5f, cc.radius * 2f);
        body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;
        var tp = go.AddComponent<TestPlayer>();
        Set(tp, "body", body.GetComponent<MeshRenderer>(), "swingEffect", ctx.puff);
        return go;
    }

    /// Floor of lab tiles (the game's L1 assets, 4 m like the game rooms) over a 2*half m square with a collider,
    /// walls all round (the muscle ones' charge stops against them), optionally four pillars.
    static void BuildRoom(int half, bool pillars)
    {
        const float tile = 4f;
        var floors = new[] { "Plain", "Plain", "Plain", "Plain", "Grate", "HazardEdge", "HazardGrate" }
            .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"{LabDir}/Lab_Floor_{n}.fbx"))
            .Where(f => f != null).ToArray();
        var floorRoot = new GameObject("Floor").transform;
        var rng = new System.Random(11);
        if (floors.Length > 0)
        {
            var b = Bounds(floors[0]);
            float k = tile / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            for (float x = -half; x < half - 0.01f; x += tile)
            for (float z = -half; z < half - 0.01f; z += tile)
            {
                var t = ((GameObject)PrefabUtility.InstantiatePrefab(floors[rng.Next(floors.Length)], floorRoot)).transform;
                t.localScale = Vector3.one * k;
                t.position = new Vector3(x + tile * 0.5f, 0f, z + tile * 0.5f) - new Vector3(b.center.x, b.max.y, b.center.z) * k;
            }
        }
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "FloorCollider";
        ground.transform.position = new Vector3(0f, -0.15f, 0f);
        ground.transform.localScale = new Vector3(half * 2f, 0.3f, half * 2f);
        if (floors.Length > 0) Object.DestroyImmediate(ground.GetComponent<MeshRenderer>());

        var wallModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{LabDir}/Lab_Wall.fbx");
        var walls = new GameObject("Walls").transform;
        const float wallHeight = 4f;
        for (int side = 0; side < 4; side++)
        {
            var rot = Quaternion.Euler(0f, side * 90f, 0f);
            var col = GameObject.CreatePrimitive(PrimitiveType.Cube);
            col.name = $"WallCollider{side}";
            col.transform.SetParent(walls, false);
            col.transform.position = rot * new Vector3(0f, wallHeight * 0.5f, half + 0.25f);
            col.transform.rotation = rot;
            col.transform.localScale = new Vector3(half * 2f + 1f, wallHeight, 0.5f);
            if (wallModel)
            {
                Object.DestroyImmediate(col.GetComponent<MeshRenderer>());
                var b = Bounds(wallModel);
                float width = Mathf.Max(0.5f, Mathf.Max(b.size.x, b.size.z));
                float k = tile / width;
                for (float a = -half + tile * 0.5f; a < half; a += tile)
                {
                    var w = (GameObject)PrefabUtility.InstantiatePrefab(wallModel, walls);
                    w.transform.localScale = Vector3.one * k;
                    w.transform.rotation = rot;
                    w.transform.position = rot * new Vector3(a, 0f, half);
                }
            }
        }
        if (!pillars)
            return;
        var stone = Mat("Pillar", "Universal Render Pipeline/Lit", m => m.SetColor("_BaseColor", new Color(0.32f, 0.31f, 0.36f)));
        foreach (var p in new[] { new Vector3(-5f, 0f, -9f), new Vector3(5f, 0f, -9f), new Vector3(-13f, 0f, 2f), new Vector3(13f, 0f, 2f) })
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Pillar";
            pillar.transform.SetParent(walls, false);
            pillar.transform.position = p + Vector3.up * wallHeight * 0.5f;
            pillar.transform.localScale = new Vector3(2f, wallHeight, 2f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = stone;
        }
    }

    static Bounds Bounds(GameObject model)
    {
        var probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
        probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        probe.transform.localScale = Vector3.one;
        var renderers = probe.GetComponentsInChildren<Renderer>();
        var bounds = renderers.Length > 0
            ? renderers.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; })
            : new Bounds(Vector3.zero, Vector3.one);
        Object.DestroyImmediate(probe);
        return bounds;
    }

    /// HUD, the game's follow camera, light, save, and add the scene to the build (after the game's scenes).
    static void FinishScene(UnityEngine.SceneManagement.Scene scene, string path, GameObject player, Vector3 offset)
    {
        var hud = new GameObject("MonsterTestHud").AddComponent<MonsterTestHud>();
        if (player) Set(hud, "player", player.GetComponent<Health>());

        var cam = Camera.main;
        cam.transform.position = (player ? player.transform.position : Vector3.zero) + offset;
        cam.transform.rotation = Quaternion.LookRotation(-offset);
        cam.fieldOfView = 50f;
        var follow = cam.gameObject.AddComponent<CameraFollow>();
        if (player) Set(follow, "target", player.transform);
        Set(follow, "offset", offset);

        var sun = Object.FindAnyObjectByType<Light>();
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.6f, 0.68f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.4f, 0.46f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.23f, 0.27f);

        EditorSceneManager.SaveScene(scene, path);
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
