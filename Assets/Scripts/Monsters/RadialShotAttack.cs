using System.Collections;
using UnityEngine;

namespace Roguelike
{
    /// A ring of projectiles flying out evenly all round on every "Fire" event of its clip: the muscle
    /// boss's ground slams (four, north/east/south/west, once per slam) and the claw's eight-way burst.
    public class RadialShotAttack : MonsterAttack
    {
        [SerializeField] Projectile projectile;
        [SerializeField] int directions = 8;
        [Tooltip("Line the ring up with the world axes (north, east, ...) instead of the way the monster faces.")]
        [SerializeField] bool worldAligned = true;
        [SerializeField] float speed = 6f;
        [SerializeField] float projectileScale = 1f;
        [Tooltip("Height above the monster's feet the shots fly at (m).")]
        [SerializeField] float height = 0.6f;
        [Tooltip("They start this far out from the monster's middle (x its body radius).")]
        [SerializeField] float startOut = 0.8f;
        [SerializeField] GameObject muzzleFlash;
        [Tooltip("How fast the monster keeps turning to the target while the clip plays (x its turn speed).")]
        [SerializeField] float trackTarget = 0.5f;

        /// Projectiles fired by the last run, for tests.
        public int Fired { get; private set; }
        public int Directions => directions;

        public override IEnumerator Perform()
        {
            Fired = 0;
            return PlayAndTrack(state, trackTarget);
        }

        public override void OnAnimationEvent(string evt)
        {
            if (evt != "Fire" || projectile == null)
                return;
            Ring(transform.position, transform.eulerAngles.y);
        }

        /// Fires one ring around `at` (feet level); used by other attacks too (the boss's wall impact).
        public void Ring(Vector3 at, float facing)
        {
            var centre = at + Vector3.up * height;
            if (muzzleFlash)
            {
                var fx = Instantiate(muzzleFlash, centre, Quaternion.identity);
                fx.transform.localScale = Vector3.one * 1.5f * MonsterScale.Factor;
                Destroy(fx, 1.5f);
            }
            float start = worldAligned ? 0f : facing;
            for (int i = 0; i < directions; i++)
            {
                var dir = Quaternion.Euler(0f, start + i * 360f / directions, 0f) * Vector3.forward;
                Projectile.Spawn(projectile, centre + dir * M.Radius * startOut, dir * speed, damage, transform, 0f, 0f, projectileScale)
                          .Cruise(centre.y);
                Fired++;
            }
        }
    }
}
