using System.Collections;
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
    /// The monsters inside the game: the catalog, the game's player as their target (PlayerHealth takes their
    /// damage), the game's weapons (WeaponDamage, LightBullet) hurting them, and SampleScene still running.
    /// The game scripts live in Assembly-CSharp, which a test assembly cannot reference: they are reached by
    /// reflection.
    public class MonsterGameTests : MonsterTestBase
    {
        static System.Type GameType(string name) => System.Type.GetType($"{name}, Assembly-CSharp", true);

        static float PlayerHp(Component playerHealth) =>
            (float)playerHealth.GetType().GetProperty("Health").GetValue(playerHealth);

        /// The game's player: the SampleScene Player's CharacterController and a PlayerHealth.
        Component GamePlayer(Vector3 at)
        {
            var go = new GameObject("Player");
            go.transform.position = at;
            var cc = go.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.5f;
            _spawned.Add(go);
            return go.AddComponent(GameType("PlayerHealth"));
        }

        [Test]
        public void CatalogListsEveryMonster()
        {
            var catalog = MonsterCatalog.Load();
            Assert.IsNotNull(catalog, "Resources/MonsterCatalog missing: run Roguelike/Build Monsters");
            Assert.AreEqual(13, catalog.Entries.Count);
            CollectionAssert.AreEquivalent(new[] { "Fat", "MuscleBoss", "SkullFace" }, catalog.Bosses.Select(e => e.name));
            foreach (var e in catalog.Entries)
            {
                Assert.IsNotNull(e.prefab, e.name);
                Assert.IsNotNull(e.Monster, $"{e.name}: no Monster");
                Assert.AreEqual(e.isBoss, e.prefab.GetComponent<BossBrain>() != null, $"{e.name}: boss flag");
                Assert.AreEqual(e.isBoss, e.sizeClass == MonsterCatalog.SizeClass.Boss, $"{e.name}: size class");
                Assert.AreEqual(e.height, e.Monster.Size, 1e-4f);
                Assert.IsNotNull(e.prefab.GetComponent(GameType("MonsterDrops")), $"{e.name}: no MonsterDrops");
            }
        }

#if UNITY_EDITOR
        [Test]
        public void PlayerSizeMatchesTheGamesPlayerModel()
        {
            // annie_easley-compressed.obj, placed at scale 1.63 under the Player in SampleScene
            var mesh = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Scientists/AnnieEasley/annie_easley-compressed.obj")
                .OfType<Mesh>().First();
            float size = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z) * 1.63f;
            Assert.AreEqual(MonsterScale.PlayerSize, size, 0.02f);
        }
