using UnityEngine;

namespace Roguelike
{
    /// The horned one switches between two moods: hanging back and shooting a volley of bolts while it
    /// circles the target, then closing in to throw its punch combo, then backing off to shoot again. A
    /// target that crowds it while it shoots gets punched straight away.
    public class SkirmisherBrain : MonsterBrain
    {
        [Tooltip("Distance band (m) it shoots from.")]
        [SerializeField] Vector2 keepAt = new Vector2(4f, 7f);
        [Tooltip("Bolts per volley before it goes in to punch (min..max).")]
        [SerializeField] Vector2Int volley = new Vector2Int(1, 3);
        [Tooltip("Gives up the assault after this long without landing it (s).")]
        [SerializeField] float assaultTime = 6f;
        [SerializeField] float chaseSpeed = 1.3f;
        [SerializeField] float circleSpeed = 0.5f;

        bool _assault;
        int _shotsLeft;
        float _assaultUntil;

        public bool Assaulting => _assault;

        protected override void Awake()
        {
            base.Awake();
            M.AttackFinished += OnAttackFinished;
            _shotsLeft = Random.Range(volley.x, volley.y + 1);
        }

        void OnAttackFinished(MonsterAttack a)
        {
            if (a is ProjectileAttack && --_shotsLeft <= 0)
                StartAssault();
            else if (a is MeleeAttack)
                Withdraw();
        }

        void StartAssault()
        {
            _assault = true;
            _assaultUntil = Time.time + assaultTime;
        }

        void Withdraw()
        {
            _assault = false;
            _shotsLeft = Random.Range(volley.x, volley.y + 1);
        }

        public override void Tick()
        {
            if (!M.Engaged)
                return;
            float dist = M.TargetDistance;
            if (_assault)
            {
                if (Time.time > _assaultUntil)
                {
                    Withdraw();
                    return;
                }
                M.FaceTarget();
                if (M.TryAttack(dist, a => a is MeleeAttack))
                    return;
                var punch = M.Attack<MeleeAttack>();
                float reach = punch ? punch.MaxRange : 1.5f * MonsterScale.Factor;
                if (dist > reach * 0.8f) M.Move(ToTarget, chaseSpeed);
                else KeepRange(reach * 0.45f, reach * 0.8f, chaseSpeed, circleSpeed);
                return;
            }
            // shooting mood; a target in its face gets punched instead
            var melee = M.Attack<MeleeAttack>();
            if (melee && dist < melee.MaxRange && !melee.OnCooldown)
            {
                StartAssault();
                return;
            }
            M.FaceTarget();
            if (M.CanSee(TargetPos) && M.TryAttack(dist, a => a is ProjectileAttack))
                return;
            KeepRange(keepAt.x, keepAt.y, 1f, circleSpeed);
        }
    }
}
