using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// Close-range hits on every "Hit" event of its clip: a punch (ghost, ghoul, horned) or each slap of the
    /// fat one's flailing arms. Damages whatever is around the striking hands, or right in front of the
    /// monster within reach.
    public class MeleeAttack : MonsterAttack
    {
        [Tooltip("Bones that strike, e.g. Hand.R; every one of them is checked on each Hit event.")]
        [SerializeField] string[] hands = { "Hand.R" };
        [Tooltip("Hit radius around each hand (x monster height).")]
        [SerializeField] float handRadius = 0.35f;
        [Tooltip("Also hits a target in front within this reach (x monster height), in a 120° cone.")]
        [SerializeField] float reach = 0.9f;
        [Tooltip("Step forward during the clip (m), so the strike lands.")]
        [SerializeField] float lunge = 0.4f;
        [Tooltip("Part of the clip (0..1) spent moving in; after that it stays where it got to.")]
        [Range(0.05f, 1f)] [SerializeField] float advanceFor = 0.45f;
        [SerializeField] float trackTarget = 0.5f;

        public override IEnumerator Perform()
        {
            M.Play(state);
            float len = M.ClipLength(state);
            float t = 0f;
            float moved = 0f;
            while (t < len)
            {
                t += Time.deltaTime;
                M.FaceTowards(TargetPoint, trackTarget);
                // move in while the blows are thrown, then stop
                float want = lunge * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (len * advanceFor)));
                var to = Monster.Flat(TargetPoint - transform.position);
                float room = Mathf.Max(0f, to.magnitude - M.Size * 0.45f);
                float step = Mathf.Min(want - moved, room);
                if (step > 0f)
                {
                    transform.position += transform.forward * step;
                    moved += step;
                }
                yield return null;
            }
        }

        public override void OnAnimationEvent(string evt)
        {
            if (evt != "Hit")
                return;
            var hit = new HashSet<Health>();
            foreach (var h in hands)
            {
                var bone = M.Bone(h);
                if (bone) HitSphere(bone.position, handRadius * M.Size, damage, hit);
            }
            if (M.Target && hit.Count == 0)
            {
                var to = M.Target.position - transform.position;
                if (Monster.Flat(to).magnitude <= reach * M.Size && Vector3.Angle(transform.forward, Monster.Flat(to)) < 60f)
                {
                    var victim = M.Target.GetComponentInParent<Health>();
                    if (victim && !victim.IsDead) victim.TakeDamage(damage, transform.position);
                }
            }
        }
    }
}
