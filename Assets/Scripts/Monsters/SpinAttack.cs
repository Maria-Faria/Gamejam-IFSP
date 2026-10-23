using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// The muscle boss's spin: whirls with fists out (Spin loop), drifting after the target and battering
    /// everything in reach, then staggers dizzy (SpinDizzy) — the moment to hit back. While it whirls it
    /// flings bolts out north, east, south and west a few times, a beat apart.
    public class SpinAttack : MonsterAttack
    {
        [SerializeField] string dizzyState = "SpinDizzy";
        [SerializeField] float duration = 3f;
        [SerializeField] float moveSpeed = 2.2f;
        [Tooltip("Reach of the fists (x monster height).")]
        [SerializeField] float radius = 0.65f;
        [SerializeField] float tickInterval = 0.35f;
        [Header("Shots while spinning")]
        [SerializeField] Projectile shot;
        [Tooltip("Volleys per spin, and the seconds into the spin of the first one / between them.")]
        [SerializeField] int volleys = 2;
        [SerializeField] float firstVolley = 0.6f;
        [SerializeField] float volleyInterval = 1f;
        [SerializeField] int directions = 4;
        [SerializeField] float shotSpeed = 6f;
        [SerializeField] float shotDamage = 8f;
        [SerializeField] float shotScale = 1.3f;
        [SerializeField] float shotHeight = 0.6f;

        /// Bolts fired by the last spin, for tests.
        public int Fired { get; private set; }

        public float Duration
        {
            get => duration;
            set => duration = value;
        }

        public override IEnumerator Perform()
        {
            M.Play(state, 0.15f);
            Fired = 0;
            float t = 0f, nextTick = 0f, nextVolley = firstVolley;
            int volleysLeft = shot ? volleys : 0;
            var tickHits = new HashSet<Health>();
            while (t < duration)
            {
                t += Time.deltaTime;
                var to = Monster.Flat(TargetPoint - transform.position);
                if (to.magnitude > M.Size * 0.3f)
                    transform.position += to.normalized * moveSpeed * Time.deltaTime;
                if (volleysLeft > 0 && t >= nextVolley)
                {
                    volleysLeft--;
                    nextVolley += volleyInterval;
                    Volley();
                }
                if (t >= nextTick)
                {
                    nextTick = t + tickInterval;
                    tickHits.Clear();
                    HitSphere(transform.position + Vector3.up * M.Size * 0.45f, radius * M.Size, damage, tickHits);
                }
                yield return null;
            }
            // finish the turn in progress so the dizzy clip starts facing the same way
            var info = M.Animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(state))
            {
                float left = (1f - Mathf.Repeat(info.normalizedTime, 1f)) * info.length;
                yield return new WaitForSeconds(left);
            }
            M.Play(dizzyState, 0.05f);
            yield return M.WaitForState(dizzyState);
        }

        void Volley()
        {
            var centre = transform.position + Vector3.up * shotHeight;
            for (int i = 0; i < directions; i++)
            {
                var dir = Quaternion.Euler(0f, i * 360f / directions, 0f) * Vector3.forward;
                Projectile.Spawn(shot, centre + dir * M.Radius, dir * shotSpeed, shotDamage, transform, 0f, 0f, shotScale)
                          .Cruise(centre.y);
                Fired++;
            }
        }
    }
}
