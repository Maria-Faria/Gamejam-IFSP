using System.Collections;
using UnityEngine;

namespace Roguelike
{
    /// The skull face twists down into smoke ("Vanish"), and reappears ("Appear") somewhere around the target,
    /// facing it, ready for its next attack. Optionally deals damage to whoever stands where it reappears.
    public class TeleportAttack : MonsterAttack
    {
        [SerializeField] GameObject smokePuff;
        [SerializeField] float minDistance = 3f;
        [SerializeField] float maxDistance = 6f;
        [Tooltip("Burst radius on reappearing (x monster height); 0 = harmless.")]
        [SerializeField] float burstRadius = 0.5f;

        Vector3 _destination;

        public override IEnumerator Perform()
        {
            _destination = PickDestination();
            yield return PlayAndTrack(state, 0f);
        }

        Vector3 PickDestination()
        {
            var center = TargetPoint;
            for (int i = 0; i < 12; i++)
            {
                float a = Random.Range(0f, 360f);
                var p = center + Quaternion.Euler(0f, a, 0f) * Vector3.forward * Random.Range(minDistance, maxDistance);
                p.y = transform.position.y;
                // keep clear of walls: the body must fit there
                if (!Physics.CheckSphere(p + Vector3.up * M.Size * 0.5f, M.Size * 0.3f, ~0, QueryTriggerInteraction.Ignore)
                    && !Physics.Linecast(center + Vector3.up * 0.5f * MonsterScale.Factor, p + Vector3.up * 0.5f * MonsterScale.Factor, ~0, QueryTriggerInteraction.Ignore))
                    return p;
            }
            return transform.position;
        }

        void Puff(Vector3 at)
        {
            if (!smokePuff) return;
            var fx = Instantiate(smokePuff, at + Vector3.up * M.Size * 0.4f, Quaternion.identity);
            fx.transform.localScale = Vector3.one * M.Size;
            Destroy(fx, 3f);
        }

        public override void OnAnimationEvent(string evt)
        {
            if (evt == "Vanish")
                Puff(transform.position);
            else if (evt == "Appear")
            {
                transform.position = _destination;
                var to = Monster.Flat(TargetPoint - _destination);
                if (to.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(to);
                Puff(_destination);
                if (burstRadius > 0f)
                    HitSphere(_destination + Vector3.up * M.Size * 0.4f, burstRadius * M.Size, damage);
            }
        }
    }
}
