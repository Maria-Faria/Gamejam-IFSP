using System.Collections;
using UnityEngine;

namespace Roguelike
{
    /// The fat one's spit ("Fire" event of its clip, from the Mouth socket). Each time it is a coin toss:
    /// either a glob lobbed at the target that hatches a shadow ball where it lands, or a shotgun blast of
    /// 6-9 globs sprayed in a cone.
    public class SpitAttack : MonsterAttack
    {
        [SerializeField] string socket = "Mouth";
        [Tooltip("Chance (0..1) of a summoning glob instead of the shotgun blast.")]
        [Range(0f, 1f)] [SerializeField] float summonChance = 0.5f;
        [Tooltip("Lobbed glob that hatches a monster where it lands.")]
        [SerializeField] Projectile summonGlob;
        [SerializeField] float lobGravity = 14f;
        [Header("Shotgun")]
        [SerializeField] Projectile pellet;
        [SerializeField] Vector2Int pellets = new Vector2Int(6, 9);
        [Tooltip("Width of the cone (degrees).")]
        [SerializeField] float spread = 50f;
        [SerializeField] Vector2 pelletSpeed = new Vector2(6f, 9f);
        [SerializeField] float pelletScale = 0.8f;
        [SerializeField] GameObject muzzleFlash;
        [Tooltip("Bullets level out at this height above the floor (m).")]
        [SerializeField] float cruiseHeight = 0.6f;

        /// What the last spit was, for tests and the HUD.
        public bool LastWasSummon { get; private set; }

        public override IEnumerator Perform() => PlayAndTrack(state, 0.6f);

        public override void OnAnimationEvent(string evt)
        {
            if (evt != "Fire")
                return;
            var bone = M.Bone(socket);
            var from = bone ? bone.position : M.Center + transform.forward * M.Size * 0.4f;
            if (muzzleFlash)
            {
                var fx = Instantiate(muzzleFlash, from, transform.rotation);
                fx.transform.localScale = Vector3.one * 1.5f * MonsterScale.Factor;
                Destroy(fx, 1.5f);
            }
            LastWasSummon = summonGlob && Random.value < summonChance;
            if (LastWasSummon)
            {
                var to = TargetPoint;
                float time = Mathf.Clamp(Vector3.Distance(from, to) / (7f * MonsterScale.Factor), 0.5f, 1.3f);
                Projectile.Spawn(summonGlob, from, Projectile.BallisticVelocity(from, to, time, lobGravity), damage, transform, lobGravity);
                return;
            }
            if (pellet == null)
                return;
            var aim = Monster.Flat(TargetPoint - transform.position);
            if (aim.sqrMagnitude < 1e-4f) aim = transform.forward;
            aim.Normalize();
            int n = Random.Range(pellets.x, pellets.y + 1);
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, (i + Random.Range(0.2f, 0.8f)) / n);
                var dir = Quaternion.Euler(0f, a, 0f) * aim;
                Projectile.Spawn(pellet, from, dir * Random.Range(pelletSpeed.x, pelletSpeed.y), damage, transform, 0f, 0f, pelletScale)
                          .Cruise(transform.position.y + cruiseHeight);
            }
        }
    }
}
