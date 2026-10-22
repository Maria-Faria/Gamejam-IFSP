using GuardiasDaLuz.MapSystem;
using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
  private const float Radius = 0.15f;
  private const float Lifetime = 4f;
  private const float TrailInterval = 0.05f;
  private const float LitDamageFactor = 0.3f;

  private Vector3 direction;
  private float speed;
  private float damage;
  private float nextTrailTime;
  private Transform[] puffs;
  private float[] puffBaseScales;

  public static void Spawn(Vector3 position, Vector3 direction, float speed, float damage)
  {
    GameObject bulletObject = new GameObject("EnemyBullet");
    bulletObject.transform.position = position;

    const int puffCount = 3;
    Transform[] puffs = new Transform[puffCount];
    float[] baseScales = new float[puffCount];
    for (int i = 0; i < puffCount; i++)
    {
      baseScales[i] = Random.Range(0.18f, 0.26f);
      puffs[i] = CreateSmokeSphere(bulletObject.transform, Random.insideUnitSphere * 0.08f, baseScales[i]).transform;
    }

    EnemyBullet bullet = bulletObject.AddComponent<EnemyBullet>();
    bullet.direction = direction;
    bullet.speed = speed;
    bullet.damage = damage;
    bullet.puffs = puffs;
    bullet.puffBaseScales = baseScales;
  }

  private static GameObject CreateSmokeSphere(Transform parent, Vector3 localPosition, float scale)
  {
    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    sphere.name = "Fumaca";
    Destroy(sphere.GetComponent<Collider>());
    if (parent != null)
    {
      sphere.transform.SetParent(parent, false);
    }
    sphere.transform.localPosition = localPosition;
    sphere.transform.localScale = Vector3.one * scale;

    float shade = Random.Range(0.3f, 0.5f);
    EnemySpawner.Paint(sphere.GetComponent<Renderer>(), new Color(shade, shade, shade * 1.05f), Color.black);
    return sphere;
  }

  public void Start()
  {
    Destroy(gameObject, Lifetime);
  }

  public void Update()
  {
    transform.position += direction * speed * Time.deltaTime;
    AnimateSmoke();

    Collider[] hits = Physics.OverlapSphere(transform.position, Radius, ~0, QueryTriggerInteraction.Ignore);
    foreach (Collider hit in hits)
    {
      if (hit.GetComponentInParent<ShadowEnemy>() != null)
      {
        continue;
      }

      PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
      if (playerHealth != null)
      {
        playerHealth.TakeDamage(damage * GetLightDamageFactor());
      }

      Destroy(gameObject);
      return;
    }
  }

  private static float GetLightDamageFactor()
  {
    StandaloneLighthouseMapSystem map = StandaloneLighthouseMapSystem.Instance;
    return map != null ? Mathf.Lerp(1f, LitDamageFactor, map.CurrentRoomBrightness) : 1f;
  }

  private void AnimateSmoke()
  {
    transform.Rotate(Vector3.up, 200f * Time.deltaTime, Space.World);
    for (int i = 0; i < puffs.Length; i++)
    {
      float wobble = 1f + Mathf.Sin(Time.time * 12f + i * 2.1f) * 0.15f;
      puffs[i].localScale = Vector3.one * puffBaseScales[i] * wobble;
    }

    if (Time.time >= nextTrailTime)
    {
      nextTrailTime = Time.time + TrailInterval;
      SpawnSmokePuff(transform.position + Random.insideUnitSphere * 0.05f, Random.Range(0.12f, 0.18f));
    }
  }

  public static void SpawnSmokePuff(Vector3 position, float scale)
  {
    CreateSmokeSphere(null, position, scale).AddComponent<SmokePuff>();
  }
}

public class SmokePuff : MonoBehaviour
{
  private const float Duration = 0.5f;

  private Vector3 startScale;
  private float age;

  public void Start()
  {
    startScale = transform.localScale;
  }

  public void Update()
  {
    age += Time.deltaTime;
    float t = age / Duration;
    if (t >= 1f)
    {
      Destroy(gameObject);
      return;
    }

    transform.localScale = startScale * (1f + t * 0.6f) * (1f - t);
    transform.position += Vector3.up * 0.6f * Time.deltaTime;
  }
}
