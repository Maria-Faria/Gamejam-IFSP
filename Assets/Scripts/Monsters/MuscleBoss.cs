using System.Collections;
using System.Linq;
using UnityEngine;

namespace Roguelike
{
    /// The muscle boss stalks the target, then either charges it from afar — running the target over stuns it;
    /// slamming into a wall blows the wall apart (bolts flying off all eight ways, a blast and a burning impact
    /// zone left on the ground) and leaves it dazed, the opening to punish it — or, up close, either whirls
    /// with its fists out until it staggers dizzy, the other opening — flinging bolts north, east, south and
    /// west twice, a beat apart, while it whirls — or throws a flurry of punches. Now and then it roars up a
    /// barrage: two walls of bullets sweeping across the target. Enraged: it stalks faster, charges three
    /// times in a row (re-aiming each time) with a quicker run, spins longer, and the barrage comes every
    /// other round.
    public class MuscleBoss : BossBrain
    {
        [Tooltip("Closer than this (m) it spins instead of charging.")]
        [SerializeField] float spinRange = 3.2f;

        ChargeAttack _charge;
        SpinAttack _spin;
        MeleeAttack _flurry;
        bool _flurryNext;
        BulletPatternAttack _barrage;
        int _round;

        protected override void Awake()
        {
            base.Awake();
            _charge = M.Attack<ChargeAttack>();
            _spin = M.Attack<SpinAttack>();
            _flurry = M.Attack<MeleeAttack>();
            _flurryNext = Random.value < 0.5f;
            _barrage = M.Attacks.OfType<BulletPatternAttack>().FirstOrDefault();
        }

        protected override IEnumerator Enrage()
        {
            yield return base.Enrage();
            if (_charge)
            {
                _charge.Speed *= 1.3f;
                _charge.Windup *= 0.7f;
            }
            if (_spin) _spin.Duration *= 1.5f;
        }

        protected override IEnumerator Round()
        {
            _round++;
            // size the target up, closing in
            yield return Approach(spinRange * 0.8f, Phase == 1 ? Random.Range(1.5f, 2.5f) : Random.Range(0.7f, 1.3f),
                                  Phase == 1 ? 1f : 1.35f);
            if (Dist <= spinRange)
            {
                // spin and flurry take turns, now and then the same one twice
                bool flurry = _flurry && (!_spin || _flurryNext);
                _flurryNext = Random.value < 0.25f ? _flurryNext : !_flurryNext;
                yield return Use(flurry ? (MonsterAttack)_flurry : _spin);
                yield return Hold(Phase == 1 ? 0.6f : 0.3f);
            }
            else
            {
                int charges = Phase == 1 ? 1 : 3;
                for (int i = 0; i < charges; i++)
                {
                    yield return Use(_charge);
                    if (_charge == null || _charge.HitWall)
                        break;                     // dazed against the wall
                    if (i < charges - 1)
                        yield return Hold(0.2f);
                }
                yield return Hold(Phase == 1 ? 0.8f : 0.4f);
            }
            if (_round % (Phase == 1 ? 3 : 2) == 1)
                yield return Use(_barrage);
            yield return Hold(Phase == 1 ? 0.5f : 0.25f);
        }
    }
}
