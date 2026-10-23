using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Roguelike
{
    /// The skull face floats round the target at a distance, working through its bullet patterns — flower
    /// bursts, a rotating cross, a smoke bomb that bursts into a ring, a spinning ring of bolts, the charged
    /// laser — never the same one twice running, and teleports somewhere new every few attacks. Enraged it
    /// teleports before half its attacks to fire from an ambush, rests less, and every pattern is denser and
    /// faster, the laser sweeping quicker.
    public class SkullFaceBoss : BossBrain
    {
        [Tooltip("Distance band (m) it floats at.")]
        [SerializeField] Vector2 keepAt = new Vector2(5f, 8f);

        LaserAttack _laser;
        TeleportAttack _teleport;
        ProjectileAttack _ring, _bomb;
        BulletPatternAttack _cross, _flower;
        MonsterAttack _last;
        int _count;

        protected override void Awake()
        {
            base.Awake();
            _laser = M.Attack<LaserAttack>();
            _teleport = M.Attack<TeleportAttack>();
            _ring = M.Attack<ProjectileAttack>("SpinShoot");
            _bomb = M.Attack<ProjectileAttack>("Throw");
            var patterns = M.Attacks.OfType<BulletPatternAttack>().ToList();
            _cross = patterns.FirstOrDefault(p => p.Kind == BulletPatternAttack.Pattern.Cross);
            _flower = patterns.FirstOrDefault(p => p.Kind == BulletPatternAttack.Pattern.Flower);
        }

        protected override IEnumerator Enrage()
        {
            yield return base.Enrage();
            if (_laser) _laser.SweepSpeed *= 1.6f;
            foreach (var p in new[] { _cross, _flower })
                if (p) p.Intensity = 1.5f;
        }

        protected override IEnumerator Round()
        {
            var pool = new List<MonsterAttack> { _flower, _cross, _bomb, _ring, _laser };
            pool.RemoveAll(a => a == null || a == _last);
            var next = pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : _laser;
            _last = next;
            if (Phase > 1 && Random.value < 0.5f)
                yield return Use(_teleport);          // ambush from somewhere new
            yield return Use(next);
            if (++_count % 3 == 0 && Phase == 1)
                yield return Use(_teleport);
            yield return Hover(keepAt.x, keepAt.y, Phase == 1 ? Random.Range(1.2f, 2f) : Random.Range(0.5f, 1f));
        }
    }
}
