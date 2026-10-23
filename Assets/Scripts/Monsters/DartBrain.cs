using UnityEngine;

namespace Roguelike
{
    /// The claw: a close-range skirmisher, never still for long. It darts from spot to spot close round the
    /// target, fast, with a short pause between darts; at a pause it sometimes (when ready) bursts a ring of
    /// bolts out all round, sometimes dives straight in to rake the target with its claws, and sometimes
    /// blinks away to reappear somewhere else next to it.
    public class DartBrain : MonsterBrain
    {
        [Tooltip("Distance band (m) it darts about in.")]
        [SerializeField] Vector2 keepAt = new Vector2(1.5f, 3.5f);
        [Tooltip("Darting speed (x move speed).")]
        [SerializeField] float dartSpeed = 1f;
        [Tooltip("Pause between darts (s, min..max).")]
        [SerializeField] Vector2 pause = new Vector2(0.15f, 0.5f);
        [Tooltip("Chance at each pause to dive in for the claws / to shoot (when ready).")]
        [Range(0f, 1f)] [SerializeField] float clawChance = 0.3f;
        [Range(0f, 1f)] [SerializeField] float shootChance = 0.35f;
        [Range(0f, 1f)] [SerializeField] float teleportChance = 0.2f;
        [SerializeField] float diveSpeed = 1.3f;

        enum Mode { Pause, Dart, Dive }

        Mode _mode;
        Vector3 _dest;
        float _until;
        int _side = 1;

        public bool Diving => _mode == Mode.Dive;

        public override void Tick()
        {
            if (!M.Engaged)
                return;
            float dist = M.TargetDistance;
            var claw = M.Attack<MeleeAttack>();
            switch (_mode)
            {
                case Mode.Dive:
                    M.FaceTarget(2f);
                    if (claw && dist <= claw.MaxRange * 0.8f && M.Run(claw))
                        _mode = Mode.Pause;            // the next tick after the rake darts away
                    else if (Time.time > _until || !M.Move(ToTarget, diveSpeed))
                        _mode = Mode.Pause;
                    return;
                case Mode.Dart:
                    if (M.MoveTo(_dest, dartSpeed) || Time.time > _until)
                    {
                        _mode = Mode.Pause;
                        _until = Time.time + Random.Range(pause.x, pause.y);
                    }
                    return;
            }
            M.FaceTarget(2f);
            if (Time.time < _until)
                return;
            float roll = Random.value;
            if (claw && !claw.OnCooldown && M.Ready && roll < clawChance)
            {
                _mode = Mode.Dive;
                _until = Time.time + 2f;
                return;
            }
            if (roll < clawChance + shootChance && M.TryAttack(dist, a => a is ProjectileAttack || a is RadialShotAttack))
                return;
            var teleport = M.Attack<TeleportAttack>();
            if (roll < clawChance + shootChance + teleportChance && teleport && !teleport.OnCooldown && M.Run(teleport))
                return;
            Dart();
        }

        void Dart()
        {
            // round the target, alternating sides now and then, always somewhere new
            if (Random.value < 0.35f) _side = -_side;
            var from = -ToTarget;
            var bearing = Quaternion.Euler(0f, _side * Random.Range(40f, 100f), 0f) * from;
            if (PickSpot(TargetPos, keepAt.x, keepAt.y, out _dest, bearing, 25f, keepAt.y * 1.5f) ||
                PickSpot(TargetPos, keepAt.x, keepAt.y, out _dest, from, 160f))
            {
                _mode = Mode.Dart;
                _until = Time.time + 2f;
            }
            else
                _until = Time.time + 0.3f;
        }
    }
}