#endif

        [UnityTest]
        public IEnumerator MonstersGoForTheGamesPlayerAndHurtItThroughPlayerHealth([Values("Ghoul", "Regular")] string name)
        {
            Floor();
            var player = GamePlayer(new Vector3(0f, 0f, 3.5f * S));
            var m = MonsterCatalog.Load().Spawn(name, Vector3.zero);
            _spawned.Add(m.gameObject);
            yield return null;
            Assert.AreEqual(player.transform, m.Target, "the monster should find the game's player by itself");
            Assert.IsNotNull(player.GetComponent(GameType("MonsterPlayerHealth")), "the player should get the damage bridge");
            float before = PlayerHp(player);
            for (float end = Time.time + 12f; Time.time < end && PlayerHp(player) >= before;)
                yield return null;
            Assert.Less(PlayerHp(player), before, $"{name} should hurt the player's PlayerHealth");
            Object.Destroy(m.gameObject);
        }

        [UnityTest]
        public IEnumerator BossesNoticeAndHurtTheGamesPlayer([Values("Fat", "MuscleBoss", "SkullFace")] string name)
        {
            Floor();
            var player = GamePlayer(new Vector3(0f, 0f, 7f * S));
            var m = MonsterCatalog.Load().Spawn(name, Vector3.zero);
            _spawned.Add(m.gameObject);
            float before = PlayerHp(player);
            for (float end = Time.time + 25f; Time.time < end && PlayerHp(player) >= before;)
                yield return null;
            Assert.IsTrue(m.GetComponent<BossBrain>().InFight, "the fight should have started");
            Assert.Less(PlayerHp(player), before, $"{name} should hurt the player");
        }

        [UnityTest]
        public IEnumerator PlayerWeaponsHurtAndKillMonsters()
        {
            Floor();
            GamePlayer(new Vector3(0f, 0f, -20f * S));
            var m = MonsterCatalog.Load().Spawn("Ghoul", Vector3.zero);
            m.GetComponent<MonsterBrain>().enabled = false;
            int died = 0;
            m.Died += _ => died++;
            yield return null;
            var hp = m.Health;
            var tryDamage = GameType("WeaponDamage").GetMethod("TryDamage", BindingFlags.Public | BindingFlags.Static);
            var body = m.GetComponent<Collider>();
            Assert.IsTrue((bool)tryDamage.Invoke(null, new object[] { body, 10f }), "WeaponDamage should find the monster");
            Assert.AreEqual(hp.Max - 10f, hp.Current, 1e-3f);

#if UNITY_EDITOR
            // a real light bullet, fired at it from 4 m
            var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LightBullet.prefab");
            Assert.IsNotNull(bulletPrefab);
            float hpBefore = hp.Current;
            var bullet = Object.Instantiate(bulletPrefab, m.BodyCenter + Vector3.back * 4f, Quaternion.identity);
            _spawned.Add(bullet);
            for (float end = Time.time + 2f; Time.time < end && hp.Current >= hpBefore;)
                yield return null;
            Assert.Less(hp.Current, hpBefore, "a LightBullet should hurt the monster");
#endif

            tryDamage.Invoke(null, new object[] { body, 10000f });
            Assert.IsTrue(hp.IsDead);
            Assert.AreEqual(1, died, "Monster.Died should fire once");
            Assert.IsFalse((bool)tryDamage.Invoke(null, new object[] { body, 10f }), "a dead monster is no target any more");
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(m == null, "the body should be removed");
        }

    }

#if UNITY_EDITOR
    /// SampleScene (map, spawner, player) still runs with the monster code in, and a monster spawned into it
    /// goes for the player. Errors logged meanwhile fail the test. The scene is unloaded again afterwards.
    public class GameSceneTests
    {
        static System.Type GameType(string name) => System.Type.GetType($"{name}, Assembly-CSharp", true);

        Scene _game;

        [UnityTest]
        public IEnumerator GameSceneRunsWithMonsters()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            _game = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
            SceneManager.SetActiveScene(_game);
            for (float end = Time.time + 2f; Time.time < end;)
                yield return null;
            var player = Object.FindAnyObjectByType(GameType("PlayerMovement")) as Component;
            Assert.IsNotNull(player, "SampleScene has a player");
            Assert.IsNotNull(Object.FindAnyObjectByType(GameType("MonsterDirector")), "the monster director runs the rooms");
            Assert.IsNull(Object.FindAnyObjectByType(GameType("EnemySpawner")), "and the old shadow enemy spawner is gone");
            var at = player.transform.position + Vector3.right * 4f * MonsterScale.Factor;
            var m = MonsterCatalog.Load().Spawn("Regular", at);
            for (int i = 0; i < 5; i++)
                yield return null;
            Assert.AreEqual(player.transform, m.Target, "a monster spawned in the game goes for the player");
            Assert.IsNotNull(player.GetComponent(GameType("MonsterPlayerHealth")));
            for (float end = Time.time + 3f; Time.time < end;)
                yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadGame()
        {
            if (!_game.IsValid() || !_game.isLoaded)
                yield break;
            var empty = SceneManager.CreateScene("AfterGameScene");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(_game);
        }
    }
#endif
}
