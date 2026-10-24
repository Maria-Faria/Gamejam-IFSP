using System.Collections.Generic;
using UnityEngine;

public class LightBullet : MonoBehaviour
{
  private const float WallBackoff = 0.1f;

  [SerializeField] private float speed = 20f;
  [SerializeField] private float lifetime = 3f;
  [SerializeField] private float damage = 10f;
  [SerializeField] private float hitRadius = 0.3f;

  private float waveAmplitude;
  private float waveFrequency;

  private bool explodes;
  private float explosionRadius;
  private float explosionDamage;
  private Color explosionColor;
  private AudioClip explosionSound;
  private GameObject fragmentPrefab;
  private int fragmentCount;
  private float fragmentDamage;

  private int bouncesLeft;
  private float damageMultiplier = 1f;

  // Teleguiado: curva em direção ao alvo mais próximo à frente
  private const float HomingRange = 16f;
  private const float HomingRetargetSeconds = 0.25f;
  private float homingTurnRate;
  private Transform homingTarget;
  private float retargetTimer;

  private Vector3 origin;
  private Vector3 moveDirection;
  private Vector3 sideDirection;
  private float age;
  private float segmentAge;
  private bool finished;

  public void SetDamage(float newDamage)
  {
    damage = newDamage;
  }

  public void SetBounces(int bounces)
  {
    bouncesLeft = bounces;
  }

  public void SetSpeed(float newSpeed)
  {
    speed = newSpeed;
  }

  // Bônus de dano das relíquias; vale para o impacto, a explosão e os fragmentos
  public void SetDamageMultiplier(float multiplier)
  {
    damageMultiplier = multiplier;
  }

  public void SetLifetime(float seconds)
  {
    lifetime = seconds;
  }

  public void ConfigureHoming(float turnDegreesPerSecond)
  {
    homingTurnRate = turnDegreesPerSecond;
  }

  public void ConfigureWave(float amplitude, float frequency)
  {
    waveAmplitude = amplitude;
    waveFrequency = frequency;
  }

  public void ConfigureExplosion(float radius, float areaDamage, Color color, AudioClip sound, GameObject fragment, int fragments, float fragmentHitDamage)
  {
    explodes = true;
    explosionRadius = radius;
    explosionDamage = areaDamage;
    explosionColor = color;
    explosionSound = sound;
    fragmentPrefab = fragment;
    fragmentCount = fragments;
    fragmentDamage = fragmentHitDamage;
  }

  public void Start()
  {
    StartSegment(transform.position, transform.forward);
  }

  private void StartSegment(Vector3 start, Vector3 direction)
  {
    origin = start;
    moveDirection = direction;
    sideDirection = Vector3.Cross(Vector3.up, moveDirection).normalized;
    segmentAge = 0f;
  }

  public void Update()
  {
    age += Time.deltaTime;
    segmentAge += Time.deltaTime;
    if (age >= lifetime)
    {
      Finish();
      return;
    }

    Vector3 next = origin + moveDirection * speed * segmentAge
      + sideDirection * waveAmplitude * Mathf.Sin(segmentAge * waveFrequency * 2f * Mathf.PI);
    Vector3 delta = next - transform.position;
    float step = delta.magnitude;

    if (step > 0f)
    {
      Vector3 direction = delta / step;
      RaycastHit[] hits = Physics.SphereCastAll(transform.position, hitRadius, direction, step, ~0, QueryTriggerInteraction.Ignore);
      System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
      foreach (RaycastHit hit in hits)
      {
        if (WeaponDamage.TryDamage(hit.collider, damage * damageMultiplier))
        {
          transform.position += direction * hit.distance;
          Finish();
          return;
        }

        if (!IsBlocking(hit))
        {
          continue;
        }

        if (bouncesLeft > 0 && hit.distance > 0f)
        {
          Bounce(direction, hit);
          return;
        }

        transform.position += direction * Mathf.Max(0f, hit.distance - WallBackoff);
        Finish();
        return;
      }
    }

    transform.position = next;
    SteerTowardTarget();
  }

