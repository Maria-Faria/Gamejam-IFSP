using System.Collections;
using UnityEngine;

namespace Roguelike
{
    /// The fat one keeps its distance and spits — each spit either hatches a shadow ball or sprays a shotgun
    /// blast — and every other round, or as soon as the target pushes in, it bounces up and comes down on top
    /// of it (half the landings also fire off four bolts, north, east, south and west). A target that stays
    /// close gets the flailing arms: it shuffles in, slapping, then stops. Enraged: three spits a round, a
    /// double bounce every round, less rest.
    public class FatBoss : BossBrain
    {
        [Tooltip("Distance band (m) it spits from.")]
        [SerializeField] Vector2 keepAt = new Vector2(6f, 9f);
        [Tooltip("Closer than this (m) it flails at the target.")]
        [SerializeField] float crowdedAt = 3.5f;

        MonsterAttack _spit, _jump, _flail;
        int _round;

        protected override void Awake()
        {
            base.Awake();
            _spit = M.Attack<SpitAttack>();
            _jump = M.Attack<JumpSmashAttack>();
            _flail = M.Attack<MeleeAttack>("Flail");
        }

        bool Crowded() => Dist < crowdedAt;

        protected override IEnumerator Round()
        {
            _round++;
            // back off to spitting distance
            yield return Hover(keepAt.x, keepAt.y, Phase == 1 ? 1.6f : 0.9f, 1f, Crowded);
            int spits = Phase == 1 ? 2 : 3;
            for (int i = 0; i < spits; i++)
            {
                if (Crowded())
                {
                    yield return Use(_flail);
                    break;
                }
                yield return Use(_spit);
                yield return Hover(keepAt.x, keepAt.y, Phase == 1 ? 0.6f : 0.25f, 1f, Crowded);
            }
            // bounce onto the target
            if (Phase > 1 || _round % 2 == 0 || Dist < keepAt.x * 0.6f)
            {
                yield return Use(_jump);
                if (Phase > 1)
                {
                    yield return Hold(0.2f);
                    yield return Use(_jump);
                }
                if (Crowded())
                    yield return Use(_flail);
            }
            yield return Hold(Phase == 1 ? 0.7f : 0.35f);
        }
    }
}
