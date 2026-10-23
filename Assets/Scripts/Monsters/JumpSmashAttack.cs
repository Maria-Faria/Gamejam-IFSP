using System.Collections;
using UnityEngine;

namespace Roguelike
{
    /// The fat one's body slam: marks the spot where the target stands, leaps (between the clip's "TakeOff"
    /// and "Land" events the body travels to the mark) and crushes everything around the landing point. Half
    /// the time the landing also shoots a projectile off in each of the four cardinal directions.
    public class JumpSmashAttack : MonsterAttack
    {
        [Tooltip("Flat disc shown where it will land.")]
        [SerializeField] GameObject telegraph;
        [SerializeField] GameObject landingEffect;
        [Tooltip("Crush radius (x monster height).")]
        [SerializeField] float radius = 0.9f;
        [Tooltip("Seconds between TakeOff and Land in the clip (filled in by Roguelike/Build Monsters).")]
        [SerializeField] float airTime = 0.67f;
        [Tooltip("Farthest it jumps (m).")]
        [SerializeField] float maxLeap = 7f;
        [Header("Landing shots")]
        [SerializeField] Projectile landingShot;
        [Range(0f, 1f)] [SerializeField] float landingShotChance = 0.5f;
        [SerializeField] float landingShotSpeed = 6f;
        [SerializeField] float landingShotDamage = 8f;
        [SerializeField] float landingShotScale = 1f;

        /// Whether the last landing fired the cardinal shots.
        public bool LastLandingShot { get; private set; }

        Vector3 _from, _to;
        float _airStart = -1f;
        bool _landed;

        public override IEnumerator Perform()
        {
            _from = transform.position;
            var to = Monster.Flat(TargetPoint - _from);
            _to = _from + Vector3.ClampMagnitude(to, maxLeap);
            _to.y = _from.y;
            _airStart = -1f;
            _landed = false;
            if (telegraph)
            {
                var t = Track(Instantiate(telegraph, _to + Vector3.up * 0.02f, Quaternion.identity));
                t.transform.localScale = Vector3.one * radius * M.Size * 2f;
            }
            M.Play(state);
            float end = Time.time + M.ClipLength(state);
            while (Time.time < end)
            {
                if (_airStart < 0f)
                    M.FaceTowards(_to, 1.5f);
                else if (!_landed)
                {
                    float k = Mathf.Clamp01((Time.time - _airStart) / airTime);
                    transform.position = Vector3.Lerp(_from, _to, Mathf.SmoothStep(0f, 1f, k));
                }
                yield return null;
            }
        }

        public override void OnAnimationEvent(string evt)
        {
            if (evt == "TakeOff")
                _airStart = Time.time;
            else if (evt == "Land")
            {
                _landed = true;
                transform.position = _to;
                HitSphere(_to + Vector3.up * 0.3f * MonsterScale.Factor, radius * M.Size, damage);
                LastLandingShot = landingShot && Random.value < landingShotChance;
                if (LastLandingShot)
                    foreach (var dir in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                    {
                        var from = _to + Vector3.up * 0.6f * MonsterScale.Factor + dir * M.Size * 0.4f;
                        Projectile.Spawn(landingShot, from, dir * landingShotSpeed, landingShotDamage, transform, 0f, 0f, landingShotScale)
                                  .Cruise(from.y);
                    }
                if (landingEffect)
                {
                    var fx = Instantiate(landingEffect, _to + Vector3.up * 0.05f, Quaternion.identity);
                    fx.transform.localScale = Vector3.one * M.Size;
                    Destroy(fx, 3f);
                }
                Cleanup();
            }
        }
    }
}