  private void SteerTowardTarget()
  {
    if (homingTurnRate <= 0f) return;

    retargetTimer -= Time.deltaTime;
    if (retargetTimer <= 0f || homingTarget == null)
    {
      retargetTimer = HomingRetargetSeconds;
      homingTarget = FindHomingTarget();
    }
    if (homingTarget == null) return;

    Vector3 toTarget = homingTarget.position - transform.position;
    toTarget.y = 0f;
    if (toTarget.sqrMagnitude < 0.01f) return;

    Vector3 direction = Vector3.RotateTowards(moveDirection, toTarget.normalized, homingTurnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
    transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    StartSegment(transform.position, direction);
  }

  // O alvo mais perto, sem contar os que ficaram muito para trás
  private Transform FindHomingTarget()
  {
    Transform best = null;
    float bestDistance = HomingRange;
    foreach (MonoBehaviour target in PlayerShooting.FindAimTargets())
    {
      if (target is GuardiasDaLuz.MapSystem.StandaloneBreakableCrate) continue;
      Vector3 toTarget = target.transform.position - transform.position;
      toTarget.y = 0f;
      float distance = toTarget.magnitude;
      if (distance >= bestDistance || Vector3.Angle(moveDirection, toTarget) > 120f) continue;
      bestDistance = distance;
      best = target.transform;
    }
    return best;
  }

  private static bool IsBlocking(RaycastHit hit)
  {
    if (hit.distance > 0f && hit.normal.y > 0.7f)
    {
      return false;
    }

    Collider other = hit.collider;
    return other.GetComponentInParent<PlayerMovement>() == null
      && other.GetComponent<LightBullet>() == null
      && other.GetComponentInParent<GuardiasDaLuz.MapSystem.StandaloneSmartPost>() == null;
  }

  private void Bounce(Vector3 direction, RaycastHit hit)
  {
    bouncesLeft--;
    Vector3 normal = hit.normal;
    normal.y = 0f;

    Vector3 reflected = Vector3.Reflect(direction, normal.normalized);
    reflected.y = 0f;
    if (reflected.sqrMagnitude < 0.0001f)
    {
      reflected = -direction;
    }

    Vector3 contact = transform.position + direction * hit.distance + normal.normalized * 0.05f;
    transform.rotation = Quaternion.LookRotation(reflected.normalized, Vector3.up);
    StartSegment(contact, reflected.normalized);
    transform.position = contact;
  }

  public void OnTriggerEnter(Collider other)
  {
    if (finished || other.isTrigger)
    {
      return;
    }

    if (WeaponDamage.TryDamage(other, damage * damageMultiplier))
    {
      Finish();
    }
  }

  private void Finish()
  {
    if (finished)
    {
      return;
    }
    finished = true;

    if (explodes)
    {
      Explode();
    }

    Destroy(gameObject);
  }

  private void Explode()
  {
    Vector3 center = transform.position;
    HashSet<MonoBehaviour> damaged = new HashSet<MonoBehaviour>();

    foreach (Collider hit in Physics.OverlapSphere(center, explosionRadius, ~0, QueryTriggerInteraction.Ignore))
    {
      MonoBehaviour target = WeaponDamage.FindTarget(hit);
      if (target == null || !damaged.Add(target))
      {
        continue;
      }

      float distance = Vector3.Distance(center, hit.ClosestPoint(center));
      float falloff = Mathf.Lerp(1f, 0.3f, distance / explosionRadius);
      WeaponDamage.Damage(target, explosionDamage * damageMultiplier * falloff);
    }

    ExplosionFlash.Spawn(center, explosionRadius, explosionColor);
    PlayExplosionSound(center);
    SpawnFragments(center);
  }

  private void SpawnFragments(Vector3 center)
  {
    if (fragmentPrefab == null || fragmentCount <= 0)
    {
      return;
    }

    float offset = Random.Range(0f, 360f);
    for (int i = 0; i < fragmentCount; i++)
    {
      Quaternion rotation = Quaternion.Euler(0f, offset + 360f * i / fragmentCount, 0f);
      GameObject fragment = Instantiate(fragmentPrefab, center, rotation);
      fragment.transform.localScale *= 0.6f;

      LightBullet bullet = fragment.GetComponent<LightBullet>();
      if (bullet != null)
      {
        bullet.SetDamage(fragmentDamage);
        bullet.SetDamageMultiplier(damageMultiplier);
        bullet.lifetime = 0.5f;
      }
    }
  }

  private void PlayExplosionSound(Vector3 position)
  {
    if (explosionSound == null)
    {
      return;
    }

    GameObject soundObject = new GameObject("ExplosionSound");
    soundObject.transform.position = position;

    AudioSource source = soundObject.AddComponent<AudioSource>();
    source.clip = explosionSound;
    source.spatialBlend = 0f;
    source.Play();

    Destroy(soundObject, explosionSound.length + 0.1f);
  }
}
