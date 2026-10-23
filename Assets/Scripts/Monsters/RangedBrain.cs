using UnityEngine;

namespace Roguelike
{
    /// The casters (regular, three-eyed, crescent): stay put and shoot from afar. Only every few seconds — or
    /// when the target gets too close, too far or out of sight — they drift to a new spot at a comfortable
    /// distance, a little round to one side, and settle there again. One with a TeleportAttack (the
    /// three-eyed) blinks to its new spot instead whenever the teleport is ready.
    public class RangedBrain : MonsterBrain
    {
        [Tooltip("Distance band (m) it likes to shoot from.")]
        [SerializeField] Vector2 keepAt = new Vector2(4f, 8f);
        [Tooltip("Seconds it stays on a spot (min..max).")]
        [SerializeField] Vector2 stayFor = new Vector2(3f, 6f);
        [Tooltip("Farthest it moves at a time (m).")]
        [SerializeField] float hop = 2.5f;
        [Tooltip("Speed of the drift to the new spot (x move speed).")]
        [SerializeField] float hopSpeed = 1.2f;

        Vector3 _dest;
        bool _moving;
        float _moveUntil, _stayUntil, _settledAt;

        public bool Repositioning => _moving;

        public override void Tick()
        {
            if (!M.Engaged)
            {
                _moving = false;
                _stayUntil = Time.time + Random.Range(0f, stayFor.x);     // first shoots, then moves
                return;
            }
            if (_moving)
            {
                M.FaceTarget(0.6f);
                if (M.MoveTo(_dest, hopSpeed, 0.15f, false) || Time.time > _moveUntil)
                {
                    _moving = false;
                    _settledAt = Time.time;
                    _stayUntil = Time.time + Random.Range(stayFor.x, stayFor.y);
                }
                return;
            }
            float dist = M.TargetDistance;
            M.FaceTarget();
            bool sees = M.CanSee(TargetPos);
            if (sees && M.TryAttack(dist, a => !(a is TeleportAttack)))
                return;
            bool uncomfortable = dist < keepAt.x * 0.7f || dist > keepAt.y * 1.2f || !sees;
            if (Time.time >= _stayUntil || (uncomfortable && Time.time >= _settledAt + 1f))
                Reposition();
        }

        void Reposition()
        {
            var teleport = M.Attack<TeleportAttack>();
            if (teleport && !teleport.OnCooldown && M.Run(teleport))
            {
                _settledAt = Time.time;
                _stayUntil = Time.time + Random.Range(stayFor.x, stayFor.y);
                return;
            }
            var away = -ToTarget;
            float side = Random.value < 0.5f ? -1f : 1f;
            var bearing = Quaternion.Euler(0f, side * Random.Range(20f, 60f), 0f) * away;
            if (PickSpot(TargetPos, keepAt.x, keepAt.y, out _dest, bearing, 25f, hop) ||
                PickSpot(TargetPos, keepAt.x, keepAt.y, out _dest, away, 120f, hop))
            {
                _moving = true;
                _moveUntil = Time.time + hop / Mathf.Max(M.MoveSpeed * hopSpeed, 0.1f) + 1f;
            }
            else
                _stayUntil = Time.time + 1f;
        }
    }
}
