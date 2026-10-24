using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// The muscle one's charge: locks onto the target, runs straight at it (Charge loop), trampling whatever
    /// it runs over. If it slams into a wall it plays ChargeWallHit and stands dazed; otherwise it skids to a
    /// stop after maxDistance. With a wallShot (the muscle boss) the impact also sends projectiles flying off
    /// all round; with an explosion it blasts everything near the impact and leaves a burning impact zone on
    /// the ground. A hitStun stuns (and shoves) whoever it runs over.
    public class ChargeAttack : MonsterAttack
    {
        [SerializeField] string wallHitState = "ChargeWallHit";
        [SerializeField] float windup = 0.45f;
        [SerializeField] float speed = 7.5f;
        [SerializeField] float maxDistance = 12f;
        [Tooltip("Runs on this far (m) past where the target stood before skidding to a stop.")]
        [SerializeField] float overshoot = 3f;
        [Tooltip("Body radius for collisions (x monster height).")]
        [SerializeField] float bodyRadius = 0.3f;
        [SerializeField] GameObject impactEffect;
        [Header("Wall impact shots")]
        [SerializeField] Projectile wallShot;
        [SerializeField] int wallShotCount = 8;
        [SerializeField] float wallShotSpeed = 6f;
        [SerializeField] float wallShotDamage = 8f;
        [SerializeField] float wallShotHeight = 0.6f;
        [Header("Wall impact explosion")]
        [SerializeField] GameObject explosionEffect;
        [SerializeField] float explosionRadius = 2f;
        [SerializeField] float explosionDamage;
        [Tooltip("Left burning on the ground where it hit the wall.")]
        [SerializeField] ImpactZone impactZone;
        [SerializeField] float zoneRadius = 2f;
        [SerializeField] float zoneDuration = 4f;
        [SerializeField] float zoneDamage = 4f;
        [Header("Trampling")]
        [Tooltip("Seconds whoever it runs over is stunned (0 = none), and how hard they are shoved (m/s).")]
        [SerializeField] float hitStun;
        [SerializeField] float hitKnockback = 8f;

        public bool HitWall { get; private set; }
        /// Where the last charge slammed into the wall, and the zone it left there (tests).
        public Vector3 WallPoint { get; private set; }
        public ImpactZone LastZone { get; private set; }

        public float Speed
        {
            get => speed;
            set => speed = value;
        }

        /// Seconds of stomping build-up before it runs.
        public float Windup
        {
            get => windup;
            set => windup = value;
        }

        public override IEnumerator Perform()
        {
            HitWall = false;
            M.Play(state, 0.2f);
            M.Animator.speed = 0.35f;              // slow, stomping build-up
            float t = 0f;
            while (t < windup)
            {
                t += Time.deltaTime;
                M.FaceTowards(TargetPoint, 2f);
                yield return null;
            }
            M.Animator.speed = 1f;
            var dir = transform.forward;
            float travelled = 0f;
            float runFor = Mathf.Min(maxDistance, Vector3.Dot(Monster.Flat(TargetPoint - transform.position), dir) + overshoot);
            float r = bodyRadius * M.Size;
            var trampled = new HashSet<Health>();
            while (travelled < runFor)
            {
                float step = speed * Time.deltaTime;
                var origin = transform.position + Vector3.up * M.Size * 0.5f;
                foreach (var hit in Physics.SphereCastAll(origin, r, dir, step + r * 0.2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (M.IsSelf(hit.collider) || hit.collider.GetComponentInParent<Monster>())
                        continue;
                    var victim = Health.Victim(hit.collider, transform);
                    if (victim)
                    {
                        if (trampled.Add(victim))
                        {
                            victim.TakeDamage(damage, transform.position, M);
                            if (hitStun > 0f) victim.Stun(hitStun, dir * hitKnockback, M);
                        }
                        continue;
                    }
                    if (hit.normal.y > 0.6f)
                        continue;                  // floor / ramp, not a wall
                    if (hit.distance <= 0f && Vector3.Dot(hit.collider.ClosestPoint(origin) - origin, dir) <= 0f)
                        continue;                  // already touching it, but it is behind or beside us
                    HitWall = true;
                    WallPoint = hit.distance > 0f && hit.point != Vector3.zero ? hit.point : transform.position + dir * r;
                    break;
                }
                if (HitWall)
                    break;
                transform.position += dir * step;
                travelled += step;
                yield return null;
            }
            if (HitWall)
            {
                if (impactEffect)
                {
                    var fx = Instantiate(impactEffect, transform.position + dir * r + Vector3.up * M.Size * 0.5f, Quaternion.LookRotation(-dir));
                    fx.transform.localScale = Vector3.one * M.Size;
                    Destroy(fx, 3f);
                }
                Explode(dir, r);
                if (wallShot)
                {
                    var centre = transform.position + Vector3.up * wallShotHeight;
                    for (int i = 0; i < wallShotCount; i++)
                    {
                        var d = Quaternion.Euler(0f, i * 360f / wallShotCount, 0f) * Vector3.forward;
                        Projectile.Spawn(wallShot, centre + d * r, d * wallShotSpeed, wallShotDamage, transform).Cruise(centre.y);
                    }
                }
                M.Play(wallHitState, 0.05f);
                // knocked back a little by the impact
                float back = 0f, t2 = 0f;
                while (t2 < M.ClipLength(wallHitState))
                {
                    t2 += Time.deltaTime;
                    if (back < M.Size * 0.25f)
                    {
                        float s = Time.deltaTime * 2.5f;
                        transform.position -= dir * s;
                        back += s;
                    }
                    yield return null;
                }
            }
            else
            {
                M.Play("Idle", 0.3f);
                yield return new WaitForSeconds(0.3f);
            }
        }

        /// The wall blows apart where it was hit: a burst of smoke and sparks, damage all round, and a burning
        /// patch of ground left there.
        void Explode(Vector3 dir, float r)
        {
            var at = WallPoint - dir * r * 0.5f;
            if (explosionEffect)
            {
                var fx = Instantiate(explosionEffect, at, Quaternion.LookRotation(-dir));
                fx.transform.localScale = Vector3.one * explosionRadius;
                Destroy(fx, 3f);
            }
            if (explosionDamage > 0f && explosionRadius > 0f)
                HitSphere(at, explosionRadius, explosionDamage);
            if (impactZone)
            {
                var ground = new Vector3(at.x, transform.position.y, at.z);
                if (Physics.Raycast(at + Vector3.up, Vector3.down, out var g, 3f + M.Size, ~0, QueryTriggerInteraction.Ignore) && !g.collider.GetComponentInParent<Monster>())
                    ground = g.point;
                LastZone = ImpactZone.Spawn(impactZone, ground + Vector3.up * 0.03f, zoneRadius, zoneDuration, zoneDamage, M);
            }
        }

        public override void Cleanup()
        {
            base.Cleanup();
            if (M && M.Animator) M.Animator.speed = 1f;
        }
    }
}
