using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Roguelike
{
    /// Every monster prefab of the game, written by Roguelike/Build Monsters (MonsterSetup) to
    /// Assets/Monsters/Resources/MonsterCatalog.asset. Use it to pick and spawn monsters:
    ///
    ///     var catalog = MonsterCatalog.Load();
    ///     Monster m = catalog.Spawn("Ghoul", position);              // target: the game's player
    ///     m.Died += dead => ...;                                     // once, when it is killed
    ///     foreach (var e in catalog.Regulars) ...                     // or catalog.Bosses / catalog.Entries
    ///
    /// A spawned monster finds the player itself (MonsterTargeting.Player), notices it within its aggro range
    /// and in sight, and attacks it; its attacks hurt the player through the player's PlayerHealth.
    [CreateAssetMenu(menuName = "Roguelike/Monster Catalog", fileName = "MonsterCatalog")]
    public class MonsterCatalog : ScriptableObject
    {
        public const string ResourceName = "MonsterCatalog";

        public enum SizeClass
        {
            Tiny,       // 0.5x the player: ball_shadow, twisted_ball
            Small,      // 0.75x: regular, 3_eyed, ghost, claw
            Medium,     // 1x: crescent, ghoul, horned, muscle
            Boss,       // 1.7-2x: fat, muscle_boss, skull_face
        }

        [System.Serializable]
        public class Entry
        {
            [Tooltip("Prefab name, e.g. Ghoul, SkullFace.")]
            public string name;
            public GameObject prefab;
            public SizeClass sizeClass;
            [Tooltip("Size relative to the player (0.5, 0.75, 1, 1.7, 1.8, 2).")]
            public float relativeSize = 1f;
            [Tooltip("Height of the monster in the game (m).")]
            public float height;
            public bool isBoss;
            [Tooltip("Boss title shown above its health bar (bosses only).")]
            public string title;
            public float maxHealth;

            public Monster Monster => prefab ? prefab.GetComponent<Monster>() : null;

            /// Instantiates this monster standing at `position` (its feet), facing `facing` (or towards its target).
            public Monster Spawn(Vector3 position, Transform target = null, Quaternion? facing = null, Transform parent = null)
            {
                if (prefab == null)
                    return null;
                if (target == null)
                    target = MonsterTargeting.Player();
                var rotation = facing ?? LookAt(position, target);
                var go = Instantiate(prefab, position, rotation, parent);
                go.name = name;
                var m = go.GetComponent<Monster>();
                if (m && target) m.Target = target;
                return m;
            }

            static Quaternion LookAt(Vector3 from, Transform target)
            {
                if (target == null) return Quaternion.identity;
                var to = Monster.Flat(target.position - from);
                return to.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(to) : Quaternion.identity;
            }
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;
        public IEnumerable<Entry> Bosses => entries.Where(e => e.isBoss);
        public IEnumerable<Entry> Regulars => entries.Where(e => !e.isBoss);

        public Entry Find(string monsterName) => entries.FirstOrDefault(e => e.name == monsterName);

        public Monster Spawn(string monsterName, Vector3 position, Transform target = null, Quaternion? facing = null, Transform parent = null)
        {
            var e = Find(monsterName);
            if (e == null)
            {
                Debug.LogError($"[MonsterCatalog] no monster named {monsterName}");
                return null;
            }
            return e.Spawn(position, target, facing, parent);
        }

        static MonsterCatalog _loaded;

        /// The catalog built by Roguelike/Build Monsters (from Resources).
        public static MonsterCatalog Load()
        {
            if (_loaded == null) _loaded = Resources.Load<MonsterCatalog>(ResourceName);
            return _loaded;
        }

#if UNITY_EDITOR
        public void SetEntries(IEnumerable<Entry> list)
        {
            entries = list.ToList();
        }
#endif
    }
}
