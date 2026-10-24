using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds the playable scientists from Tools/make_characters.py's output (Assets/Characters/<Key>):
// a URP Lit material from the exported textures, an animator controller (8-direction run blend tree on
// MoveX/MoveZ with Idle in the middle, plus an "Aim" layer masked to the right arm), the pistol in the right
// hand (a GunSocket child of the hand bone, lined up with the forearm in the Aim pose so the barrel points
// where the arm does) and a prefab per character in Assets/Characters/Resources/Characters, sized to the
// game's player (MonsterScale.PlayerSize tall). Also writes the PickupCatalog (coin, heart, pistol).
// Menu: Roguelike/Build Characters   (batch: -executeMethod CharacterSetup.BuildAll)
public static class CharacterSetup
{
    const string Root = "Assets/Characters";
    const string ResourcesDir = Root + "/Resources";
    const string PrefabDir = ResourcesDir + "/" + CharacterRoster.ResourceFolder;
    const string PistolDir = Root + "/Pistol";
    const string PowerUpDir = "Assets/Powerups";

    static readonly (string clip, Vector2 at)[] Locomotion =
    {
        ("Idle", Vector2.zero),
        ("Run_F", new Vector2(0f, 1f)), ("Run_B", new Vector2(0f, -1f)),
        ("Run_L", new Vector2(-1f, 0f)), ("Run_R", new Vector2(1f, 0f)),
        ("Run_FL", new Vector2(-0.7071f, 0.7071f)), ("Run_FR", new Vector2(0.7071f, 0.7071f)),
        ("Run_BL", new Vector2(-0.7071f, -0.7071f)), ("Run_BR", new Vector2(0.7071f, -0.7071f)),
    };

    [System.Serializable] class PistolMeta { public float length; public float[] muzzle; }

