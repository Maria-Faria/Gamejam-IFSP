using UnityEngine;

namespace Roguelike
{
    /// Decides what a monster does: where it goes and which attack it uses when. The Monster calls Tick every
    /// frame no attack is running; the brain moves it (Monster.Move/MoveTo) and starts attacks
    /// (Monster.TryAttack/Run). Shared movement patterns live here; the subclasses mix them per monster.
    [RequireComponent(typeof(Monster))]
    public abstract class MonsterBrain : MonoBehaviour
    {
        protected Monster M;
        int _strafe = 1;
        float _strafeFlip;

        protected virtual void Awake() => M = GetComponent<Monster>();

        public abstract void Tick();

        protected Vector3 TargetPos => M.Target ? M.Target.position : transform.position;
        protected Vector3 ToTarget => Monster.Flat(TargetPos - transform.position);

        /// A free spot `minR`..`maxR` from `center` that the monster can walk to in a straight line. With a
        /// `bearing`, the spot is taken within `spread` degrees of that direction from the center.
        protected bool PickSpot(Vector3 center, float minR, float maxR, out Vector3 spot, Vector3? bearing = null,
                                float spread = 180f, float maxTravel = float.PositiveInfinity)
        {
            var baseDir = bearing.HasValue && Monster.Flat(bearing.Value).sqrMagnitude > 1e-4f
                ? Monster.Flat(bearing.Value).normalized
                : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            for (int i = 0; i < 14; i++)
            {
                var dir = Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * baseDir;
                var p = center + dir * Random.Range(minR, maxR);
                p.y = transform.position.y;
                var travel = p - transform.position;
                if (travel.magnitude > maxTravel)
                    p = transform.position + travel.normalized * maxTravel;
                if (M.CanReach(p))
                {
                    spot = p;
                    return true;
                }
            }
            spot = transform.position;
            return false;
        }

        /// One frame of holding a distance band from the target while facing it: backs off when it is closer
        /// than `min`, closes in past `max`, and otherwise circles it (switching direction now and then, or
        /// when it bumps into something).
        protected void KeepRange(float min, float max, float speed, float circleSpeed)
        {
            M.FaceTarget();
            var to = ToTarget;
            float d = to.magnitude;
            if (d < 1e-3f)
                return;
            var dir = to / d;
            if (Time.time > _strafeFlip)
            {
                _strafeFlip = Time.time + Random.Range(1.5f, 3.5f);
                if (Random.value < 0.4f) _strafe = -_strafe;
            }
            var side = Vector3.Cross(Vector3.up, dir) * _strafe;
            if (d < min)
            {
                if (!M.Move(-dir + side * 0.5f, speed, false))
                    M.Move(side, speed, false);
            }
            else if (d > max)
                M.Move(dir, speed, false);
            else if (circleSpeed > 0f && !M.Move(side, circleSpeed, false))
                _strafe = -_strafe;
        }
    }
}
