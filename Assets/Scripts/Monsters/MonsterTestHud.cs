using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Roguelike
{
    /// Test-scene overlay: the player's health and a log of the hits taken and the attacks monsters start.
    /// Number keys 1-9 make the nearest monster use its attack #n right away. Tab cycles through fighting one
    /// monster at a time (the others are put away) and back to all of them.
    public class MonsterTestHud : MonoBehaviour
    {
        [SerializeField] Health player;

        readonly Queue<string> _log = new Queue<string>();
        readonly Dictionary<Monster, MonsterAttack> _seen = new Dictionary<Monster, MonsterAttack>();
        Monster[] _monsters;
        (Vector3 pos, Quaternion rot)[] _spawns;
        int _solo = -1;

        void Start()
        {
            _monsters = FindObjectsByType<Monster>().OrderBy(m => m.name).ToArray();
            _spawns = _monsters.Select(m => (m.transform.position, m.transform.rotation)).ToArray();
            if (player) player.Damaged += (d, from) => Add($"player hit for {d:0}");
        }

        void Add(string line)
        {
            _log.Enqueue($"{Time.time:0.0}s  {line}");
            while (_log.Count > 12) _log.Dequeue();
        }

        void Update()
        {
            foreach (var m in _monsters)
            {
                if (!m) continue;
                _seen.TryGetValue(m, out var last);
                if (m.CurrentAttack && m.CurrentAttack != last)
                    Add($"{m.name}: {m.CurrentAttack.State}");
                _seen[m] = m.CurrentAttack;
            }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || player == null) return;
            if (kb.tabKey.wasPressedThisFrame)
                Solo(_solo + 1 >= _monsters.Length ? -1 : _solo + 1);
            for (int i = 0; i < 9; i++)
            {
                if (!kb[UnityEngine.InputSystem.Key.Digit1 + i].wasPressedThisFrame) continue;
                var nearest = _monsters.Where(m => m && m.isActiveAndEnabled).OrderBy(m => Vector3.Distance(m.transform.position, player.transform.position)).FirstOrDefault();
                if (nearest && i < nearest.Attacks.Count) nearest.Run(nearest.Attacks[i]);
            }
        }

        void Solo(int index)
        {
            _solo = index;
            for (int i = 0; i < _monsters.Length; i++)
            {
                if (!_monsters[i]) continue;
                bool on = index < 0 || i == index;
                if (on && !_monsters[i].gameObject.activeSelf)
                    _monsters[i].transform.SetPositionAndRotation(_spawns[i].pos, _spawns[i].rot);
                _monsters[i].gameObject.SetActive(on);
            }
            Add(index < 0 ? "all monsters" : $"solo: {(_monsters[index] ? _monsters[index].name : "(dead)")}");
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            style.normal.textColor = Color.white;
            if (player)
                GUI.Label(new Rect(12, 8, 900, 24), $"Player HP {player.Current:0}/{player.Max:0}   (1-9: nearest monster attacks, Tab: one monster at a time)", style);
            int y = 34;
            foreach (var l in _log)
            {
                GUI.Label(new Rect(12, y, 600, 22), l, style);
                y += 20;
            }
        }
    }
}