    [MenuItem("Roguelike/Build Characters")]
    public static void BuildAll()
    {
        AssetDatabase.Refresh();
        Directory.CreateDirectory(PrefabDir);
        var pistol = BuildPistol();
        int built = 0;
        foreach (var entry in CharacterRoster.All)
        {
            if (BuildCharacter(entry.Key, pistol.prefab, pistol.muzzle)) built++;
        }
        BuildCatalog(pistol.prefab);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CharacterSetup] built {built} characters");
    }

    static Material LitMaterial(string path, string texDir, string stem, float smoothness)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = shader;
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{stem}_albedo.png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{stem}_normal.png");
        var metal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{stem}_metallicSmoothness.png");
        m.SetTexture("_BaseMap", albedo);
        m.SetColor("_BaseColor", Color.white);
        if (normal != null)
        {
            m.SetTexture("_BumpMap", normal);
            m.EnableKeyword("_NORMALMAP");
        }
        if (metal != null)
        {
            m.SetTexture("_MetallicGlossMap", metal);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        m.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(m);
        return m;
    }

    static (GameObject prefab, Vector3 muzzle) BuildPistol()
    {
        string fbx = $"{PistolDir}/pistol.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (model == null)
        {
            Debug.LogError($"[CharacterSetup] {fbx} missing, run Tools/make_characters.py");
            return (null, Vector3.zero);
        }
        var meta = JsonUtility.FromJson<PistolMeta>(File.ReadAllText($"{PistolDir}/pistol.json"));
        var mat = LitMaterial($"{PistolDir}/pistol.mat", $"{PistolDir}/Textures", "pistol", 0.55f);
        var go = (GameObject)Object.Instantiate(model);
        go.name = "Pistol";
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PistolDir}/Pistol.prefab");
        Object.DestroyImmediate(go);
        var muzzle = meta?.muzzle != null && meta.muzzle.Length == 3 ? new Vector3(meta.muzzle[0], meta.muzzle[1], meta.muzzle[2]) : new Vector3(0f, 0.04f, 0.19f);
        return (prefab, muzzle);
    }

    static AnimationClip[] Clips(string fbx) =>
        AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();

    static Transform Find(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    static bool BuildCharacter(string key, GameObject pistol, Vector3 muzzleOffset)
    {
        string dir = $"{Root}/{key}";
        string stem = key.ToLowerInvariant();
        string fbx = $"{dir}/{stem}.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var meta = CharacterImporter.LoadMeta(fbx);
        if (model == null || meta == null)
        {
            Debug.LogError($"[CharacterSetup] {fbx} missing, run Tools/make_characters.py");
            return false;
        }
        var clips = Clips(fbx);
        var material = LitMaterial($"{dir}/{stem}.mat", $"{dir}/Textures", stem, 0.45f);

        var root = new GameObject(key);
        float scale = Roguelike.MonsterScale.PlayerSize / Mathf.Max(meta.height, 0.1f);
        root.transform.localScale = Vector3.one * scale;
        var body = (GameObject)Object.Instantiate(model, root.transform, false);
        body.name = stem;
        foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            smr.sharedMaterial = material;
            smr.updateWhenOffscreen = true;
        }

        var animator = body.GetComponent<Animator>();
        if (animator == null) animator = body.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.runtimeAnimatorController = BuildController($"{dir}/{stem}.controller", clips, body.transform);

        var hand = Find(body.transform, "RightHand");
        var foreArm = Find(body.transform, "RightForeArm");
        var arm = Find(body.transform, "RightArm");
        Transform socket = null, muzzle = null;
        if (hand != null && foreArm != null)
        {
            // line the pistol up with the forearm as it is in the Aim pose
            var aim = clips.FirstOrDefault(c => c.name == "Aim");
            if (aim != null) aim.SampleAnimation(body, 0f);
            var handEnd = hand.childCount > 0 ? hand.GetChild(0) : null;
            Vector3 along = (hand.position - foreArm.position).normalized;
            float handLength = handEnd != null ? Vector3.Distance(hand.position, handEnd.position) : 0.06f * scale;
            socket = new GameObject("GunSocket").transform;
            socket.SetParent(hand, false);
            socket.SetPositionAndRotation(hand.position + along * handLength * 0.45f + Vector3.down * handLength * 0.15f,
                                          Quaternion.LookRotation(along, Vector3.up));
            socket.localScale = Vector3.one / Mathf.Max(hand.lossyScale.x, 1e-4f) * scale;
            if (pistol != null)
            {
                // the pistol keeps its own import rotation, which turns the barrel to +Z (the socket's forward)
                var gun = (GameObject)PrefabUtility.InstantiatePrefab(pistol, socket);
                gun.transform.localPosition = Vector3.zero;
                gun.transform.localRotation = pistol.transform.localRotation;
                gun.transform.localScale = Vector3.one;
                // muzzle: the tip of the barrel, the farthest point of the gun along the socket's forward
                var mf = gun.GetComponentInChildren<MeshFilter>();
                var b = mf.sharedMesh.bounds;
                float tipZ = float.MinValue, midY = 0f;
                for (int c = 0; c < 8; c++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    var p = socket.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    tipZ = Mathf.Max(tipZ, p.z);
                }
                midY = socket.InverseTransformPoint(mf.transform.TransformPoint(b.center)).y;
                muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(socket, false);
                muzzle.localPosition = new Vector3(0f, midY + b.extents.z * 0.4f, tipZ);
            }
            var idle = clips.FirstOrDefault(c => c.name == "Idle");
            if (idle != null) idle.SampleAnimation(body, 0f);
        }
        else
        {
            Debug.LogError($"[CharacterSetup] {key}: no RightHand/RightForeArm bones");
        }

        var rig = root.AddComponent<CharacterRig>();
        var so = new SerializedObject(rig);
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("rightArm").objectReferenceValue = arm;
        so.FindProperty("gunSocket").objectReferenceValue = socket;
        so.FindProperty("muzzle").objectReferenceValue = muzzle;
        so.FindProperty("height").floatValue = meta.height * scale;
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[CharacterSetup] {key}: {meta.height * scale:0.00} m, {clips.Length} clips, pistol {(muzzle != null ? "in hand" : "MISSING")}");
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{key}.prefab");
        Object.DestroyImmediate(root);
        return true;
    }

    static AnimatorController BuildController(string path, AnimationClip[] clips, Transform body)
    {
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MoveZ", AnimatorControllerParameterType.Float);

        var sm = ctrl.layers[0].stateMachine;
        var tree = new BlendTree
        {
            name = "Locomotion",
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = "MoveX",
            blendParameterY = "MoveZ",
            useAutomaticThresholds = false,
        };
        AssetDatabase.AddObjectToAsset(tree, ctrl);
        foreach (var (clip, at) in Locomotion)
        {
            var c = clips.FirstOrDefault(x => x.name == clip);
            if (c == null) { Debug.LogError($"[CharacterSetup] {path}: clip {clip} missing"); continue; }
            tree.AddChild(c, at);
        }
        var loco = sm.AddState("Locomotion");
        loco.motion = tree;
        sm.defaultState = loco;

        // the right arm raised with the pistol, over the run
        var mask = new AvatarMask();
        mask.name = "BracoDireito";
        var transforms = body.GetComponentsInChildren<Transform>(true).Where(t => t != body).ToArray();
        var shoulder = Find(body, "RightShoulder");
        mask.transformCount = transforms.Length;
        for (int i = 0; i < transforms.Length; i++)
        {
            mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(transforms[i], body));
            mask.SetTransformActive(i, shoulder != null && transforms[i].IsChildOf(shoulder));
        }
        AssetDatabase.AddObjectToAsset(mask, ctrl);
        ctrl.AddLayer(new AnimatorControllerLayer
        {
            name = "Aim",
            defaultWeight = 0f,
            avatarMask = mask,
            blendingMode = AnimatorLayerBlendingMode.Override,
            stateMachine = new AnimatorStateMachine { name = "Aim" },
        });
        var aimLayer = ctrl.layers[1];
        AssetDatabase.AddObjectToAsset(aimLayer.stateMachine, ctrl);
        var aimState = aimLayer.stateMachine.AddState("Aim");
        aimState.motion = clips.FirstOrDefault(x => x.name == "Aim");
        aimLayer.stateMachine.defaultState = aimState;
        var layers = ctrl.layers;
        layers[1] = aimLayer;
        ctrl.layers = layers;
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static void BuildCatalog(GameObject pistol)
    {
        // coin.fbx / heart.fbx get their materials from DecorTextureSetup like the other power-ups
        if (!File.Exists($"{PowerUpDir}/Materials/coin.mat") || !File.Exists($"{PowerUpDir}/Materials/heart.mat"))
            DecorTextureSetup.RunPowerUps();
        string path = $"{ResourcesDir}/{PickupCatalog.ResourceName}.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<PickupCatalog>(path);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<PickupCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
        }
        catalog.coin = AssetDatabase.LoadAssetAtPath<GameObject>($"{PowerUpDir}/coin.fbx");
        catalog.heart = AssetDatabase.LoadAssetAtPath<GameObject>($"{PowerUpDir}/heart.fbx");
        catalog.pistol = pistol;
        EditorUtility.SetDirty(catalog);
        if (catalog.coin == null || catalog.heart == null) Debug.LogError("[CharacterSetup] coin.fbx / heart.fbx missing in Assets/Powerups");
    }
}
