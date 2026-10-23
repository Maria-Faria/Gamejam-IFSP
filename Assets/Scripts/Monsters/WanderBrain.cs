using UnityEngine;

namespace Roguelike
{
    /// The shadow balls: roll about at random around where they started, pausing now and then. With a
    /// rushRange (the twisted ball) it charges the target once it comes that close, and lunges at it; without,
    /// it only lashes out at a target that walks right into it.
    public class WanderBrain : MonsterBrain
    {
        [Tooltip("How far from its starting point it roams (m).")]
        [SerializeField] float wanderRadius = 3.5f;
        [Tooltip("Roaming speed (x the monster's move speed).")]
        [SerializeField] float wanderSpeed = 0.7f;
        [Tooltip("Rest between two roams (s, min..max).")]
        [SerializeField] Vector2 pause = new Vector2(0.2f, 1.4f);
        [Tooltip("Charges the target when it comes within this distance (m). 0 = never.")]
        [SerializeField] float rushRange;
        [SerializeField] float rushSpeed = 2.2f;
        [Tooltip("Gives up the charge when the target gets this far away (m).")]
        [SerializeField] float giveUpRange = 8f;

        Vector3 _dest;
        bool _roaming;
        float _restUntil, _roamUntil;

        public bool Rushing { get; private set; }

        public override void Tick()
        {
            float dist = M.TargetDistance;
            if (rushRange > 0f)
            {
                if (!Rushing && M.HasTarget && dist <= rushRange && M.CanSee(TargetPos))
                    Rushing = true;
                else if (Rushing && (!M.HasTarget || dist > giveUpRange))
                {
                    Rushing = false;
                    _roaming = false;
                }
            }
            if (M.HasTarget && M.TryAttack(dist))
                return;
            if (Rushing)
            {
                if (dist > M.Size * 0.8f) M.Move(ToTarget, rushSpeed);
                else M.FaceTarget(2f);
                return;
            }
            Roam();
        }

        void Roam()
        {
            if (_roaming)
            {
                if (M.MoveTo(_dest, wanderSpeed * Random.Range(0.9f, 1.1f)) || Time.time > _roamUntil)
                {
                    _roaming = false;
                    _restUntil = Time.time + Random.Range(pause.x, pause.y);
                }
                return;
            }
            if (Time.time < _restUntil)
                return;
            // anywhere around home, but mostly onwards rather than doubling back
            var home = M.Home;
            bool far = Vector3.Distance(transform.position, home) > wanderRadius * 0.6f;
            if (PickSpot(far ? home : transform.position, 0.8f * MonsterScale.Factor, far ? wanderRadius * 0.6f : wanderRadius * 0.7f, out _dest,
                         far ? (Vector3?)null : transform.forward, far ? 180f : 120f))
            {
                _roaming = true;
                _roamUntil = Time.time + 4f;
            }
            else
                _restUntil = Time.time + 0.5f;
        }
    }
}
