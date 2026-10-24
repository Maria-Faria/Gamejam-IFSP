using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Roguelike.Tests
{
#if UNITY_EDITOR
    /// The playable scientists and the economy around them, in the game scene: each of the seven rigged
    /// models becomes the player when chosen (8-direction run, pistol raised to shoot, recoil), each has its
    /// own attributes and crystals raise a random one, everyone starts with only the pistol and buys the
    /// special weapons in the one shop room of every floor with the coins monsters drop, monsters also drop
    /// hearts that heal, barrels explode when shot, and shots shove monsters back. Game scripts live in
    /// Assembly-CSharp: reached by reflection.
    public class CharacterGameplayTests
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        static readonly string[] Keys = { "Enedina", "Hertha", "Maria", "Chien", "Patricia", "Annie", "Bibha" };

        static System.Type T(string name) =>
            System.Type.GetType($"{name}, Assembly-CSharp") ?? System.Type.GetType($"GuardiasDaLuz.MapSystem.{name}, Assembly-CSharp", true);

        static object Get(object o, string member)
        {
            var t = o as System.Type ?? o.GetType();
            var target = o is System.Type ? null : o;
            var p = t.GetProperty(member, Any);
            if (p != null) return p.GetValue(target);
            var f = t.GetField(member, Any);
            Assert.IsNotNull(f, $"{t.Name}.{member} not found");
            return f.GetValue(target);
        }

        static void Set(object o, string member, object value)
        {
            var t = o.GetType();
            var p = t.GetProperty(member, Any);
            if (p != null && p.CanWrite) { p.SetValue(o, value); return; }
            var f = t.GetField(member, Any);
            Assert.IsNotNull(f, $"{t.Name}.{member} not found");
            f.SetValue(o, value);
        }

        static object Call(object o, string method, params object[] args)
        {
            var t = o as System.Type ?? o.GetType();
            var target = o is System.Type ? null : o;
            var m = t.GetMethods(Any).First(x => x.Name == method && x.GetParameters().Length == args.Length);
            return m.Invoke(target, args);
        }

        static object Weapon(string name) => System.Enum.Parse(T("WeaponType"), name);

        Scene _game;
        int _savedChoice;
        readonly List<Object> _spawned = new List<Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savedChoice = PlayerPrefs.GetInt("personagem_escolhido", 0);
            Application.targetFrameRate = 60;
            yield break;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.SetInt("personagem_escolhido", _savedChoice);
            Application.targetFrameRate = -1;
            foreach (var o in _spawned) if (o) Object.Destroy(o);
            _spawned.Clear();
            foreach (var loot in Object.FindObjectsByType(T("LootPickup"))) Object.Destroy(((Component)loot).gameObject);
            if (!_game.IsValid() || !_game.isLoaded) yield break;
            var empty = SceneManager.CreateScene("DepoisDasCientistas" + Random.Range(0, 99999));
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(_game);
        }

        IEnumerator LoadGame(int character)
        {
            PlayerPrefs.SetInt("personagem_escolhido", character);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            _game = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(_game);
            for (float end = Time.time + 1f; Time.time < end;) yield return null;
            // nothing should interfere: the monster director stays off unless a test turns it on
            var director = Object.FindAnyObjectByType(T("MonsterDirector")) as Behaviour;
            if (director) director.enabled = false;
        }

        static object Map() => T("StandaloneLighthouseMapSystem").GetProperty("Instance").GetValue(null);

        static IEnumerable<object> Rooms(object map)
        {
            foreach (var kv in (IEnumerable)Get(map, "DungeonGrid"))
                yield return kv.GetType().GetProperty("Value").GetValue(kv);
        }

        static Component Player()
        {
            var player = Object.FindAnyObjectByType(T("PlayerMovement")) as Component;
            Assert.IsNotNull(player, "the game scene has a player");
            var bridge = player.GetComponent<Health>() ?? (Health)player.gameObject.AddComponent(T("MonsterPlayerHealth"));
            bridge.Invulnerable = true;
            return player;
        }

        static void Teleport(Component player, Vector3 to)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = to;
            Physics.SyncTransforms();
            cc.enabled = true;
        }

        /// An empty, lit arena far from the map: a floor and nothing else.
        GameObject Arena(Vector3 at)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = at + Vector3.down * 0.5f;
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            _spawned.Add(floor);
            return floor;
        }

        Monster SpawnMonster(string name, Vector3 at, Component player)
        {
            var m = MonsterCatalog.Load().Spawn(name, at, player.transform);
            _spawned.Add(m.gameObject);
            return m;
        }

        // ---------------------------------------------------------------------------------- 1. characters

        [Test]
        public void EveryScientistHasARiggedAnimatedPrefabWithThePistolInHand([ValueSource(nameof(Keys))] string key)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Characters/Resources/Characters/{key}.prefab");
            Assert.IsNotNull(prefab, $"{key}: run Roguelike/Build Characters");
            var rig = prefab.GetComponent(T("CharacterRig"));
            Assert.IsNotNull(rig);
            Assert.AreEqual(MonsterScale.PlayerSize, (float)Get(rig, "Height"), 0.02f, "as tall as the game's player");
            var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(smr, "a skinned (rigged) mesh");
            Assert.Greater(smr.bones.Length, 20, "a full skeleton");
            Assert.IsNotNull(smr.sharedMaterial.GetTexture("_BaseMap"), "textured");
            var animator = (Animator)Get(rig, "Animator");
            var ctrl = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
            var clips = ctrl.animationClips.Select(c => c.name).Distinct().ToArray();
            foreach (var clip in new[] { "Idle", "Run_F", "Run_B", "Run_L", "Run_R", "Run_FL", "Run_FR", "Run_BL", "Run_BR", "Aim" })
                Assert.Contains(clip, clips, $"{key}: clip {clip}");
            Assert.AreEqual(2, ctrl.layers.Length, "run + aim layers");
            var socket = (Transform)Get(rig, "GunSocket");
            Assert.AreEqual("RightHand", socket.parent.name, "the pistol is held in the right hand");
            Assert.IsNotNull(socket.GetComponentInChildren<MeshRenderer>(), "the pistol model");
            Assert.IsNotNull(Get(rig, "Muzzle"));
        }

        [UnityTest]
        public IEnumerator ChoosingAScientistMakesHerThePlayer([ValueSource(nameof(Keys))] string key)
        {
            int index = System.Array.IndexOf(Keys, key);
            yield return LoadGame(index);
            var player = Player();
            var model = player.transform.Find("Model");
            Assert.IsNotNull(model);
            var rig = model.GetComponent(T("CharacterRig"));
            Assert.IsNotNull(rig, "the rigged model replaces the scene's static one");
            Assert.IsTrue(model.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.name.ToLowerInvariant().Contains(key.ToLowerInvariant()),
                          $"{key}'s own mesh");
            var stats = player.GetComponent(T("CharacterStats"));
            Assert.AreEqual(index, (int)Get(stats, "CharacterIndex"));
            var entry = Call(T("CharacterRoster"), "Get", index);
            var expected = Get(entry, "Stats");
            Assert.AreEqual((float)Get(expected, "maxHealth"), (float)Get(player.GetComponent(T("PlayerHealth")), "MaxHealth"), 0.01f,
                            "her own life");
        }

        [UnityTest]
        public IEnumerator SheRunsInEightDirectionsWhileAimingAtAMonster()
        {
            yield return LoadGame(5);
            var player = Player();
            var at = new Vector3(400f, 0.05f, 0f);
            Arena(at);
            Teleport(player, at);
            player.transform.rotation = Quaternion.identity;
            var monster = SpawnMonster("Regular", at + new Vector3(0f, 0f, 12f), player);
            monster.GetComponent<MonsterBrain>().enabled = false;
            monster.Health.Invulnerable = true;
            var rig = player.transform.Find("Model").GetComponent(T("CharacterRig"));
            var shooting = player.GetComponent(T("PlayerShooting"));
            var movement = player.GetComponent(T("PlayerMovement"));
            Set(shooting, "SimulatedFire", true);
            var directions = new Dictionary<string, Vector2>
            {
                { "F", new Vector2(0, 1) }, { "B", new Vector2(0, -1) }, { "L", new Vector2(-1, 0) }, { "R", new Vector2(1, 0) },
                { "FL", new Vector2(-1, 1).normalized }, { "FR", new Vector2(1, 1).normalized },
                { "BL", new Vector2(-1, -1).normalized }, { "BR", new Vector2(1, -1).normalized },
            };
            foreach (var kv in directions)
            {
                Teleport(player, at);
                monster.transform.position = at + new Vector3(0f, 0f, 12f);
                Set(movement, "SimulatedInput", kv.Value);
                for (float end = Time.time + 0.45f; Time.time < end;) yield return null;
                var move = (Vector2)Get(rig, "Move");
                Assert.Greater(Vector3.Dot(player.transform.forward, Vector3.forward), 0.9f, $"{kv.Key}: she keeps facing the monster");
                Assert.Greater(Vector2.Dot(move.normalized, kv.Value), 0.85f, $"{kv.Key}: the run blend follows her direction (got {move})");
                Assert.Greater(move.magnitude, 0.5f, $"{kv.Key}: running");
            }
            Set(movement, "SimulatedInput", Vector2.zero);
            Assert.Greater((float)Get(rig, "AimWeight"), 0.9f, "pistol raised while shooting");
            Set(shooting, "SimulatedFire", false);
        }

        [UnityTest]
        public IEnumerator ShootingRaisesThePistolKicksTheGunAndShovesTheMonster()
        {
            yield return LoadGame(3);
            var player = Player();
            var at = new Vector3(400f, 0.05f, 0f);
            Arena(at);
            Teleport(player, at);
            player.transform.rotation = Quaternion.identity;
            var monster = SpawnMonster("Ghoul", at + new Vector3(0f, 0f, 5f), player);
            monster.GetComponent<MonsterBrain>().enabled = false;
            monster.Health.Invulnerable = true;
            var rig = player.transform.Find("Model").GetComponent(T("CharacterRig"));
            var shooting = player.GetComponent(T("PlayerShooting"));
            float start = monster.transform.position.z;
            float maxRecoil = 0f, maxPush = 0f;
            Set(shooting, "SimulatedFire", true);
            for (float end = Time.time + 1.2f; Time.time < end;)
            {
                maxRecoil = Mathf.Max(maxRecoil, (float)Get(rig, "Recoil"));
                maxPush = Mathf.Max(maxPush, monster.PushVelocity.magnitude);
                yield return null;
            }
            Set(shooting, "SimulatedFire", false);
            Assert.Greater((float)Get(rig, "AimWeight"), 0.8f, "the arm comes up with the pistol");
            Assert.Greater(maxRecoil, 0.3f, "each shot kicks the gun");
            Assert.Greater(maxPush, 0.5f, "hits shove the monster");
            Assert.Greater(monster.transform.position.z, start + 0.3f, "pushed back, away from her");
            var muzzle = (Transform)Get(rig, "Muzzle");
            var shotOrigin = (Transform)Get(shooting, "Muzzle");
            Assert.Less(Vector3.Distance(muzzle.position, shotOrigin.position), 0.6f, "the shots leave from the pistol");
        }

        // ---------------------------------------------------------------------------------- 2. barrels

        [UnityTest]
        public IEnumerator ShootingABarrelBlowsItUpWithLightAndDamage()
        {
            yield return LoadGame(0);
            var player = Player();
            var at = new Vector3(400f, 0.05f, 0f);
            Arena(at);
            Teleport(player, at);
            player.transform.rotation = Quaternion.identity;
            var barrelModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Decor/barrel.fbx");
            var barrel = (GameObject)Call(T("RoomDecorKit"), "SpawnProp", barrelModel, null, at + new Vector3(1.5f, 0f, 7f), Quaternion.identity, true);
            _spawned.Add(barrel);
            Assert.IsNotNull(barrel.GetComponent(T("ExplosiveBarrel")), "decor barrels are explosive");
            var monster = SpawnMonster("Ghost", at + new Vector3(3.2f, 0f, 7.5f), player);
            monster.GetComponent<MonsterBrain>().enabled = false;
            float hp = monster.Health.Current;
            var light = (Light)null;
            // shoot it: a bullet straight at the barrel
            var bulletPrefab = (GameObject)Get(player.GetComponent(T("PlayerShooting")), "BulletPrefab");
            var target = barrel.transform.position + Vector3.up * 0.6f;
            var from = at + new Vector3(1.5f, 0.6f, 2f);
            Object.Instantiate(bulletPrefab, from, Quaternion.LookRotation(target - from));
            for (float end = Time.time + 1.5f; Time.time < end && barrel; ) yield return null;
            Assert.IsTrue(barrel == null, "the barrel is gone");
            light = Object.FindObjectsByType<Light>().FirstOrDefault(l => l.name == "Clarao_Barril");
            Assert.IsNotNull(light, "it lights up the room");
            Assert.Greater(light.intensity, 1f);
            Assert.Less(monster.Health.Current, hp, "and hurts the monsters near it");
        }

        // ---------------------------------------------------------------------------------- 3. attributes

        [Test]
        public void EveryScientistHasHerOwnAttributes()
        {
            var all = ((System.Array)Get(T("CharacterRoster"), "All")).Cast<object>().ToArray();
            Assert.AreEqual(7, all.Length);
            var fields = new[] { "maxHealth", "moveSpeed", "fireRate", "damage", "maxMana", "manaRegen" };
            var profiles = all.Select(e => string.Join("|", fields.Select(f => Get(Get(e, "Stats"), f).ToString()))).ToArray();
            Assert.AreEqual(7, profiles.Distinct().Count(), "seven different profiles");
            foreach (var f in new[] { "maxHealth", "moveSpeed", "fireRate", "damage" })
            {
                var values = all.Select(e => (float)Get(Get(e, "Stats"), f)).ToArray();
                Assert.Greater(values.Max(), values.Min() * 1.15f, $"{f} varies between them");
            }
        }

        [UnityTest]
        public IEnumerator FasterScientistsRunFasterAndShootFaster()
        {
            yield return LoadGame(6);       // Bibha: the fastest
            var player = Player();
            float bibha = (float)Get(player.GetComponent(T("PlayerMovement")), "CurrentSpeed");
            Call(player.GetComponent(T("PlayerCharacter")), "Use", 0);       // Enedina: slow and strong
            yield return null;
            float enedina = (float)Get(player.GetComponent(T("PlayerMovement")), "CurrentSpeed");
            Assert.Greater(bibha, enedina * 1.25f, "speed differs");
            var stats = player.GetComponent(T("CharacterStats"));
            Assert.Greater((float)Get(stats, "Damage"), 1.1f, "Enedina hits harder");
            Assert.AreEqual(130f, (float)Get(player.GetComponent(T("PlayerHealth")), "MaxHealth"), 0.01f);
        }

        [UnityTest]
        public IEnumerator EachCrystalEnhancesARandomAttribute()
        {
            yield return LoadGame(2);
            var player = Player();
            var stats = player.GetComponent(T("CharacterStats"));
            var powerUps = player.GetComponent(T("PlayerPowerUps"));
            string Snapshot() => string.Join("|", new[] { "MaxHealth", "MoveSpeed", "FireRate", "Damage", "MaxMana" }.Select(m => Get(stats, m).ToString()));
            var types = System.Enum.GetValues(T("PowerUpType")).Cast<object>().Where(t => t.ToString() != "Mana").ToArray();
            var raised = new HashSet<string>();
            for (int i = 0; i < 40; i++)
            {
                string before = Snapshot();
                Call(powerUps, "Collect", types[i % types.Length]);
                string after = Snapshot();
                Assert.AreNotEqual(before, after, "a crystal always improves something");
                var b = before.Split('|'); var a = after.Split('|');
                Assert.AreEqual(1, Enumerable.Range(0, b.Length).Count(k => b[k] != a[k]), "exactly one attribute");
                raised.Add(Enumerable.Range(0, b.Length).First(k => b[k] != a[k]).ToString());
                yield return null;
            }
            Assert.GreaterOrEqual(raised.Count, 4, "the attribute is drawn at random");
        }

        // ---------------------------------------------------------------------------------- 4. pistol, coins, shop

        [UnityTest]
        public IEnumerator EveryoneStartsWithJustThePistol()
        {
            yield return LoadGame(1);
            var shooting = Player().GetComponent(T("PlayerShooting"));
            Assert.AreEqual("Pistol", Get(shooting, "CurrentWeapon").ToString());
            foreach (var w in System.Enum.GetValues(T("WeaponType")))
                Assert.AreEqual(w.ToString() == "Pistol", (bool)Call(shooting, "Owns", w), $"{w}");
            Call(shooting, "SetWeapon", Weapon("Shotgun"));
            Assert.AreEqual("Pistol", Get(shooting, "CurrentWeapon").ToString(), "special weapons must be bought first");
        }

        [UnityTest]
        public IEnumerator EveryFloorHasExactlyOneShop()
        {
            yield return LoadGame(0);
            var map = Map();
            for (int floor = 0; floor < 6; floor++)
            {
                Call(map, "GenerateFloor", floor);
                yield return null;
                var shops = Rooms(map).Where(r => Get(r, "roomType").ToString() == "Shop").ToArray();
                Assert.AreEqual(1, shops.Length, $"floor {floor + 1}: one shop");
                var root = (GameObject)Get(shops[0], "roomRoot");
                var stands = root.GetComponentsInChildren(T("WeaponStand"));
                Assert.AreEqual(6, stands.Length, "the six special weapons on sale");
            }
        }

        [UnityTest]
        public IEnumerator MonstersDropCoinsThatBuyWeaponsInTheShop()
        {
            yield return LoadGame(4);
            var player = Player();
            var at = new Vector3(400f, 0.05f, 0f);
            Arena(at);
            Teleport(player, at);
            var wallet = player.GetComponent(T("PlayerWallet"));
            Assert.AreEqual(0, (int)Get(wallet, "Coins"));
            // a monster dies next to her: coins pop out and fly to her
            for (int i = 0; i < 6; i++)
            {
                var m = SpawnMonster("Ghoul", at + new Vector3(2f, 0f, 2f), player);
                m.GetComponent<MonsterBrain>().enabled = false;
                m.Health.TakeDamage(99999f, m.transform.position);
                yield return null;
            }
            var coins = Object.FindObjectsByType(T("LootPickup")).Where(p => Get(p, "Type").ToString() == "Coin").ToArray();
            Assert.GreaterOrEqual(coins.Length, 6, "every monster drops coins");
            for (float end = Time.time + 3f; Time.time < end && Object.FindObjectsByType(T("LootPickup")).Any(p => Get(p, "Type").ToString() == "Coin");)
                yield return null;
            int earned = (int)Get(wallet, "Coins");
            Assert.GreaterOrEqual(earned, 12, "picked up (2 coins per medium monster)");

            // the shop: not enough for the explosive launcher, enough for the triple shot
            var map = Map();
            var shop = Rooms(map).First(r => Get(r, "roomType").ToString() == "Shop");
            var stands = ((GameObject)Get(shop, "roomRoot")).GetComponentsInChildren(T("WeaponStand"));
            var shooting = player.GetComponent(T("PlayerShooting"));
            Component Stand(string w) => stands.First(s => Get(Get(s, "Offer"), "Weapon").ToString() == w);
            Assert.IsFalse((bool)Call(Stand("Exploding"), "Buy", shooting, wallet), "90 coins: too expensive yet");
            Call(wallet, "Add", 30);
            int before = (int)Get(wallet, "Coins");
            Assert.IsTrue((bool)Call(Stand("AimedTriple"), "Buy", shooting, wallet));
            Assert.AreEqual(before - 30, (int)Get(wallet, "Coins"), "paid 30 coins");
            Assert.IsTrue((bool)Call(shooting, "Owns", Weapon("AimedTriple")));
            Assert.AreEqual("AimedTriple", Get(shooting, "CurrentWeapon").ToString(), "in her hand right away");
            Assert.IsFalse((bool)Call(Stand("AimedTriple"), "Buy", shooting, wallet), "bought once");
        }

        [UnityTest]
        public IEnumerator WalkingUpToAStandAndPressingEBuys()
        {
            yield return LoadGame(5);
            var player = Player();
            var map = Map();
            var shop = Rooms(map).First(r => Get(r, "roomType").ToString() == "Shop");
            var stand = ((GameObject)Get(shop, "roomRoot")).GetComponentsInChildren(T("WeaponStand"))
                .First(s => Get(Get(s, "Offer"), "Weapon").ToString() == "RapidFire");
            Call(player.GetComponent(T("PlayerWallet")), "Add", 100);
            Teleport(player, stand.transform.position + new Vector3(0f, 0.05f, -1.4f));
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(((string)Get(map, "hoverPromptBanner")).Contains("[E]"), "a prompt to buy appears");
        }

        // ---------------------------------------------------------------------------------- 5. hearts

        [UnityTest]
        public IEnumerator HeartsFromMonstersRestoreLife()
        {
            yield return LoadGame(0);
            var player = Player();
            var at = new Vector3(400f, 0.05f, 0f);
            Arena(at);
            Teleport(player, at);
            var health = player.GetComponent(T("PlayerHealth"));
            Call(health, "TakeDamage", 60f);
            float hurt = (float)Get(health, "Health");
            // a boss always drops one (and a bag of coins)
            var boss = SpawnMonster("MuscleBoss", at + new Vector3(0f, 0f, 3f), player);
            boss.GetComponent<MonsterBrain>().enabled = false;
            boss.Health.TakeDamage(999999f, boss.transform.position);
            yield return null;
            var hearts = Object.FindObjectsByType(T("LootPickup")).Where(p => Get(p, "Type").ToString() == "Heart").ToArray();
            Assert.AreEqual(1, hearts.Length, "the boss drops a heart");
            Assert.IsTrue(((Component)hearts[0]).GetComponentInChildren<Renderer>() != null, "heart.fbx model");
            for (float end = Time.time + 3f; Time.time < end && (float)Get(health, "Health") <= hurt;) yield return null;
            Assert.Greater((float)Get(health, "Health"), hurt + 20f, "picking it up heals her");

            // ordinary monsters drop them sometimes
            int dropped = 0;
            var drops = SpawnMonster("Ghost", at + new Vector3(30f, 0f, 30f), player).GetComponent(T("MonsterDrops"));
            for (int i = 0; i < 200; i++)
            {
                Call(drops, "OnDied");
                dropped = Object.FindObjectsByType(T("LootPickup")).Count(p => Get(p, "Type").ToString() == "Heart");
            }
            Assert.That(dropped, Is.InRange(8, 50), "about 12% of monsters drop a heart");
        }

        // ---------------------------------------------------------------------------------- screenshots

        static void Shot(string dir, string name, Vector3 from, Vector3 look, float fov = 35f, int size = 640)
        {
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.transform.position = from;
            cam.transform.LookAt(look);
            cam.fieldOfView = fov;
            var rt = new RenderTexture(size, size, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(cam.gameObject);
            rt.Release();
        }

        [UnityTest, Explicit("writes TestScreenshots/Characters/*.png")]
        public IEnumerator CaptureCloseUps()
        {
            var dir = System.IO.Path.Combine(Application.dataPath, "..", "TestScreenshots", "Characters");
            System.IO.Directory.CreateDirectory(dir);
            yield return LoadGame(5);
            var player = Player();
            var at = new Vector3(400f, 0.05f, 0f);
            Arena(at);
            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            _spawned.Add(sun.gameObject);
            Teleport(player, at);
            player.transform.rotation = Quaternion.identity;
            var shooting = player.GetComponent(T("PlayerShooting"));
            var movement = player.GetComponent(T("PlayerMovement"));
            var head = player.transform.position + Vector3.up * 1.1f;
            for (int i = 0; i < 10; i++) yield return null;
            Shot(dir, "close_idle_front", at + new Vector3(0.8f, 1.6f, 3.2f), head);
            var monster = SpawnMonster("Regular", at + new Vector3(0f, 0f, 9f), player);
            monster.GetComponent<MonsterBrain>().enabled = false;
            monster.Health.Invulnerable = true;
            Set(shooting, "SimulatedFire", true);
            for (float end = Time.time + 0.6f; Time.time < end;) yield return null;
            Shot(dir, "close_aim_side", player.transform.position + new Vector3(3.2f, 1.4f, 0.6f), player.transform.position + Vector3.up * 1f);
            Shot(dir, "close_aim_front", player.transform.position + new Vector3(-1.2f, 1.5f, 3.4f), player.transform.position + Vector3.up * 1f);
            foreach (var kv in new Dictionary<string, Vector2> { { "back", new Vector2(0, -1) }, { "left", new Vector2(-1, 0) }, { "diag", new Vector2(0.7f, 0.7f) } })
            {
                Teleport(player, at);
                Set(movement, "SimulatedInput", kv.Value);
                for (float end = Time.time + 0.33f; Time.time < end;) yield return null;
                Shot(dir, $"close_run_{kv.Key}", player.transform.position + new Vector3(3.6f, 1.6f, 1.6f), player.transform.position + Vector3.up * 0.9f);
            }
            Set(movement, "SimulatedInput", Vector2.zero);
            Set(shooting, "SimulatedFire", false);
            // loot
            for (int i = 0; i < 3; i++)
            {
                var m = SpawnMonster("Ghoul", at + new Vector3(-6f, 0f, -6f), player);
                m.Health.TakeDamage(99999f, m.transform.position);
            }
            var b = SpawnMonster("Fat", at + new Vector3(-9f, 0f, -3f), player);
            b.Health.TakeDamage(999999f, b.transform.position);
            for (float end = Time.time + 1.2f; Time.time < end;) yield return null;
            Shot(dir, "loot", at + new Vector3(-4f, 3f, -1f), at + new Vector3(-7.5f, 0.3f, -5f), 45f);
            // the shop room
            var map = Map();
            var shop = Rooms(map).First(r => Get(r, "roomType").ToString() == "Shop");
            var center = (Vector3)Get(shop, "worldCenter");
            Teleport(player, center + new Vector3(0f, 0.05f, -6f));
            for (int i = 0; i < 20; i++) yield return null;
            Shot(dir, "shop", center + new Vector3(0f, 7f, -11f), center + new Vector3(0f, 1f, 2f), 55f, 800);
            // a barrel going up
            var barrelModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Decor/barrel.fbx");
            var barrel = (GameObject)Call(T("RoomDecorKit"), "SpawnProp", barrelModel, null, at + new Vector3(6f, 0f, -6f), Quaternion.identity, true);
            Call(barrel.GetComponent(T("ExplosiveBarrel")), "Explode");
            for (int i = 0; i < 4; i++) yield return null;
            Shot(dir, "barrel", at + new Vector3(2f, 3f, -1f), at + new Vector3(6f, 0.8f, -6f), 50f);
        }

        [UnityTest]
        public IEnumerator TheSelectionMenuShowsTheScientistsIn3D()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Menu.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            _game = SceneManager.GetSceneByPath("Assets/Scenes/Menu.unity");
            SceneManager.SetActiveScene(_game);
            yield return null;
            var menu = Object.FindAnyObjectByType(T("MainMenu"));
            Assert.IsNotNull(menu);
            Call(menu, "Activate", 0);              // "Jogar" opens the character selection
            for (int i = 0; i < 10; i++) yield return new WaitForEndOfFrame();
            var stage = Object.FindAnyObjectByType(T("CharacterPreviewStage"));
            Assert.IsNotNull(stage, "the 3D stage of the carousel");
            var rigs = ((Component)stage).GetComponentsInChildren(T("CharacterRig"));
            Assert.AreEqual(7, rigs.Length, "the seven rigged scientists");
            var texture = (Texture)Call(stage, "TextureFor", 5);
            Assert.IsNotNull(texture, "each one rendered for the menu");
            var cams = ((Component)stage).GetComponentsInChildren<Camera>();
            Assert.IsTrue(cams.Any(c => c.enabled), "rendering while the carousel is open");
            var dir = System.IO.Path.Combine(Application.dataPath, "..", "TestScreenshots", "Characters");
            System.IO.Directory.CreateDirectory(dir);
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "menu_selection.png"), shot.EncodeToPNG());
            Object.Destroy(shot);
        }

        [UnityTest, Explicit("writes TestScreenshots/Characters/*.png")]
        public IEnumerator CaptureCharactersInGame()
        {
            var dir = System.IO.Path.Combine(Application.dataPath, "..", "TestScreenshots", "Characters");
            System.IO.Directory.CreateDirectory(dir);
            for (int i = 0; i < Keys.Length; i++)
            {
                yield return LoadGame(i);
                var player = Player();
                var at = new Vector3(400f, 0.05f, 0f);
                Arena(at);
                Teleport(player, at);
                player.transform.rotation = Quaternion.identity;
                var monster = SpawnMonster("Regular", at + new Vector3(0f, 0f, 9f), player);
                monster.GetComponent<MonsterBrain>().enabled = false;
                monster.Health.Invulnerable = true;
                var shooting = player.GetComponent(T("PlayerShooting"));
                Set(shooting, "SimulatedFire", true);
                Set(player.GetComponent(T("PlayerMovement")), "SimulatedInput", new Vector2(0.7f, -0.7f));
                for (float end = Time.time + 0.7f; Time.time < end;) yield return null;
                var cam = new GameObject("Cam").AddComponent<Camera>();
                cam.transform.position = at + new Vector3(3.2f, 2.4f, 3.5f);
                cam.transform.LookAt(player.transform.position + Vector3.up * 1f);
                cam.fieldOfView = 40f;
                var rt = new RenderTexture(640, 640, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(640, 640, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"{Keys[i]}_run_shoot.png"), tex.EncodeToPNG());
                Object.Destroy(cam.gameObject);
                Set(shooting, "SimulatedFire", false);
                yield return TearDown();
            }
        }
    }
#endif
}
