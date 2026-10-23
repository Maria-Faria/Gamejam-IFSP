using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Roguelike
{
    /// Body of a monster: runs its attacks (MonsterAttack components on the same object) and moves it about.
    /// What it does and when — wander, chase, keep its distance, a boss pattern — is decided by its brain
    /// (a MonsterBrain component), which gets a Tick every frame no attack is running. Attacks drive the
    /// Animator states and react to the animation events baked into the clips ("Fire", "Hit", "Land", ...),
    /// which the clips send to MonsterEvent. Between attacks the Animator plays Move while the monster moves
    /// and Idle while it stands.
    [RequireComponent(typeof(Animator))]
    public class Monster : MonoBehaviour
    {
        static readonly List<Monster> All = new List<Monster>();

        [SerializeField] Transform target;
        [Tooltip("Height of the monster (m), set by Roguelike/Build Monsters; attacks scale their reach with it.")]
        [SerializeField] float size = 1f;
        [Tooltip("Notices the target within this distance (m) and in sight; loses it again past 1.5x.")]
        [SerializeField] float aggroRange = 12f;
        [SerializeField] float turnSpeed = 220f;
        [Tooltip("Walking/floating speed (m/s). The brain scales it: wandering is slower, rushing faster.")]
        [SerializeField] float moveSpeed = 1.2f;
        [Tooltip("Pause after an attack before the next one.")]
        [SerializeField] float recovery = 0.6f;
        [Tooltip("Burst played where it dies.")]
        [SerializeField] GameObject deathEffect;
        [Tooltip("Seconds after dying before the body is removed (negative = it stays).")]
        [SerializeField] float removeAfter = 0.15f;

        readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();
        Dictionary<string, AnimationClip> _clips;
        MonsterAttack[] _attacks;
        MonsterAttack _current;
        MonsterBrain _brain;
        CapsuleCollider _body;
        float _nextThink, _lastMoved = -1f;
        Health _health;

        public Animator Animator { get; private set; }
        public float Size => size;
        public float AggroRange => aggroRange;
        public float MoveSpeed => moveSpeed * SpeedScale;
        /// Multipliers the game sets per spawn (level difficulty, variants): walking speed, damage of every
        /// attack and projectile, and attack cooldowns / recovery.
        public float SpeedScale { get; set; } = 1f;
        public float DamageScale { get; set; } = 1f;
        public float CooldownScale { get; set; } = 1f;
        /// Out of the fight for now (a phantom faded away): it does not think or attack, the player's aim and
        /// weapons ignore it.
        public bool Untargetable { get; set; }
        public bool Busy => _current != null;
        public bool Dead => _health && _health.IsDead;
        /// Free to start an attack: none running and recovered from the last one.
        public bool Ready => !Busy && Time.time >= _nextThink;
        public bool Engaged { get; private set; }
        public Vector3 Home { get; private set; }
        public MonsterAttack CurrentAttack => _current;
        public IReadOnlyList<MonsterAttack> Attacks => _attacks;
        public MonsterBrain Brain => _brain;
        public float Radius => _body ? _body.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) : size * 0.3f;
        public Vector3 Center => transform.position + Vector3.up * size * 0.5f;
        public Vector3 BodyCenter => _body ? transform.TransformPoint(_body.center) : Center;

        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                MonsterTargeting.Prepare(value);
            }
        }

        /// Its hit points (the player's weapons damage it through this; WeaponDamage finds it).
        public Health Health => _health;

        public bool HasTarget
        {
            get
            {
                if (target == null) return false;
                var h = target.GetComponentInParent<Health>();
                return h == null || !h.IsDead;
            }
        }

        /// Ground distance to the target (infinity without one).
        public float TargetDistance => target ? Flat(target.position - transform.position).magnitude : float.PositiveInfinity;

        /// Farthest reach of its attacks (m).
        public float Reach => _attacks.Where(a => a.enabled).Select(a => a.MaxRange).DefaultIfEmpty(0f).Max();

        /// Fired for every animation event the clips send (after the running attack has seen it).
        public event System.Action<string> AnimationEvent;
        /// Fired when an attack has played out (not when it is interrupted by death).
        public event System.Action<MonsterAttack> AttackFinished;
        /// Fired once when it dies (before the body is removed).
        public event System.Action<Monster> Died;

        void Awake()
        {
            Animator = GetComponent<Animator>();
            Animator.applyRootMotion = false;
            _attacks = GetComponents<MonsterAttack>();
            _clips = Animator.runtimeAnimatorController
                ? Animator.runtimeAnimatorController.animationClips.GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First())
                : new Dictionary<string, AnimationClip>();
            _health = GetComponent<Health>();
            if (_health) _health.Died += OnDied;
            _body = GetComponent<CapsuleCollider>();
            _brain = GetComponent<MonsterBrain>();
            if (_brain == null) _brain = gameObject.AddComponent<ChaseBrain>();
            _nextThink = Time.time + Random.Range(0.3f, 1.2f);
        }

        void OnEnable() => All.Add(this);
        void OnDisable()
        {
            All.Remove(this);
            // coroutines die with the object: drop the attack that was running
            if (_current) _current.Cleanup();
            _current = null;
        }

        void Start()
        {
            Home = transform.position;
            if (target == null)
                target = MonsterTargeting.Player();
            else
                MonsterTargeting.Prepare(target);
        }

        void Update()
        {
            if (Dead)
                return;
            UpdateEngaged();
            if (!Busy && !Untargetable && _brain && _brain.enabled)
                _brain.Tick();
        }

        void LateUpdate()
        {
            if (Dead || Busy)
                return;
            Separate();
            Locomotion();
        }

        void UpdateEngaged()
        {
            if (!HasTarget)
            {
                Engaged = false;
                return;
            }
            float dist = TargetDistance;
            if (!Engaged)
                Engaged = dist <= aggroRange && (CanSee(target.position) || (_health && Time.time - _health.LastHitTime < 0.5f));
            else if (dist > aggroRange * 1.5f)
                Engaged = false;
        }

        // ------------------------------------------------------------------------------------ attacks

        /// Starts an attack now (ignores range and cooldown). Returns false if another attack is running.
        public bool Run(MonsterAttack attack)
        {
            if (Busy || attack == null || Dead)
                return false;
            StartCoroutine(RunRoutine(attack));
            return true;
        }

        /// Starts a random one (by weight) of the attacks that are ready for a target `distance` away and pass
        /// `filter`. Returns false if none is, or if the monster is still recovering.
        public bool TryAttack(float distance, System.Func<MonsterAttack, bool> filter = null)
        {
            if (!Ready)
                return false;
            var ready = _attacks.Where(a => a.enabled && a.CanStart(distance) && (filter == null || filter(a))).ToList();
            if (ready.Count == 0)
                return false;
            float pick = Random.value * ready.Sum(a => a.Weight);
            foreach (var a in ready)
            {
                pick -= a.Weight;
                if (pick <= 0f) return Run(a);
            }
            return Run(ready[ready.Count - 1]);
        }

        public T Attack<T>(string state = null) where T : MonsterAttack =>
            _attacks.OfType<T>().FirstOrDefault(a => state == null || a.State == state);

        IEnumerator RunRoutine(MonsterAttack attack)
        {
            _current = attack;
            attack.MarkUsed();
            yield return attack.Perform();
            attack.Cleanup();
            _current = null;
            _nextThink = Time.time + recovery * CooldownScale;
            if (!Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && !Animator.IsInTransition(0))
                Play("Idle", 0.2f);
            AttackFinished?.Invoke(attack);
        }

        /// Animation event receiver (the event name is the string parameter).
        public void MonsterEvent(string name)
        {
            if (_current) _current.OnAnimationEvent(name);
            AnimationEvent?.Invoke(name);
        }

        // ------------------------------------------------------------------------------------ animation

        public void Play(string state, float fade = 0.12f) => Animator.CrossFadeInFixedTime(state, fade, 0, 0f);

        public float ClipLength(string clip) => _clips.TryGetValue(clip, out var c) ? c.length : 1f;

        public bool HasClip(string clip) => _clips.ContainsKey(clip);

        /// Waits until the state has played to its end (or another state took over).
        public IEnumerator WaitForState(string state, float speed = 1f)
        {
            float end = Time.time + ClipLength(state) / Mathf.Max(speed, 0.01f) + 0.05f;
            while (Time.time < end)
                yield return null;
        }

        void Locomotion()
        {
            string want = Time.time - _lastMoved < 0.15f && HasClip("Move") ? "Move" : "Idle";
            if (Animator.IsInTransition(0) ? Animator.GetNextAnimatorStateInfo(0).IsName(want)
                                           : Animator.GetCurrentAnimatorStateInfo(0).IsName(want))
                return;
            Play(want, 0.25f);
        }

        public Transform Bone(string name)
        {
            if (_bones.TryGetValue(name, out var t) && t)
                return t;
            t = GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            _bones[name] = t;
            return t;
        }

        // ------------------------------------------------------------------------------------ movement

        public void FaceTowards(Vector3 point, float speedScale)
        {
            var to = Flat(point - transform.position);
            if (to.sqrMagnitude < 1e-4f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to),
                turnSpeed * speedScale * Time.deltaTime);
        }

        public void FaceTarget(float speedScale = 1f)
        {
            if (target) FaceTowards(target.position, speedScale);
        }

        /// Moves one frame's worth along `direction` at moveSpeed x `speedScale`, sliding along walls. Turns to
        /// face the way it goes unless `face` is false (backing off, strafing). Returns false if blocked.
        public bool Move(Vector3 direction, float speedScale = 1f, bool face = true)
        {
            var dir = Flat(direction);
            if (dir.sqrMagnitude < 1e-6f || speedScale <= 0f)
                return false;
            dir.Normalize();
            if (face) FaceTowards(transform.position + dir, 1f);
            float step = MoveSpeed * speedScale * Time.deltaTime;
            if (Blocked(dir, step, out var normal))
            {
                var slide = Flat(Vector3.ProjectOnPlane(dir, normal));
                if (slide.sqrMagnitude < 0.04f || Blocked(slide.normalized, step, out _))
                    return false;
                dir = slide.normalized;
            }
            transform.position += dir * step;
            _lastMoved = Time.time;
            return true;
        }

        /// Heads for `point`; returns true once within `stopAt` of it — or when it cannot get any closer, so
        /// the caller picks another spot.
        public bool MoveTo(Vector3 point, float speedScale = 1f, float stopAt = 0.15f, bool face = true)
        {
            var to = Flat(point - transform.position);
            if (to.magnitude <= stopAt)
                return true;
            return !Move(to, Mathf.Min(speedScale, to.magnitude / Mathf.Max(MoveSpeed * Time.deltaTime, 1e-4f)), face);
        }

        /// Is a wall/obstacle (not the floor, not a monster) within `distance` along `dir`?
        public bool Blocked(Vector3 dir, float distance, out Vector3 normal)
        {
            normal = Vector3.zero;
            var origin = BodyCenter;
            float r = Radius * 0.9f;
            foreach (var hit in Physics.SphereCastAll(origin, r, dir, distance + 0.05f, ~0, QueryTriggerInteraction.Ignore))
            {
                var c = hit.collider;
                if (IsSelf(c) || c.GetComponentInParent<Monster>() || hit.normal.y > 0.6f)
                    continue;
                if (hit.distance <= 0f)
                {
                    // already touching it: only blocks if it is ahead
                    if (c is MeshCollider mc && !mc.convex)
                        continue;
                    if (Vector3.Dot(Flat(c.ClosestPoint(origin) - origin), dir) <= 0f)
                        continue;
                    normal = -dir;
                    return true;
                }
                normal = Flat(hit.normal).normalized;
                if (normal.sqrMagnitude < 1e-4f) normal = -dir;
                return true;
            }
            return false;
        }

        /// Could the body stand at `point` (no wall there) and get there in a straight line?
        public bool CanReach(Vector3 point)
        {
            var to = Flat(point - transform.position);
            if (to.sqrMagnitude > 1e-4f && Blocked(to.normalized, to.magnitude, out _))
                return false;
            var c = point + (BodyCenter - transform.position);
            foreach (var col in Physics.OverlapSphere(c, Radius, ~0, QueryTriggerInteraction.Ignore))
                if (!IsSelf(col) && !col.GetComponentInParent<Monster>() && col.bounds.max.y > point.y + 0.15f)
                    return false;
            return true;
        }

        /// Clear line from the monster's eyes to `point` (walls block it; characters and monsters don't).
        public bool CanSee(Vector3 point)
        {
            var from = Center;
            var to = point + Vector3.up * 0.5f * MonsterScale.Factor;
            var d = to - from;
            foreach (var h in Physics.RaycastAll(from, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                var c = h.collider;
                if (IsSelf(c) || c.GetComponentInParent<Monster>() || c.GetComponentInParent<Health>() || c.GetComponentInParent<CharacterController>())
                    continue;
                if (target && c.transform.IsChildOf(target))
                    continue;
                return false;
            }
            return true;
        }

        /// Pushes overlapping monsters apart, so a pack does not merge into one blob.
        void Separate()
        {
            var push = Vector3.zero;
            foreach (var o in All)
            {
                if (o == this || o.Dead)
                    continue;
                var d = Flat(transform.position - o.transform.position);
                float min = (Radius + o.Radius) * 0.9f, m = d.magnitude;
                if (m >= min)
                    continue;
                push += m > 1e-3f ? d / m * (min - m) : new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f) * 0.02f;
            }
            if (push.sqrMagnitude < 1e-6f)
                return;
            var step = Vector3.ClampMagnitude(push, 1.5f * MonsterScale.Factor * Time.deltaTime);
            if (!Blocked(step.normalized, step.magnitude, out _))
                transform.position += step;
        }

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        public bool IsSelf(Collider c) => c && c.transform.IsChildOf(transform);

        void OnDied()
        {
            StopAllCoroutines();
            if (_current) _current.Cleanup();
            _current = null;
            enabled = false;
            Died?.Invoke(this);
            if (deathEffect)
            {
                var fx = Instantiate(deathEffect, Center, Quaternion.identity);
                fx.transform.localScale = Vector3.one * size * 1.5f;
                Destroy(fx, 3f);
            }
            if (removeAfter >= 0f)
                Destroy(gameObject, removeAfter);
        }
    }
}
