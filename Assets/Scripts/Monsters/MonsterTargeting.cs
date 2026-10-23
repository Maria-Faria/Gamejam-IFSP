using System;
using UnityEngine;

namespace Roguelike
{
    /// How monsters find their target and make it hurtable. The monster code knows nothing about the game's
    /// player: the game glue (MonsterGameBridge, outside this assembly) fills these in at startup — FindPlayer
    /// returns the game's player, PrepareTarget makes sure a target has a Health the monster attacks can hit
    /// (for the game's player: a MonsterPlayerHealth that forwards the damage to PlayerHealth).
    /// Without the glue (tests), a target is whatever is tagged "Player".
    public static class MonsterTargeting
    {
        public static Func<Transform> FindPlayer;
        public static Action<Transform> PrepareTarget;

        /// The player monsters should go after, or null if there is none.
        public static Transform Player()
        {
            var t = FindPlayer?.Invoke();
            if (t == null && GameObject.FindWithTag("Player") is GameObject tagged)
                t = tagged.transform;
            if (t != null) Prepare(t);
            return t;
        }

        public static void Prepare(Transform target)
        {
            if (target != null) PrepareTarget?.Invoke(target);
        }
    }
}
