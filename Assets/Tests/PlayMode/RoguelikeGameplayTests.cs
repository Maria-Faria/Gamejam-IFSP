using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Roguelike.Tests
{
    /// The roguelike layer of the game: the floor plans of MonsterDirector (3-5 types a floor, a boss on every
    /// second floor in the order MuscleBoss, Fat, SkullFace, everything harder floor by floor), the monster
    /// variants and what their hits do to the player (PlayerStatus), the phantom ghosts that fade out and back,
    /// and the damage / speed multipliers. Game scripts are in Assembly-CSharp: reached by reflection.
    public class RoguelikeGameplayTests : MonsterTestBase
    {
        static System.Type GameType(string name) => System.Type.GetType($"{name}, Assembly-CSharp", true);

        static object Get(object o, string member)
        {
            var t = o as System.Type ?? o.GetType();
            var target = o is System.Type ? null : o;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            var p = t.GetProperty(member, flags);
            if (p != null) return p.GetValue(target);
            var f = t.GetField(member, flags);
            Assert.IsNotNull(f, $"{t.Name}.{member} not found");
            return f.GetValue(target);
        }

        static object[] Plans() => ((System.Array)GameType("MonsterDirector").GetMethod("DefaultPlans").Invoke(null, null)).Cast<object>().ToArray();

        /// The game's player with its PlayerHealth and PlayerStatus; monster damage does not lower its HP (the
        /// bridge is invulnerable) so long tests cannot kill it, but on-hit effects still apply.
        Component GamePlayer(Vector3 at)
        {
            var go = new GameObject("Player");
            go.transform.position = at;
            var cc = go.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.5f;
            _spawned.Add(go);
            go.AddComponent(GameType("PlayerHealth"));
            var bridge = (Health)go.AddComponent(GameType("MonsterPlayerHealth"));
            bridge.Invulnerable = true;
            return go.AddComponent(GameType("PlayerStatus"));
        }

        [Test]
        public void EveryFloorUsesThreeToFiveKnownTypes()
        {
            var catalog = MonsterCatalog.Load();
            var plans = Plans();
            Assert.AreEqual(6, plans.Length, "3 levels x 2 floors");
            var baseName = GameType("MonsterDirector").GetMethod("BaseName");
            foreach (var plan in plans)
            {
                var types = (string[])Get(plan, "types");
                Assert.That(types.Length, Is.InRange(3, 5), $"{Get(plan, "label")}: 3 to 5 monster types");
                Assert.AreEqual(types.Length, types.Distinct().Count());
                foreach (var t in types)
                {
                    var entry = catalog.Find((string)baseName.Invoke(null, new object[] { t }));
                    Assert.IsNotNull(entry, $"{t} is not in the catalog");
                    Assert.IsFalse(entry.isBoss, $"{t}: bosses only in the boss rooms");
                }
            }
        }

        [Test]
        public void BossesComeOnEverySecondFloorInOrder()
        {
            var plans = Plans();
            string[] expected = { null, "MuscleBoss", null, "Fat", null, "SkullFace" };
            for (int i = 0; i < plans.Length; i++)
            {
                var boss = (string)Get(plans[i], "boss");
                Assert.AreEqual(expected[i], string.IsNullOrEmpty(boss) ? null : boss, $"floor {Get(plans[i], "label")}");
                if (expected[i] != null) Assert.IsTrue(MonsterCatalog.Load().Find(boss).isBoss);
            }
        }

        [Test]
        public void FloorsGetHarder()
        {
            var plans = Plans();
            for (int i = 1; i < plans.Length; i++)
            {
                foreach (var stat in new[] { "health", "damage", "speed", "variantChance" })
                    Assert.GreaterOrEqual((float)Get(plans[i], stat), (float)Get(plans[i - 1], stat), $"{stat} on floor {i + 1}");
                Assert.LessOrEqual((float)Get(plans[i], "cooldown"), (float)Get(plans[i - 1], "cooldown"), $"cooldown on floor {i + 1}");
                Assert.GreaterOrEqual(((Vector2Int)Get(plans[i], "perWave")).y, ((Vector2Int)Get(plans[i - 1], "perWave")).y);
            }
            Assert.Greater((float)Get(plans[5], "damage"), (float)Get(plans[0], "damage") * 1.8f, "3-2 hits much harder than 1-1");
            Assert.Greater((float)Get(plans[5], "speed"), (float)Get(plans[0], "speed"), "and is faster");
            var phantoms = plans.SelectMany(p => (string[])Get(p, "types")).Where(t => t.EndsWith("Fantasma")).Distinct();
            CollectionAssert.AreEquivalent(new[] { "GhostFantasma", "GhoulFantasma" }, phantoms, "phasing ghost and ghoul appear");
        }

        [UnityTest]
        public IEnumerator DamageScaleMultipliesEveryHit()
        {
            Floor();
            var player = GamePlayer(new Vector3(0f, 0f, 2.5f * S));
            var m = MonsterCatalog.Load().Spawn("Ghoul", Vector3.zero, player.transform);
            _spawned.Add(m.gameObject);
            m.DamageScale = 2f;
            float dealt = -1f;
            System.Action<Health, float, Monster> hit = (victim, amount, source) => { if (source == m) dealt = amount; };
            Health.HitByMonster += hit;
            try
            {
                for (float end = Time.time + 12f; Time.time < end && dealt < 0f;)
                    yield return null;
            }
            finally
            {
                Health.HitByMonster -= hit;
            }
            Assert.Greater(dealt, 0f, "the ghoul should hit the player");
            var damages = m.Attacks.Select(a => a.Damage * 2f).ToArray();
            Assert.IsTrue(damages.Any(d => Mathf.Abs(d - dealt) < 1e-3f), $"hit for {dealt}, expected one of {string.Join(", ", damages)}");
        }

        [UnityTest]
        public IEnumerator VariantHitsAffectThePlayer([Values("Venenoso", "Atordoante", "Gelido", "Brutamontes")] string variant)
        {
            Floor();
            var status = GamePlayer(new Vector3(0f, 0f, 2.5f * S));
            var applied = new List<string>();
            var evt = status.GetType().GetEvent("StatusApplied");
            System.Action<string> onStatus = s => applied.Add(s);
            evt.AddEventHandler(status, onStatus);

            var m = MonsterCatalog.Load().Spawn("Ghoul", Vector3.zero, status.transform);
            _spawned.Add(m.gameObject);
            var type = GameType("MonsterVariantType");
            GameType("MonsterVariant").GetMethod("Apply").Invoke(null, new object[] { m, System.Enum.Parse(type, variant), 1f });

            string expected = variant == "Venenoso" ? "poison" : variant == "Atordoante" ? "stun" : variant == "Gelido" ? "slow" : "knockback";
            for (float end = Time.time + 14f; Time.time < end && !applied.Contains(expected);)
                yield return null;
            Assert.Contains(expected, applied, $"a {variant} ghoul's hit should apply {expected}");
            if (variant == "Brutamontes")
                Assert.AreEqual(1.3f, m.transform.localScale.x / LoadPrefab("Ghoul").transform.localScale.x, 1e-3f, "brutes are bigger");
        }

        [UnityTest]
        public IEnumerator FastVariantMovesQuickerWithLessHealth()
        {
            Floor();
            var normal = MonsterCatalog.Load().Spawn("Regular", new Vector3(-3f, 0f, 0f));
            var fast = MonsterCatalog.Load().Spawn("Regular", new Vector3(3f, 0f, 0f));
            _spawned.Add(normal.gameObject);
            _spawned.Add(fast.gameObject);
            yield return null;
            GameType("MonsterVariant").GetMethod("Apply").Invoke(null, new object[] { fast, System.Enum.Parse(GameType("MonsterVariantType"), "Veloz"), 1f });
            Assert.AreEqual(1.45f, fast.MoveSpeed / normal.MoveSpeed, 1e-3f);
            Assert.Less(fast.CooldownScale, 1f);
            Assert.AreEqual(0.8f, fast.Health.Max / normal.Health.Max, 1e-3f);
        }

        [UnityTest]
        public IEnumerator PhantomGhostFadesOutUntouchableAndComesBack([Values("Ghost", "Ghoul")] string name)
        {
            Floor();
            var player = GamePlayer(new Vector3(0f, 0f, 6f * S));
            var hp = player.GetComponent(GameType("PlayerHealth"));
            var m = MonsterCatalog.Load().Spawn(name, Vector3.zero, player.transform);
            _spawned.Add(m.gameObject);
            var phase = m.gameObject.AddComponent(GameType("MonsterPhaseShift"));
            yield return null;
            var body = m.GetComponentInChildren<Collider>();
            var findHealth = GameType("MonsterGameBridge").GetMethod("FindMonsterHealth");
            Assert.IsNotNull(findHealth.Invoke(null, new object[] { body }), "visible: the player's weapons find it");

            phase.GetType().GetMethod("PhaseNow").Invoke(phase, null);
            for (float end = Time.time + 3f; Time.time < end && !m.Untargetable;)
                yield return null;
            Assert.IsTrue(m.Untargetable, "it should fade away");
            yield return new WaitForSeconds(0.45f);
            Assert.IsNull(findHealth.Invoke(null, new object[] { body }), "while gone the weapons ignore it");
            Assert.IsFalse(body.enabled, "and bullets pass through");
            float before = m.Health.Current;
            m.Health.TakeDamage(50f, m.transform.position);
            Assert.AreEqual(before, m.Health.Current, "and it cannot be hurt");
            Assert.Less((float)Get(phase, "Opacity"), 0.05f, "it is (almost) invisible");

            int hits = 0;
            System.Action<Health, float, Monster> onHit = (v, a, s) => { if (s == m && m.Untargetable) hits++; };
            Health.HitByMonster += onHit;
            try
            {
                for (float end = Time.time + 4f; Time.time < end && m.Untargetable;)
                    yield return null;
            }
            finally
            {
                Health.HitByMonster -= onHit;
            }
            Assert.IsFalse(m.Untargetable, "it should come back");
            Assert.AreEqual(0, hits, "it never attacks while gone");
            Assert.IsTrue(body.enabled);
            Assert.IsNotNull(findHealth.Invoke(null, new object[] { body }), "back: it can be shot again");
            float dist = Monster.Flat(m.transform.position - player.transform.position).magnitude;
            Assert.Less(dist, 5.5f, "it reappears close to the player");
        }
    }

#if UNITY_EDITOR
    /// MonsterDirector inside SampleScene: entering a combat room spawns the floor's monsters into it (counted by
    /// the room), and the boss rooms of floors 1-2, 2-2 and 3-2 hold MuscleBoss, Fat and SkullFace.
    public class DirectorSceneTests
    {
        static System.Type GameType(string name) => System.Type.GetType($"{name}, Assembly-CSharp", true);
        static readonly System.Type MapType = System.Type.GetType("GuardiasDaLuz.MapSystem.StandaloneLighthouseMapSystem, Assembly-CSharp", true);

        Scene _game;

        IEnumerator LoadGame()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            _game = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
            SceneManager.SetActiveScene(_game);
            for (float end = Time.time + 1.5f; Time.time < end;)
                yield return null;
        }

        static object Map() => MapType.GetProperty("Instance").GetValue(null);

        static IEnumerable<object> Rooms(object map)
        {
            foreach (var kv in (IEnumerable)MapType.GetProperty("DungeonGrid").GetValue(map))
                yield return kv.GetType().GetProperty("Value").GetValue(kv);
        }

        static object Field(object o, string name) => o.GetType().GetField(name).GetValue(o);

        Component Player()
        {
            var player = Object.FindAnyObjectByType(GameType("PlayerMovement")) as Component;
            Assert.IsNotNull(player);
            var bridge = player.GetComponent<Health>() ?? (Health)player.gameObject.AddComponent(GameType("MonsterPlayerHealth"));
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

        [UnityTest]
        public IEnumerator EnteringACombatRoomSpawnsTheFloorsMonsters()
        {
            yield return LoadGame();
            var map = Map();
            var player = Player();
            var room = Rooms(map).First(r => Field(r, "roomType").ToString() == "Combat");
            var root = (GameObject)Field(room, "roomRoot");
            Teleport(player, (Vector3)Field(room, "worldCenter") + new Vector3(4f, 1f, 3f));

            Monster[] spawned = new Monster[0];
            for (float end = Time.time + 4f; Time.time < end && spawned.Length == 0;)
            {
                yield return null;
                spawned = root.GetComponentsInChildren<Monster>();
            }
            yield return new WaitForSeconds(0.6f);
            spawned = root.GetComponentsInChildren<Monster>();
            Assert.That(spawned.Length, Is.InRange(2, 3), "floor 1-1 sends 2-3 monsters a wave");
            var allowed = new[] { "BallShadow", "Regular", "Ghost" };
            foreach (var m in spawned)
                Assert.Contains(m.name, allowed, "only the floor's types");
            var roomMonsters = (IList)Field(room, "roomMonsters");
            foreach (var m in spawned)
                Assert.IsTrue(roomMonsters.Contains(m.gameObject), "monsters count for the room (doors stay shut)");
            foreach (var m in spawned)
            {
                Assert.AreEqual(player.transform, m.Target);
                Assert.Greater(Monster.Flat(m.transform.position - player.transform.position).magnitude, 5f, "not on top of the player");
            }
            // only our rigged monsters: none of the old untextured shadow enemies, nothing in the room that is not one
            yield return new WaitForSeconds(3f);
            Assert.IsNull(Object.FindAnyObjectByType(System.Type.GetType("EnemySpawner, Assembly-CSharp", true)), "no shadow spawner");
            Assert.IsNull(Object.FindAnyObjectByType(System.Type.GetType("ShadowEnemy, Assembly-CSharp", true)), "no shadow enemies");
            foreach (GameObject g in (IList)Field(room, "roomMonsters"))
                if (g) Assert.IsNotNull(g.GetComponent<Monster>(), $"{g.name} is one of our monsters");
        }

        [UnityTest]
        public IEnumerator BossRoomsHoldTheBossesInOrder()
        {
            yield return LoadGame();
            var map = Map();
            var player = Player();
            var director = Object.FindAnyObjectByType(GameType("MonsterDirector"));
            Assert.IsNotNull(director, "the map adds the director");
            var expected = new Dictionary<int, string> { { 1, "MuscleBoss" }, { 3, "Fat" }, { 5, "SkullFace" } };
            foreach (var kv in expected)
            {
                MapType.GetMethod("GenerateFloor").Invoke(map, new object[] { kv.Key });
                yield return null;
                var room = Rooms(map).First(r => Field(r, "roomType").ToString() == "BossExit");
                Teleport(player, (Vector3)Field(room, "worldCenter") + new Vector3(0f, 1f, -9f));
                Monster boss = null;
                for (float end = Time.time + 5f; Time.time < end && boss == null;)
                {
                    yield return null;
                    boss = (Monster)director.GetType().GetProperty("CurrentBoss").GetValue(director);
                }
                Assert.IsNotNull(boss, $"floor {kv.Key + 1}: a boss should appear");
                Assert.AreEqual(kv.Value, boss.name);
                Assert.Greater(boss.Health.Max, 1000f, "bosses get a long fight");
                Assert.IsTrue(((IList)Field(room, "roomMonsters")).Contains(boss.gameObject), "the boss holds the doors");
            }
        }

        /// Plays each boss fight and a hard 3-2 combat room for a while with an untouchable player: no errors.
        [UnityTest]
        public IEnumerator BossFightsAndHardRoomsRunWithoutErrors()
        {
            yield return LoadGame();
            var map = Map();
            var player = Player();
            foreach (int floor in new[] { 1, 3, 5 })
            {
                MapType.GetMethod("GenerateFloor").Invoke(map, new object[] { floor });
                yield return null;
                var boss = Rooms(map).First(r => Field(r, "roomType").ToString() == "BossExit");
                Teleport(player, (Vector3)Field(boss, "worldCenter") + new Vector3(0f, 1f, -8f));
                for (float end = Time.time + 8f; Time.time < end;)
                    yield return null;
            }
            var room = Rooms(map).First(r => Field(r, "roomType").ToString() == "Combat");
            Teleport(player, (Vector3)Field(room, "worldCenter") + new Vector3(4f, 1f, 3f));
            for (float end = Time.time + 8f; Time.time < end;)
                yield return null;
            Assert.Greater(((GameObject)Field(room, "roomRoot")).GetComponentsInChildren<Monster>().Length, 0, "the 3-2 room fills");
        }

        [UnityTearDown]
        public IEnumerator UnloadGame()
        {
            if (!_game.IsValid() || !_game.isLoaded)
                yield break;
            var empty = SceneManager.CreateScene("AfterDirectorScene" + Random.Range(0, 99999));
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(_game);
        }
    }
#endif
}
