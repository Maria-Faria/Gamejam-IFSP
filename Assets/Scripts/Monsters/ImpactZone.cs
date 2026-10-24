using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// A patch of blasted ground left where the muscle boss slams into a wall: a smouldering ring of darkness
    /// that, for a few seconds, hurts everything standing in it every tick, then fades. The prefab is built
    /// one metre in radius and scaled to `radius` when spawned.
    public class ImpactZone : MonoBehaviour
    {
        [SerializeField] float tick = 0.5f;
        [Tooltip("Seconds it takes to grow to full size, and to fade at the end.")]
        [SerializeField] float growFor = 0.25f;
        [SerializeField] float fadeFor = 0.6f;
        [Tooltip("How high above the ground it still hurts (m).")]
        [SerializeField] float height = 2f;

        float _radius, _duration, _damage, _age, _nextTick;
        Monster _source;
        Vector3 _scale;
        readonly List<ParticleSystem> _smoke = new List<ParticleSystem>();
        bool _fading;

        public float Radius => _radius;
        public int Ticks { get; private set; }

        public static ImpactZone Spawn(ImpactZone prefab, Vector3 at, float radius, float duration, float damagePerTick, Monster source)
        {
            var z = Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            z._radius = radius;
            z._duration = duration;
            z._damage = damagePerTick;
            z._source = source;
            z._scale = Vector3.one * radius;
            z.transform.localScale = z._scale * 0.2f;
            z._nextTick = z.growFor;
            z.GetComponentsInChildren(z._smoke);
            return z;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float grow = Mathf.SmoothStep(0.2f, 1f, Mathf.Clamp01(_age / Mathf.Max(growFor, 1e-3f)));
            float fade = Mathf.Clamp01((_duration - _age) / Mathf.Max(fadeFor, 1e-3f));
            transform.localScale = _scale * grow * Mathf.Lerp(0.6f, 1f, fade);
            if (!_fading && _age >= _duration - fadeFor)
            {
                _fading = true;
                foreach (var ps in _smoke)
                    if (ps) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            if (!_fading && _age >= _nextTick)
            {
                _nextTick += tick;
                Hurt();
            }
            if (_age >= _duration)
            {
                enabled = false;               // the last smoke drifts off before it goes
                Destroy(gameObject, 1.5f);
            }
        }

        void Hurt()
        {
            Ticks++;
            var c = transform.position;
            var hit = new HashSet<Health>();
            foreach (var col in Physics.OverlapSphere(c + Vector3.up * height * 0.5f, _radius + height * 0.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                var h = Health.Victim(col, _source ? _source.transform : null);
                if (h == null || !hit.Add(h))
                    continue;
                var p = h.transform.position;
                if (Monster.Flat(p - c).magnitude > _radius || p.y < c.y - 0.5f || p.y > c.y + height)
                    continue;
                h.TakeDamage(_damage, c, _source);
            }
        }
    }
}
