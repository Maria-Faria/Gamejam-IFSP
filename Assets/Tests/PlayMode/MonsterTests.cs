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
    /// Spawning helpers shared by the monster tests: everything spawned is destroyed after each test.
    public abstract class MonsterTestBase
    {
        protected const string PrefabDir = "Assets/Prefabs/Monsters";
        /// The monsters are scaled by this against the game's player (MonsterScale): distances in the tests too.
        protected const float S = MonsterScale.Factor;

        protected readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o) Object.Destroy(o);
            _spawned.Clear();
            foreach (var p in Object.FindObjectsByType<Projectile>())
                Object.Destroy(p.gameObject);
            foreach (var m in Object.FindObjectsByType<Monster>())          // summoned ones too
                Object.Destroy(m.gameObject);
        }

        protected static GameObject LoadPrefab(string name)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");
#else
            return null;
#endif
        }

        protected GameObject Spawn(string name, Vector3 at)
        {
            var prefab = LoadPrefab(name);
            Assert.IsNotNull(prefab, $"prefab {name} not found in {PrefabDir}");
            var go = Object.Instantiate(prefab, at, Quaternion.identity);
            _spawned.Add(go);
            return go;
        }

        protected GameObject Floor()
        {
            var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
            f.transform.position = new Vector3(0f, -0.15f, 0f);
            f.transform.localScale = new Vector3(60f * S, 0.3f, 60f * S);
            _spawned.Add(f);
            return f;
        }

        /// A player stand-in: the game player's CharacterController (SampleScene: height 2, radius 0.5), with health.
        protected Health Dummy(Vector3 at)
        {
            var go = new GameObject("Dummy");
            go.transform.position = at;
            var cc = go.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.5f;
            var h = go.AddComponent<Health>();
            h.Invulnerable = true;
            _spawned.Add(go);
            return h;
        }
    }

    /// Every monster prefab is sized for its class relative to the game's player, has its clips, and every
    /// one of its attacks plays through and lands on a target standing in range.
    public class MonsterTests : MonsterTestBase
    {
        const float ReferenceSize = MonsterScale.PlayerSize;     // the game's player (Annie Easley), largest side

        // name, size class (x reference), clips
        public static readonly object[][] Monsters =
        {
            new object[] { "BallShadow", 0.5f, new[] { "Idle", "Move", "Lunge" } },
            new object[] { "TwistedBall", 0.5f, new[] { "Idle", "Move", "Lunge" } },
            new object[] { "Regular", 0.75f, new[] { "Idle", "Move", "Shoot" } },
            new object[] { "ThreeEyed", 0.75f, new[] { "Idle", "Move", "Shoot", "Teleport" } },
            new object[] { "Ghost", 0.75f, new[] { "Idle", "Move", "Punch" } },
            new object[] { "Crescent", 1f, new[] { "Idle", "Move", "Shoot" } },
            new object[] { "Ghoul", 1f, new[] { "Idle", "Move", "Punch" } },
            new object[] { "Horned", 1f, new[] { "Idle", "Move", "Punch", "Shoot" } },
            new object[] { "Claw", 0.75f, new[] { "Idle", "Move", "Shoot", "Slash", "Teleport" } },
            new object[] { "Fat", 1.7f, new[] { "Idle", "Move", "Flail", "Jump", "Spit" } },
            new object[] { "Muscle", 1f, new[] { "Idle", "Move", "Charge", "ChargeWallHit", "Punch" } },
            new object[] { "MuscleBoss", 1.8f, new[] { "Idle", "Move", "Charge", "ChargeWallHit", "Spin", "SpinDizzy", "Cast" } },
            new object[] { "SkullFace", 2f, new[] { "Idle", "Move", "ChargedLaser", "Teleport", "SpinShoot", "Throw", "Cast" } },
        };

        public static IEnumerable<string> Names => Monsters.Select(m => (string)m[0]);

        [UnityTest]
        public IEnumerator IsSizedForItsClass([ValueSource(nameof(Names))] string name)
        {
            var spec = Monsters.First(m => (string)m[0] == name);
            float want = (float)spec[1] * ReferenceSize;
            var go = Spawn(name, Vector3.zero);
            go.GetComponent<Monster>().enabled = false;
            yield return null;
            go.GetComponent<Animator>().enabled = false;        // measure the rest pose
            yield return null;
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            smr.BakeMesh(mesh, true);
            var toWorld = smr.transform.localToWorldMatrix;
            var verts = mesh.vertices.Select(p => toWorld.MultiplyPoint3x4(p)).ToArray();
            Object.Destroy(mesh);
            var b = new Bounds(verts[0], Vector3.zero);
            foreach (var p in verts) b.Encapsulate(p);
            Debug.Log($"{name}: {b.min} .. {b.max}");
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            Assert.That(size, Is.InRange(want * 0.9f, want * 1.12f), $"{name}: largest side {size:0.00} m, want {want:0.00} m");
            Assert.That(b.min.y, Is.InRange(-0.08f * S, 0.25f * want), $"{name}: lowest point {b.min.y:0.00} m should be at/just above the floor");
        }

        [Test]
        public void HasItsClipsAndEvents([ValueSource(nameof(Names))] string name)
        {
            var spec = Monsters.First(m => (string)m[0] == name);
            var go = LoadPrefab(name);
            Assert.IsNotNull(go);
            var animator = go.GetComponent<Animator>();
            Assert.IsNotNull(animator.runtimeAnimatorController, "no animator controller");
            var clips = animator.runtimeAnimatorController.animationClips;
            foreach (var clip in (string[])spec[2])
                Assert.IsTrue(clips.Any(c => c.name == clip), $"{name}: clip {clip} missing (has {string.Join(", ", clips.Select(c => c.name))})");
            Assert.IsTrue(clips.First(c => c.name == "Idle").isLooping, "Idle must loop");
            Assert.IsTrue(clips.First(c => c.name == "Move").isLooping, "Move must loop");
            foreach (var attack in go.GetComponents<MonsterAttack>())
            {
                var clip = clips.First(c => c.name == attack.State);
                if (attack is ChargeAttack || attack is SpinAttack || attack is BulletPatternAttack)
                    continue;                       // driven by code, no events needed
                Assert.IsTrue(clip.events.Any(e => e.functionName == "MonsterEvent"), $"{name}: {clip.name} has no MonsterEvent animation events");
            }
            Assert.IsNotNull(go.GetComponentInChildren<ParticleSystem>(), $"{name}: no smoke");
            var mats = go.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials;
            Assert.AreEqual("Roguelike/Ghostly", mats[0].shader.name);
            Assert.AreEqual("Roguelike/GhostSmoke", mats[1].shader.name);
        }

        /// Renders every monster idling and mid-attack (TestScreenshots/Monsters), next to a player character
        /// for scale. Not an assertion: for looking at.
        [UnityTest, Explicit]
        public IEnumerator CaptureScreenshots()
        {
            Floor();
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.shadows = LightShadows.Soft;
            _spawned.Add(sun.gameObject);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.86f, 0.83f, 0.81f);
            cam.fieldOfView = 30f;
            _spawned.Add(cam.gameObject);
            var rt = new RenderTexture(640, 640, 24);
            cam.targetTexture = rt;
            var dir = System.IO.Path.Combine(Application.dataPath, "..", "TestScreenshots", "Monsters");
            System.IO.Directory.CreateDirectory(dir);
#if UNITY_EDITOR
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Scientists/AnnieEasley/annie_easley-compressed.obj");
#else
            GameObject reference = null;
#endif
            foreach (var name in Names)
            {
                var go = Spawn(name, Vector3.zero);
                var m = go.GetComponent<Monster>();
                m.enabled = false;
                GameObject person = null;
                if (reference)
                {
                    person = Object.Instantiate(reference, new Vector3(-m.Size * 0.6f - 0.7f * S, 0f, 0.3f), Quaternion.Euler(0f, 160f, 0f));
                    person.transform.localScale = Vector3.one * 1.63f;      // as under the Player in SampleScene
                }
                float h = Mathf.Max(m.Size, MonsterScale.PlayerSize);
                cam.transform.position = new Vector3(-h * 0.55f, h * 0.9f, -h * 3.4f);
                cam.transform.LookAt(new Vector3(-h * 0.2f, h * 0.45f, 0f));
                go.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
                for (float t = 0f; t < 1.5f; t += Time.deltaTime) yield return null;
                Save(cam, rt, dir, $"{name}_Idle");
                foreach (var attack in go.GetComponents<MonsterAttack>())
                {
                    foreach (float k in new[] { 0.3f, 0.55f })
                    {
                        m.Animator.CrossFadeInFixedTime(attack.State, 0f, 0, 0f);
                        float until = m.ClipLength(attack.State) * k;
                        for (float t = 0f; t < until; t += Time.deltaTime) yield return null;
                        Save(cam, rt, dir, $"{name}_{attack.State}_{(int)(k * 100)}");
                    }
                }
                Object.Destroy(go);
                if (person) Object.Destroy(person);
                yield return null;
            }
            cam.targetTexture = null;
            rt.Release();
        }

        /// Renders the casters a moment after each "Fire" (TestScreenshots/Monsters/*_Firing.png): muzzle flash
        /// and bolt in flight. Not an assertion: for looking at.
        [UnityTest, Explicit]
        public IEnumerator CaptureFiring([Values("Regular", "ThreeEyed", "Crescent", "Horned", "Claw", "Fat", "SkullFace")] string name)
        {
            Floor();
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            _spawned.Add(sun.gameObject);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.86f, 0.83f, 0.81f);
            cam.fieldOfView = 35f;
            _spawned.Add(cam.gameObject);
            var rt = new RenderTexture(640, 640, 24);
            cam.targetTexture = rt;
            var dir = System.IO.Path.Combine(Application.dataPath, "..", "TestScreenshots", "Monsters");
            var go = Spawn(name, Vector3.zero);
            go.GetComponent<MonsterBrain>().enabled = false;
            var m = go.GetComponent<Monster>();
            var target = Dummy(new Vector3(0f, 0f, 4f * S));
            m.Target = target.transform;
            float h = Mathf.Max(m.Size, MonsterScale.PlayerSize);
            cam.tag = "MainCamera";                 // the bolt halos turn to face it
            cam.transform.position = new Vector3(h * 1.6f + 2.2f, h * 0.9f + 1.0f, 1.6f - h * 0.8f);
            cam.transform.LookAt(new Vector3(0f, h * 0.45f, 1.7f));
            int fired = 0;
            m.AnimationEvent += e => { if (e == "Fire") fired++; };
            yield return null;
            m.Run(m.Attacks.FirstOrDefault(a => a.State == "Throw") ??
                  m.Attacks.First(a => a is ProjectileAttack || a is SpitAttack || a is RadialShotAttack));
            while (fired == 0 && m.Busy) yield return null;
            for (int i = 0; i < 4; i++) yield return null;
            Save(cam, rt, dir, $"{name}_Firing");
            for (float t = 0f; t < 0.35f; t += Time.deltaTime) yield return null;
            Save(cam, rt, dir, $"{name}_Firing2");
            cam.targetTexture = null;
            rt.Release();
        }

        /// The skull face's bullet patterns and smoke bomb seen from above, the way the game camera sees them
        /// (TestScreenshots/Monsters/Pattern_*.png). Not an assertion: for looking at.
        [UnityTest, Explicit]
        public IEnumerator CapturePatterns([Values("Wall", "Cross", "Flower", "Throw")] string which)
        {
            Floor();
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            _spawned.Add(sun.gameObject);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.56f, 0.6f);
            cam.fieldOfView = 45f;
            cam.transform.position = new Vector3(0f, 11f, -6f) * S;
            cam.transform.LookAt(new Vector3(0f, 0f, 3f) * S);
            _spawned.Add(cam.gameObject);
            var rt = new RenderTexture(720, 720, 24);
            cam.targetTexture = rt;
            var dir = System.IO.Path.Combine(Application.dataPath, "..", "TestScreenshots", "Monsters");
            var go = Spawn("SkullFace", Vector3.zero);
            go.GetComponent<MonsterBrain>().enabled = false;
            var m = go.GetComponent<Monster>();
            var target = Dummy(new Vector3(0f, 0f, 5f * S));
            m.Target = target.transform;
            yield return null;
            m.Run(which == "Throw" ? m.Attack<ProjectileAttack>("Throw")
                                   : m.Attacks.OfType<BulletPatternAttack>().First(a => a.Kind.ToString() == which));
            foreach (var at in new[] { 1.2f, 2.0f, 2.8f })
            {
                for (float end = Time.time + (at == 1.2f ? 1.2f : 0.8f); Time.time < end;) yield return null;
                Save(cam, rt, dir, $"Pattern_{which}_{(int)(at * 10)}");
            }
            cam.targetTexture = null;
            rt.Release();
        }

        static void Save(Camera cam, RenderTexture rt, string dir, string file)
        {
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, file + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator EveryAttackLands([ValueSource(nameof(Names))] string name)
        {
            Floor();
            var prefab = LoadPrefab(name);
            Assert.IsNotNull(prefab);
            int count = prefab.GetComponents<MonsterAttack>().Length;
            Assert.Greater(count, 0, $"{name} has no attacks");
            for (int i = 0; i < count; i++)
            {
                var go = Spawn(name, Vector3.zero);
                go.GetComponent<MonsterBrain>().enabled = false;     // only the attack under test
                var m = go.GetComponent<Monster>();
                var attack = go.GetComponents<MonsterAttack>()[i];
                float size = m.Size;
                var body = go.GetComponent<CapsuleCollider>();
                float dist = Mathf.Max(attack.MinRange + 0.3f * S, Mathf.Min(attack.MaxRange * 0.6f, body.radius + 0.5f + size * 0.4f));
                if (attack is ChargeAttack) dist = 5f * S;
                var target = Dummy(new Vector3(0f, 0f, dist));
                int hits = 0;
                target.Damaged += (d, from) => hits++;
                m.Target = target.transform;
                yield return null;
                Assert.IsTrue(m.Run(attack), $"{name}: could not start {attack.State}");
                var start = go.transform.position;
                bool sawProjectile = false;
                float timeout = Time.time + 12f;
                while (m.Busy && Time.time < timeout)
                {
                    sawProjectile |= Object.FindAnyObjectByType<Projectile>() != null;
                    yield return null;
                }
                Assert.IsFalse(m.Busy, $"{name}: {attack.State} never finished");
                // let bolts/globs in flight arrive
                for (float t = 0f; t < 1.5f && hits == 0; t += Time.deltaTime)
                    yield return null;

                if (attack is TeleportAttack)
                    Assert.Greater(Vector3.Distance(start, go.transform.position), 1f * S, $"{name}: Teleport did not move");
                else if (attack is ProjectileAttack p && p.AimMode == ProjectileAttack.Aim.Outward)
                    Assert.IsTrue(sawProjectile, $"{name}: {attack.State} fired nothing");
                else if (attack is BulletPatternAttack bp)
                    Assert.Greater(bp.Fired, 0, $"{name}: {bp.Kind} pattern fired nothing");
                else
                    Assert.Greater(hits, 0, $"{name}: {attack.State} never hit a target {dist:0.0} m in front");

                Object.Destroy(go);
                Object.Destroy(target.gameObject);
                foreach (var pr in Object.FindObjectsByType<Projectile>())
                    Object.Destroy(pr.gameObject);
                yield return null;
            }
        }
    }
}
