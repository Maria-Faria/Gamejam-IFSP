using UnityEngine;

namespace Roguelike
{
    /// The muscle one (a plain enemy now): a brawler. It stalks to a charging distance, paces there sizing the
    /// target up, and runs at it whenever the charge is ready and nothing is in the way (dazed for a moment
    /// if it slams into a wall instead); a target that ends up close, or comes close, gets punched.
    public class ChargerBrain : MonsterBrain
    {
        [Tooltip("Distance band (m) it likes to charge from.")]
        [SerializeField] Vector2 keepAt = new Vector2(4f, 8f);
        [SerializeField] float stalkSpeed = 0.8f;
        [SerializeField] float paceSpeed = 0.4f;

        public override void Tick()
        {
            if (!M.Engaged)
                return;
            float dist = M.TargetDistance;
            M.FaceTarget();
            if (M.CanSee(TargetPos) && M.TryAttack(dist))
                return;
            var punch = M.Attack<MeleeAttack>();
            if (punch && !punch.OnCooldown && dist < keepAt.x)
                M.Move(ToTarget, stalkSpeed * 1.4f);       // up close: go in for the punch
            else
                KeepRange(keepAt.x, keepAt.y, stalkSpeed, paceSpeed);
        }
    }
}
