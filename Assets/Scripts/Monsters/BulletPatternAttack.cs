using System.Collections;
using UnityEngine;

namespace Roguelike
{
    /// Bullet patterns, channelled while the attack's state (a looping cast) plays; the wall is the muscle
    /// boss's barrage, the cross and flower the skull face's:
    /// - Wall: a straight line of evenly spaced bullets that sweeps across the target from the north, south,
    ///   east or west, with a random gap of 1-3 missing bullets to slip through; several walls in a row, the
    ///   gap moving each time.
    /// - Cross: four bullets at once, 90° apart, again and again, the firing angle turning a little each
    ///   set, so four rotating streams spiral out (each bullet itself flies straight).
    /// - Flower: a ring of 8-12 bullets that swing from side to side as they fly out, each starting its
    ///   swing at a different point by its place in the ring, which draws petals; repeated, the ring turned
    ///   a little each burst.
    /// Intensity (set by the boss when it enrages) means more walls/sets/bursts, closer together.
    public class BulletPatternAttack : MonsterAttack
    {
        public enum Pattern { Wall, Cross, Flower }

        [SerializeField] Pattern pattern;
        [SerializeField] Projectile projectile;
        [SerializeField] float speed = 4.5f;
        [SerializeField] float projectileScale = 1f;
        [SerializeField] float lifetime = 5f;
        [Tooltip("Bullets fly at this height above the monster's feet (m).")]
        [SerializeField] float height = 0.6f;
        [Tooltip("Walls / sets of four / bursts.")]
        [SerializeField] int repeats = 3;
        [Tooltip("Seconds between them.")]
        [SerializeField] float interval = 1f;
        [Tooltip("Build-up before the first, facing the target (s).")]
        [SerializeField] float windup = 0.4f;
        [SerializeField] GameObject spawnPuff;
        [Header("Wall")]
        [SerializeField] int wallBullets = 15;
        [SerializeField] float wallSpacing = 0.75f;
        [SerializeField] Vector2Int gap = new Vector2Int(1, 3);
        [Tooltip("How far from the target (m) the wall starts.")]
        [SerializeField] float wallDistance = 7f;
        [Header("Cross")]
        [Tooltip("Degrees the cross turns between sets.")]
        [SerializeField] float crossTurn = 9f;
        [Header("Flower")]
        [SerializeField] Vector2Int petals = new Vector2Int(8, 12);
        [SerializeField] float waveAmplitude = 0.55f;
        [SerializeField] float waveFrequency = 1.1f;
        [Tooltip("Degrees the ring turns between bursts.")]
        [SerializeField] float burstTurn = 15f;

        public Pattern Kind => pattern;
        public float Intensity { get; set; } = 1f;
        /// Bullets fired by the last run, for tests.
        public int Fired { get; private set; }

        public override IEnumerator Perform()
        {
            Fired = 0;
            M.Play(state, 0.15f);
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
                M.FaceTarget(1.5f);
                yield return null;
            }
            int n = Mathf.Max(1, Mathf.RoundToInt(repeats * Intensity));
            float wait = interval / Mathf.Max(Intensity, 0.1f);
            float angle = transform.eulerAngles.y;
            int lastDir = -1;
            for (int i = 0; i < n; i++)
            {
                switch (pattern)
                {
                    case Pattern.Wall:
                        lastDir = Wall(lastDir);
                        break;
                    case Pattern.Cross:
                        for (int q = 0; q < 4; q++)
                            Fire(Origin(), Quaternion.Euler(0f, angle + q * 90f, 0f) * Vector3.forward);
                        angle += crossTurn;
                        break;
                    case Pattern.Flower:
                        int count = Random.Range(petals.x, petals.y + 1);
                        for (int k = 0; k < count; k++)
                        {
                            var p = Fire(Origin(), Quaternion.Euler(0f, angle + k * 360f / count, 0f) * Vector3.forward);
                            // two swings round the ring: neighbours swing against each other, petals form between
                            p?.Wave(waveAmplitude, waveFrequency, k * 4f * Mathf.PI / count);
                        }
                        angle += burstTurn;
                        break;
                }
                for (float t = 0f; t < wait; t += Time.deltaTime)
                {
                    if (pattern != Pattern.Cross) M.FaceTarget(0.5f);
                    yield return null;
                }
            }
        }

        Vector3 Origin() => transform.position + Vector3.up * height + transform.forward * M.Radius * 0.5f;

        Projectile Fire(Vector3 from, Vector3 dir)
        {
            if (projectile == null)
                return null;
            Fired++;
            return Projectile.Spawn(projectile, from, dir * speed, damage, transform, 0f, 0f, projectileScale)
                             .Cruise(from.y).Life(lifetime);
        }

        /// One wall from a cardinal direction other than the last one's; returns the direction used.
        int Wall(int lastDir)
        {
            Vector3[] dirs = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            int d = Random.Range(0, 4);
            if (d == lastDir) d = (d + Random.Range(1, 4)) % 4;
            var travel = dirs[d];
            var across = Vector3.Cross(Vector3.up, travel);
            var centre = TargetPoint - travel * wallDistance;
            centre.y = transform.position.y + height;
            int gapLen = Random.Range(gap.x, gap.y + 1);
            int mid = wallBullets / 2;
            // the gap lands somewhere near the target's line, never off the end of the wall
            int gapStart = Mathf.Clamp(mid + Random.Range(-3, 4) - gapLen / 2, 1, wallBullets - gapLen - 1);
            for (int k = 0; k < wallBullets; k++)
            {
                if (k >= gapStart && k < gapStart + gapLen)
                    continue;
                var at = centre + across * ((k - (wallBullets - 1) * 0.5f) * wallSpacing);
                if (Physics.CheckSphere(at, 0.15f * MonsterScale.Factor, ~0, QueryTriggerInteraction.Ignore))
                    continue;                      // inside a wall
                Fire(at, travel);
                if (spawnPuff && k % 3 == 0)
                {
                    var fx = Instantiate(spawnPuff, at, Quaternion.identity);
                    fx.transform.localScale = Vector3.one * 0.5f * MonsterScale.Factor;
                    Destroy(fx, 2f);
                }
            }
            return d;
        }
    }
}
