using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Roguelike.Tests
{
    /// The movement/attack patterns: each monster left to its brain against a stand-in player does what its
    /// kind is meant to do.
    public class MonsterBehaviourTests : MonsterTestBase
    {
        static readonly (string name, System.Type brain)[] Brains =
        {
            ("BallShadow", typeof(WanderBrain)), ("TwistedBall", typeof(WanderBrain)),
            ("Regular", typeof(RangedBrain)), ("ThreeEyed", typeof(RangedBrain)), ("Crescent", typeof(RangedBrain)),
            ("Ghost", typeof(ChaseBrain)), ("Ghoul", typeof(ChaseBrain)), ("Horned", typeof(SkirmisherBrain)), ("Claw", typeof(DartBrain)),
            ("Muscle", typeof(ChargerBrain)),
            ("Fat", typeof(FatBoss)), ("MuscleBoss", typeof(MuscleBoss)), ("SkullFace", typeof(SkullFaceBoss)),
        };

        static IEnumerable<string> Names => Brains.Select(b => b.name);
        static IEnumerable<string> Bosses => new[] { "Fat", "MuscleBoss", "SkullFace" };

        [Test]
        public void HasItsBrain([ValueSource(nameof(Names))] string name)
        {
            var want = Brains.First(b => b.name == name).brain;
            var brains = LoadPrefab(name).GetComponents<MonsterBrain>();
            Assert.AreEqual(1, brains.Length, $"{name}: one brain expected");
            Assert.AreEqual(want, brains[0].GetType());
        }

        /// Runs the scene for `seconds`, logging every attack the monster starts and every hit on the dummy.
        class Watch
        {
            public readonly List<string> Attacks = new List<string>();
            public int Hits;
            public float MinDist = float.PositiveInfinity, MaxDist;
            public float Travelled;
        }

        static IEnumerator Observe(Monster m, Health dummy, float seconds, Watch w, System.Func<bool> until = null)
        {
            dummy.Damaged += (d, from) => w.Hits++;
            MonsterAttack last = null;
            var prev = m.transform.position;
            for (float end = Time.time + seconds; Time.time < end && (until == null || !until());)
            {
                if (m.CurrentAttack && m.CurrentAttack != last) w.Attacks.Add(m.CurrentAttack.State);
                last = m.CurrentAttack;
                float d = Monster.Flat(dummy.transform.position - m.transform.position).magnitude;
                w.MinDist = Mathf.Min(w.MinDist, d);
                w.MaxDist = Mathf.Max(w.MaxDist, d);
                if (!(m.CurrentAttack is TeleportAttack))       // a blink is not walking about
                    w.Travelled += Vector3.Distance(prev, m.transform.position);
                prev = m.transform.position;
                yield return null;
            }
        }

        (Monster, Health) Setup(string name, float dist)
        {
            Floor();
            var go = Spawn(name, Vector3.zero);
            var m = go.GetComponent<Monster>();
            var dummy = Dummy(new Vector3(0f, 0f, dist * S));
            m.Target = dummy.transform;
            return (m, dummy);
        }

        [UnityTest]
        public IEnumerator BallWandersAboutAndLeavesADistantPlayerAlone()
        {
            var (m, dummy) = Setup("BallShadow", 7.5f);
            var w = new Watch();
            var spots = new List<Vector3>();
            for (int i = 0; i < 8; i++)
            {
                yield return Observe(m, dummy, 1f, w);
                spots.Add(m.transform.position);
            }
            Assert.Greater(w.Travelled, 1.5f * S, "it should roam about");
            Assert.Greater(spots.Max(p => Vector3.Distance(p, spots[0])), 0.6f * S, "it should not just jitter in place");
            Assert.Less(spots.Max(p => Monster.Flat(p).magnitude), 5f * S, "it should stay around where it started");
            Assert.IsEmpty(w.Attacks, "a ball only lashes out when the player walks right into it");
        }

        [UnityTest]
        public IEnumerator TwistedBallChargesAPlayerThatComesNear()
        {
            var (m, dummy) = Setup("TwistedBall", 3.5f);
            var w = new Watch();
            yield return Observe(m, dummy, 6f, w, () => w.Hits > 0);
            Assert.Contains("Lunge", w.Attacks, "it should rush in and lunge");
            Assert.Greater(w.Hits, 0, "the lunge should land");
        }

        [UnityTest]
        public IEnumerator BrawlersFollowAndHit([Values("Ghost", "Ghoul")] string name)
        {
            var (m, dummy) = Setup(name, 7f);
            var w = new Watch();
            yield return Observe(m, dummy, 10f, w, () => w.Hits > 0);
            Assert.Less(w.MinDist, 1.6f * S, "it should follow the player");
            Assert.Greater(w.Hits, 0, "and hit it");
        }

        [UnityTest]
        public IEnumerator CastersShootFromAfarAndMoveOnlyNowAndThen([Values("Regular", "ThreeEyed", "Crescent")] string name)
        {
            var (m, dummy) = Setup(name, 6.5f);
            var w = new Watch();
            yield return Observe(m, dummy, 9f, w);
            Assert.Contains("Shoot", w.Attacks);
            Assert.Greater(w.Hits, 0, "bolts should land");
            Assert.Greater(w.MinDist, 3f * S, "it should keep its distance");
            Assert.Less(w.Travelled, 7f * S, "it should mostly stay put");
        }

        [UnityTest]
        public IEnumerator HornedShootsThenGoesInToPunch()
        {
            var (m, dummy) = Setup("Horned", 6f);
            var w = new Watch();
            yield return Observe(m, dummy, 16f, w, () => w.Attacks.Contains("Shoot") && w.Attacks.Contains("Punch"));
            Assert.Contains("Shoot", w.Attacks, "it should shoot");
            Assert.Contains("Punch", w.Attacks, "and close in to punch");
        }

        static void SetField(Object target, string field, float value)
        {
#if UNITY_EDITOR
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
        }

        static List<Projectile> Live() => Object.FindObjectsByType<Projectile>().ToList();

        [UnityTest]
        public IEnumerator ClawDartsAboutFastAndUsesShotsAndClaws()
        {
            var (m, dummy) = Setup("Claw", 5f);
            var w = new Watch();
            yield return Observe(m, dummy, 20f, w, () => w.Attacks.Contains("Shoot") && w.Attacks.Contains("Slash"));
            Assert.Contains("Shoot", w.Attacks, "it should shoot now and then");
            Assert.Contains("Slash", w.Attacks, "and dive in with its claws");
            Assert.Greater(w.Travelled, 8f * S, "it should keep darting about");
        }

        [UnityTest]
        public IEnumerator ThreeEyedFiresThreeAndTeleports()
        {
            var (m, dummy) = Setup("ThreeEyed", 6f);
            m.GetComponent<MonsterBrain>().enabled = false;
            yield return null;
            m.Run(m.Attack<ProjectileAttack>());
            int most = 0;
            while (m.Busy)
            {
                most = Mathf.Max(most, Live().Count);
                yield return null;
            }
            Assert.AreEqual(3, most, "one shot is a fan of three bolts");
            m.GetComponent<MonsterBrain>().enabled = true;
            dummy.transform.position = new Vector3(0f, 0f, 2f * S);   // too close for comfort
            var w = new Watch();
            yield return Observe(m, dummy, 12f, w, () => w.Attacks.Contains("Teleport"));
            Assert.Contains("Teleport", w.Attacks, "it should blink away to a new spot");
        }

        [UnityTest]
        public IEnumerator CrescentsBoomerangComesBack()
        {
            var (m, dummy) = Setup("Crescent", 4f);
            m.GetComponent<MonsterBrain>().enabled = false;
            int hits = 0;
            dummy.Damaged += (d, from) => hits++;
            yield return null;
            m.Run(m.Attack<ProjectileAttack>());
            Projectile blade = null;
            float far = 0f;
            bool returned = false;
            for (float end = Time.time + 8f; Time.time < end;)
            {
                blade = blade ? blade : Object.FindAnyObjectByType<Projectile>();
                if (blade)
                {
                    far = Mathf.Max(far, Monster.Flat(blade.transform.position - m.transform.position).magnitude);
                    returned |= blade.Returning;
                }
                else if (returned)
                    break;                         // caught again
                yield return null;
            }
            Assert.IsTrue(returned, "it should turn back");
            Assert.Greater(far, 4.5f * S, "after flying out past the target");
            Assert.IsNull(Object.FindAnyObjectByType<Projectile>(), "and be caught by its thrower");
            Assert.GreaterOrEqual(hits, 1, "cutting the target on the way");
        }

        [UnityTest]
        public IEnumerator FatsSpitHatchesABallOrBlastsAShotgun()
        {
            var (m, dummy) = Setup("Fat", 6f);
            m.GetComponent<MonsterBrain>().enabled = false;
            var spit = m.Attack<SpitAttack>();
            SetField(spit, "summonChance", 1f);
            yield return null;
            m.Run(spit);
            yield return new WaitUntil(() => !m.Busy);
            for (float end = Time.time + 3f; Time.time < end && Object.FindObjectsByType<WanderBrain>().Length == 0;)
                yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<WanderBrain>().Length, "the glob should hatch a shadow ball");

            SetField(spit, "summonChance", 0f);
            m.Run(spit);
            int most = 0;
            for (float end = Time.time + 3f; Time.time < end;)
            {
                most = Mathf.Max(most, Live().Count);
                yield return null;
            }
            Assert.That(most, Is.InRange(6, 9), "a shotgun blast of 6-9 globs");
        }

        [UnityTest]
        public IEnumerator FatsLandingCanShootTheFourWays()
        {
            var (m, dummy) = Setup("Fat", 5f);
            m.GetComponent<MonsterBrain>().enabled = false;
            var jump = m.Attack<JumpSmashAttack>();
            SetField(jump, "landingShotChance", 1f);
            yield return null;
            m.Run(jump);
            List<Projectile> shots = null;
            for (float end = Time.time + 4f; Time.time < end && (shots == null || shots.Count == 0);)
            {
                shots = Live();
                yield return null;
            }
            Assert.AreEqual(4, shots.Count, "four shots on landing");
            var dirs = shots.Select(p => Monster.Flat(p.Velocity).normalized).ToList();
            foreach (var want in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                Assert.IsTrue(dirs.Any(d => Vector3.Dot(d, want) > 0.99f), $"one heading {want}");
        }

        [UnityTest]
        public IEnumerator FatsFlailClosesInThenStops()
        {
            var (m, dummy) = Setup("Fat", 4f);
            m.GetComponent<MonsterBrain>().enabled = false;
            yield return null;
            m.Run(m.Attack<MeleeAttack>("Flail"));
            float start = m.TargetDistance;
            float len = m.ClipLength("Flail");
            yield return new WaitForSeconds(len * 0.7f);
            float mid = m.TargetDistance;
            yield return new WaitForSeconds(len * 0.25f);
            Assert.Less(mid, start - 1.5f * S, "it should shuffle in while flailing");
            Assert.AreEqual(mid, m.TargetDistance, 0.05f * S, "then stop");
        }

        [UnityTest]
        public IEnumerator BulletPatterns([Values(BulletPatternAttack.Pattern.Wall, BulletPatternAttack.Pattern.Cross,
                                                  BulletPatternAttack.Pattern.Flower)] BulletPatternAttack.Pattern kind)
        {
            // the wall barrage is the muscle boss's, the cross and flower the skull face's
            var (m, dummy) = Setup(kind == BulletPatternAttack.Pattern.Wall ? "MuscleBoss" : "SkullFace", 6f);
            m.GetComponent<MonsterBrain>().enabled = false;
            yield return null;
            var attack = m.Attacks.OfType<BulletPatternAttack>().First(a => a.Kind == kind);
            m.Run(attack);
            var start = new Dictionary<Projectile, (Vector3 pos, Vector3 dir, float t)>();
            bool swayed = false;
            while (m.Busy)
            {
                foreach (var p in Live())
                {
                    if (!start.ContainsKey(p))
                        start[p] = (p.transform.position, Monster.Flat(p.Velocity).normalized, Time.time);
                    // how far it has drifted off the straight line it set off along
                    var (from, dir, t0) = start[p];
                    var off = Monster.Flat(p.transform.position - from);
                    if (Time.time - t0 > 0.3f && (off - Vector3.Project(off, dir)).magnitude > 0.2f * S) swayed = true;
                }
                yield return null;
            }
            switch (kind)
            {
                case BulletPatternAttack.Pattern.Wall:
                    Assert.That(attack.Fired, Is.InRange(2 * 12, 2 * 14), "two walls of 15, each missing a 1-3 gap");
                    break;
                case BulletPatternAttack.Pattern.Cross:
                    Assert.AreEqual(24 * 4, attack.Fired, "24 sets of four");
                    Assert.IsFalse(swayed, "cross bullets fly straight");
                    break;
                case BulletPatternAttack.Pattern.Flower:
                    Assert.That(attack.Fired, Is.InRange(4 * 8, 4 * 12), "four rings of 8-12");
                    Assert.IsTrue(swayed, "flower bullets swing from side to side");
                    break;
            }
        }

        [UnityTest]
        public IEnumerator MuscleChargesAndPunches()
        {
            var attacks = LoadPrefab("Muscle").GetComponents<MonsterAttack>();
            Assert.AreEqual(2, attacks.Length, "a charge and a punch, nothing else");
            var (m, dummy) = Setup("Muscle", 7f);
            var w = new Watch();
            yield return Observe(m, dummy, 20f, w, () => w.Attacks.Contains("Charge") && w.Attacks.Contains("Punch"));
            Assert.Contains("Charge", w.Attacks, "it should charge the player");
            Assert.Contains("Punch", w.Attacks, "and punch it up close");
        }

        [UnityTest]
        public IEnumerator MuscleBossSpinShootsTwiceFourWays()
        {
            var (m, dummy) = Setup("MuscleBoss", 6f);
            m.GetComponent<MonsterBrain>().enabled = false;
            yield return null;
            var slam = m.Attack<SpinAttack>();
            m.Run(slam);
            var dirs = new HashSet<Vector3Int>();
            while (m.Busy)
            {
                foreach (var p in Live())
                {
                    var d = Monster.Flat(p.Velocity).normalized;
                    dirs.Add(new Vector3Int(Mathf.RoundToInt(d.x), 0, Mathf.RoundToInt(d.z)));
                }
                yield return null;
            }
            Assert.AreEqual(8, slam.Fired, "two volleys of four while spinning");
            Assert.AreEqual(4, dirs.Count, "north, east, south and west");
        }

        [UnityTest]
        public IEnumerator MuscleBossWallHitShootsEightWays()
        {
            var (m, dummy) = Setup("MuscleBoss", 5f);
            m.GetComponent<MonsterBrain>().enabled = false;
            dummy.transform.position = new Vector3(0f, 0f, 30f * S);   // charge past it...
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);  // ...into a wall
            wall.transform.position = new Vector3(0f, 1.25f, 6f) * S;
            wall.transform.localScale = new Vector3(8f, 2.5f, 0.5f) * S;
            _spawned.Add(wall);
            yield return null;
            var charge = m.Attack<ChargeAttack>();
            m.Run(charge);
            int most = 0;
            while (m.Busy)
            {
                most = Mathf.Max(most, Live().Count);
                yield return null;
            }
            Assert.IsTrue(charge.HitWall, "it should hit the wall");
            Assert.GreaterOrEqual(most, 5, "bolts fly off all round (those spawned into the wall die at once)");
        }

        [UnityTest]
        public IEnumerator ClawBurstsEightWays()
        {
            var (m, dummy) = Setup("Claw", 3f);
            m.GetComponent<MonsterBrain>().enabled = false;
            yield return null;
            var burst = m.Attack<RadialShotAttack>();
            m.Run(burst);
            yield return new WaitUntil(() => !m.Busy);
            Assert.AreEqual(8, burst.Fired);
            Assert.IsNotNull(m.Attack<TeleportAttack>(), "it teleports too");
        }

        [UnityTest]
        public IEnumerator SkullsSmokeBombBurstsIntoEight()
        {
            var (m, dummy) = Setup("SkullFace", 6f);
            m.GetComponent<MonsterBrain>().enabled = false;
            dummy.GetComponent<CharacterController>().enabled = false;   // let it land on the floor
            yield return null;
            m.Run(m.Attack<ProjectileAttack>("Throw"));
            int most = 0;
            for (float end = Time.time + 5f; Time.time < end;)
            {
                most = Mathf.Max(most, Live().Count);
                yield return null;
            }
            Assert.AreEqual(8, most, "a ring of eight once the bomb bursts");
        }

        [UnityTest]
        public IEnumerator FatKeepsAwayAndSpits()
        {
            var (m, dummy) = Setup("Fat", 4f);
            var w = new Watch();
            yield return Observe(m, dummy, 2.5f, w);
            Assert.Greater(Monster.Flat(dummy.transform.position - m.transform.position).magnitude, 4.8f * S, "it should back away first");
            yield return Observe(m, dummy, 12f, w, () => w.Attacks.Contains("Spit") && w.Attacks.Contains("Jump"));
            Assert.Contains("Spit", w.Attacks);
            Assert.Contains("Jump", w.Attacks, "it should bounce onto the player now and then");
        }

        [UnityTest]
        public IEnumerator BossesRunTheirPatternAndEnrage([ValueSource(nameof(Bosses))] string name)
        {
            var (m, dummy) = Setup(name, 7f);
            var boss = m.GetComponent<BossBrain>();
            var w = new Watch();
            yield return Observe(m, dummy, 20f, w, () => w.Attacks.Distinct().Count() >= 2);
            Assert.IsTrue(boss.InFight, "the fight should have started");
            Assert.GreaterOrEqual(w.Attacks.Distinct().Count(), 2, $"{name} used only {string.Join(", ", w.Attacks)}");
            Assert.AreEqual(1, boss.Phase);

            var hp = m.GetComponent<Health>();
            hp.TakeDamage(hp.Max * 0.55f, dummy.transform.position);
            for (float end = Time.time + 12f; Time.time < end && (boss.Phase < 2 || boss.Step == "Enrage");)
                yield return null;
            Assert.AreEqual(2, boss.Phase, "it should enrage below half health");
            Assert.AreNotEqual("Enrage", boss.Step, "the enrage roar should be short");
            var w2 = new Watch();
            yield return Observe(m, dummy, 20f, w2, () => w2.Attacks.Count >= 2);
            Assert.GreaterOrEqual(w2.Attacks.Count, 2, $"{name} should keep attacking once enraged");
            Assert.IsFalse(hp.Invulnerable, "the enrage roar must not leave it invulnerable");
        }
    }
}
