using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// A monster's projectile: a little ghost of smoke that flies straight or ballistic, sweeps a sphere along
    /// its path and damages the first victim it touches; with a splash radius it also hurts everything around
    /// the impact point. Ignores its owner and other monsters. Variants, set on the prefab: a boomerang flies
    /// out, slows, and curves back to its thrower, cutting through everyone on the way; a bomb bursts where it
    /// lands into a ring of other projectiles; a summoning glob hatches a monster where it lands. Per shot it
    /// can also sway from side to side (Wave) or level out at a cruising height (Cruise).
    public class Projectile : MonoBehaviour
    {
        static readonly int PhaseId = Shader.PropertyToID("_Phase");
        static readonly Dictionary<Transform, List<GameObject>> Summons = new Dictionary<Transform, List<GameObject>>();

        [SerializeField] float radius = 0.12f;
        [SerializeField] float lifetime = 4f;
        [Tooltip("Spawned where it hits (smoke puff / splash).")]
        [SerializeField] GameObject impactEffect;
        [Tooltip("Turns about its up axis (deg/s) instead of pointing where it flies: the boomerang's spin.")]
        [SerializeField] float spin;
        [Header("Boomerang")]
        [Tooltip("Flies out this far (m), slowing down, then curves back to its thrower. 0 = flies on.")]
        [SerializeField] float returnAfter;
        [Header("Burst")]
        [Tooltip("Where it lands it bursts into a ring of these.")]
        [SerializeField] Projectile burstInto;
        [SerializeField] int burstCount = 8;
        [SerializeField] float burstSpeed = 5f;
        [SerializeField] float burstDamage = 6f;
        [Header("Summon")]
        [Tooltip("Spawned on the ground where it lands (a monster).")]
        [SerializeField] GameObject summon;
        [Tooltip("At most this many of the thrower's summons alive at once.")]
        [SerializeField] int maxSummons = 3;

        Vector3 _velocity, _base, _side, _origin, _launchDir;
        float _gravity, _damage, _splash, _dieAt, _age, _speed, _homeHeight;
        float _waveAmp, _waveFreq, _wavePhase;
        float _cruise = float.NaN;
        bool _returning;
        Transform _owner;
        Collider _ignore;                   // what a burst was born inside of, for a moment
        float _ignoreUntil;
        readonly HashSet<Health> _pierced = new HashSet<Health>();

        public Vector3 Velocity => _velocity;
        public bool Returning => _returning;
        public bool IsBoomerang => returnAfter > 0f;

        public static Projectile Spawn(Projectile prefab, Vector3 position, Vector3 velocity, float damage, Transform owner,
                                       float gravity = 0f, float splash = 0f, float scale = 1f)
        {
            var p = Instantiate(prefab, position, velocity.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(velocity) : Quaternion.identity);
            p.transform.localScale *= scale;
            p.radius *= scale;
            p._velocity = velocity;
            p._speed = velocity.magnitude;
            p._launchDir = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.forward;
            var flat = Monster.Flat(p._launchDir);
            p._side = flat.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
            p._base = p._origin = position;
            p._damage = damage;
            p._owner = owner;
            p._homeHeight = owner ? position.y - owner.position.y : 0f;
            p._gravity = gravity;
            p._splash = splash;
            p._dieAt = Time.time + p.lifetime;
            var block = new MaterialPropertyBlock();
            block.SetFloat(PhaseId, Random.Range(0f, 100f));
            foreach (var r in p.GetComponentsInChildren<MeshRenderer>())
                r.SetPropertyBlock(block);
            return p;
        }

        /// Launch velocity for a lob that lands on `to` after `time` seconds under `gravity`.
        public static Vector3 BallisticVelocity(Vector3 from, Vector3 to, float time, float gravity) =>
            (to - from) / time + 0.5f * gravity * time * Vector3.up;

        /// Sways from side to side across its path: `amplitude` m, `frequency` swings a second, starting at
        /// `phase` (radians) — it still leaves from where it was spawned.
        public Projectile Wave(float amplitude, float frequency, float phase)
        {
            _waveAmp = amplitude;
            _waveFreq = frequency;
            _wavePhase = phase;
            return this;
        }

        /// Climbs or sinks to fly level at world height `y` (so bolts from a tall monster still reach the player).
        public Projectile Cruise(float y)
        {
            _cruise = y;
            return this;
        }

        public Projectile Life(float seconds)
        {
            _dieAt = Time.time + seconds;
            return this;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (IsBoomerang && Boomerang(dt))
                return;
            _velocity += Vector3.down * _gravity * dt;
            if (!float.IsNaN(_cruise))
                _velocity.y = (_cruise - _base.y) * 4f;
            var nextBase = _base + _velocity * dt;
            var next = nextBase;
            if (_waveAmp > 0f)
                next += _side * (_waveAmp * (Mathf.Sin(_age * _waveFreq * 2f * Mathf.PI + _wavePhase) - Mathf.Sin(_wavePhase)));
            var step = next - transform.position;
            if (Sweep(step))
                return;
            _base = nextBase;
            transform.position = next;
            if (spin != 0f)
                transform.Rotate(0f, spin * dt, 0f, Space.World);
            else if (step.sqrMagnitude > 1e-8f)
                transform.rotation = Quaternion.LookRotation(step);
            if (Time.time > _dieAt)
                Impact(transform.position);
        }

        /// Slows down on the way out, then homes back on the thrower; true once caught.
        bool Boomerang(float dt)
        {
            float decel = _speed * _speed / (2f * returnAfter);
            if (!_returning)
            {
                _velocity -= _launchDir * decel * dt;
                if (Vector3.Dot(_velocity, _launchDir) <= _speed * 0.05f)
                    Turn();
                return false;
            }
            var home = _owner ? _owner.position + Vector3.up * _homeHeight : _origin;
            var to = home - _base;
            if (Monster.Flat(to).magnitude < 0.5f * MonsterScale.Factor)
            {
                Impact(transform.position);
                return true;
            }
            _velocity = Vector3.MoveTowards(_velocity, to.normalized * _speed, decel * 2f * dt);
            return false;
        }

        void Turn()
        {
            _returning = true;
            _pierced.Clear();               // it can cut the same target again on the way back
        }

        /// Checks the path of this frame's step; returns true if the projectile is done (hit something).
        bool Sweep(Vector3 step)
        {
            float len = step.magnitude;
            if (len < 1e-6f)
                return false;
            var hits = Physics.SphereCastAll(transform.position, radius, step / len, len, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (_owner && h.collider.transform.IsChildOf(_owner)) continue;
                if (h.collider.GetComponentInParent<Monster>()) continue;
                if (h.collider == _ignore && Time.time < _ignoreUntil) continue;
                var victim = Health.Victim(h.collider, _owner);
                var point = h.distance <= 0f || h.point == Vector3.zero ? transform.position : h.point;
                if (IsBoomerang)
                {
                    if (victim)
                    {
                        if (_pierced.Add(victim)) victim.TakeDamage(_damage, transform.position);
                        continue;
                    }
                    if (h.collider.GetComponentInParent<CharacterController>())
                        continue;
                    if (_returning)
                    {
                        Impact(point);
                        return true;
                    }
                    // glances off the wall and starts back
                    Turn();
                    _velocity = -_launchDir * _speed * 0.2f;
                    return true;
                }
                victim?.TakeDamage(_damage, transform.position);
                if (_splash > 0f)
                    foreach (var c in Physics.OverlapSphere(point, _splash, ~0, QueryTriggerInteraction.Ignore))
                        if (c != h.collider)
                            Health.Victim(c, _owner)?.TakeDamage(_damage * 0.5f, point);
                Impact(point, h.collider);
                return true;
            }
            return false;
        }

        void Impact(Vector3 at, Collider hit = null)
        {
            if (impactEffect)
            {
                var fx = Instantiate(impactEffect, at, Quaternion.identity);
                fx.transform.localScale = transform.localScale * MonsterScale.Factor;
                Destroy(fx, 2f);
            }
            if (burstInto && burstCount > 0)
                Burst(at, hit);
            if (summon)
                Summon(at);
            Destroy(gameObject);
        }

        void Burst(Vector3 at, Collider inside)
        {
            var c = at + Vector3.up * 0.45f * MonsterScale.Factor;
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < burstCount; i++)
            {
                var dir = Quaternion.Euler(0f, start + i * 360f / burstCount, 0f) * Vector3.forward;
                var p = Spawn(burstInto, c, dir * burstSpeed, burstDamage, _owner).Cruise(c.y);
                p._ignore = inside;
                p._ignoreUntil = Time.time + 0.3f;
            }
        }

        void Summon(Vector3 at)
        {
            List<GameObject> alive = null;
            if (_owner)
            {
                if (!Summons.TryGetValue(_owner, out alive)) Summons[_owner] = alive = new List<GameObject>();
                alive.RemoveAll(g => g == null || !g.activeInHierarchy || (g.GetComponent<Health>()?.IsDead ?? false));
                if (alive.Count >= maxSummons)
                    return;
            }
            var ground = at;
            if (Physics.Raycast(at + Vector3.up * MonsterScale.Factor, Vector3.down, out var hit, 3f * MonsterScale.Factor, ~0, QueryTriggerInteraction.Ignore))
                ground = hit.point;
            var look = Monster.Flat(_velocity);
            var go = Instantiate(summon, ground, look.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(look) : Quaternion.identity);
            var m = go.GetComponent<Monster>();
            var parent = _owner ? _owner.GetComponent<Monster>() : null;
            if (m && parent) m.Target = parent.Target;
            alive?.Add(go);
        }
    }
}
