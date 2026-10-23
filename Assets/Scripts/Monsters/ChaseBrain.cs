using UnityEngine;

namespace Roguelike
{
    /// The brawlers (ghost, ghoul): once they notice the target they follow it and hit it whenever it is in
    /// reach. While the punch recharges they hover close, circling, instead of standing still.
    public class ChaseBrain : MonsterBrain
    {
        [Tooltip("Following speed (x the monster's move speed).")]
        [SerializeField] float chaseSpeed = 1f;
        [Tooltip("Circling speed while its attacks recharge (x move speed). 0 = waits in place.")]
        [SerializeField] float circleSpeed = 0.45f;

        public override void Tick()
        {
            if (!M.Engaged)
                return;
            float dist = M.TargetDistance;
            M.FaceTarget();
            if (M.TryAttack(dist))
                return;
            float reach = M.Reach;
            if (dist > reach * 0.8f)
                M.Move(ToTarget, chaseSpeed);
            else
                KeepRange(reach * 0.45f, reach * 0.8f, chaseSpeed, circleSpeed);
        }
    }
}
