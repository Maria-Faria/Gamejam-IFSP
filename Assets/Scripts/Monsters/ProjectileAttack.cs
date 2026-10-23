using System.Collections;
using UnityEngine;

namespace Roguelike
{
    /// Fires projectiles from socket bones on every "Fire" event of its clip: a bolt from the hand (regular,
    /// claw), a pair from the hand (horned), a fan of three from between the hands (3-eyed), a boomerang (crescent), a lobbed smoke
    /// bomb (skull face), or a ring of bolts flung outward from the claws while spinning (skull face).
    public class ProjectileAttack : MonsterAttack
    {
        public enum Aim { AtTarget, Lob, Outward }

        [SerializeField] Projectile projectile;
        [Tooltip("Burst played at the socket on every shot.")]
        [SerializeField] GameObject muzzleFlash;
        [Tooltip("Bones the projectiles leave from, e.g. Muzzle.R, Mouth.")]
        [SerializeField] string[] sockets = { "Muzzle.R" };
        [SerializeField] Aim aim = Aim.AtTarget;
        [SerializeField] float speed = 9f;
        [Tooltip("Lob: gravity (m/s²) and splash radius at the landing point.")]
        [SerializeField] float gravity = 14f;
        [SerializeField] float splash = 1.2f;
        [Tooltip("Bolts per shot from each socket, fanned out fanAngle degrees apart round the aim.")]
        [SerializeField] int count = 1;
        [SerializeField] float fanAngle = 12f;
        [SerializeField] float projectileScale = 1f;
        [Tooltip("Fire one volley from the middle of the sockets instead of one from each.")]
        [SerializeField] bool fromMiddle;
        [Tooltip("Straight shots level out at this height above the monster's feet (m), so bolts from up high still reach the player. 0 = keep the aim.")]
        [SerializeField] float cruiseHeight;
        [Tooltip("How fast the monster keeps turning to the target while the clip plays (x its turn speed).")]
        [SerializeField] float trackTarget = 0.6f;

        public Aim AimMode => aim;

        public override IEnumerator Perform() => PlayAndTrack(state, aim == Aim.Outward ? 0f : trackTarget);

        public override void OnAnimationEvent(string evt)
        {
            if (evt != "Fire" || projectile == null)
                return;
            var origins = new System.Collections.Generic.List<Vector3>();
            foreach (var s in sockets)
            {
                var socket = M.Bone(s);
                origins.Add(socket ? socket.position : M.Center + transform.forward * M.Size * 0.3f);
            }
            if (fromMiddle && origins.Count > 1)
            {
                var mid = Vector3.zero;
                foreach (var o in origins) mid += o;
                origins = new System.Collections.Generic.List<Vector3> { mid / origins.Count };
            }
            foreach (var from in origins)
            {
                if (muzzleFlash)
                {
                    var fx = Instantiate(muzzleFlash, from, transform.rotation);
                    fx.transform.localScale = Vector3.one * projectileScale;
                    Destroy(fx, 1.5f);
                }
                switch (aim)
                {
                    case Aim.Lob:
                    {
                        var to = TargetPoint;
                        float time = Mathf.Clamp(Vector3.Distance(from, to) / speed, 0.45f, 1.4f);
                        var v = Projectile.BallisticVelocity(from, to, time, gravity);
                        Projectile.Spawn(projectile, from, v, damage, transform, gravity, splash, projectileScale);
                        break;
                    }
                    case Aim.Outward:
                    {
                        var dir = Monster.Flat(from - M.Center);
                        if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;
                        FireFan(from, dir.normalized);
                        break;
                    }
                    default:
                    {
                        var to = TargetPoint + Vector3.up * TargetHeight(M.Target);
                        var dir = to - from;
                        dir.y = Mathf.Clamp(dir.y, -0.3f * dir.magnitude, 0.3f * dir.magnitude);
                        FireFan(from, dir.normalized);
                        break;
                    }
                }
            }
        }

        void FireFan(Vector3 from, Vector3 dir)
        {
            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                float a = (i - (count - 1) * 0.5f) * fanAngle;
                var p = Projectile.Spawn(projectile, from, Quaternion.AngleAxis(a, Vector3.up) * dir * speed, damage,
                                         transform, 0f, 0f, projectileScale);
                if (cruiseHeight > 0f) p.Cruise(transform.position.y + cruiseHeight);
            }
        }
    }
}
