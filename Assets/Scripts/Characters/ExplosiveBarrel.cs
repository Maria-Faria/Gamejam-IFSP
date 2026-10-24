using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Barril explosivo das salas (RoomDecorKit põe um em cada barril de decoração). Um tiro faz o barril
// explodir: um clarão de luz que ilumina a sala por um instante (e espanta as sombras, como as outras
// luzes do LightField), dano e empurrão em todos os monstros do raio — a jogadora perto também se fere —,
// e os barris vizinhos explodem em cadeia.
public class ExplosiveBarrel : MonoBehaviour
{
  [SerializeField] private float radius = 4.5f;
  [SerializeField] private float damage = 60f;
  [Tooltip("Dano na jogadora se ela estiver no raio.")]
  [SerializeField] private float playerDamage = 15f;
  [SerializeField] private float knockback = 10f;
  [SerializeField] private Color flashColor = new Color(1f, 0.75f, 0.35f, 0.75f);
  [SerializeField] private float lightIntensity = 9f;
  [SerializeField] private float lightSeconds = 1.4f;
  [Tooltip("Segundos até um barril vizinho, atingido pela explosão, explodir também.")]
  [SerializeField] private float chainDelay = 0.18f;

  private bool exploded;

  public bool Exploded => exploded;
  public float Radius => radius;

  public static int Count => FindObjectsByType<ExplosiveBarrel>().Length;

  public void TakeHit(float amount)
  {
    Explode();
  }

  public void Explode()
  {
    if (exploded)
    {
      return;
    }
    exploded = true;

    Vector3 center = Center();
    var damaged = new HashSet<MonoBehaviour>();
    bool playerHit = false;
    foreach (Collider hit in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
    {
      if (hit.transform.IsChildOf(transform))
      {
        continue;
      }

      float falloff = Mathf.Lerp(1f, 0.35f, Vector3.Distance(center, hit.ClosestPoint(center)) / radius);
      ExplosiveBarrel other = hit.GetComponentInParent<ExplosiveBarrel>();
      if (other != null)
      {
        if (!other.exploded) other.StartCoroutine(other.ExplodeAfter(chainDelay));
        continue;
      }

      PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
      if (player != null)
      {
        if (!playerHit && player.GetComponent<GuardiasDaLuz.CoopRemoteAvatar>() == null)
        {
          playerHit = true;
          player.TakeDamage(playerDamage * falloff);
          PlayerStatus.Of(player)?.Knockback(Flat(player.transform.position - center).normalized * knockback * 0.6f);
        }
        continue;
      }

      MonoBehaviour target = WeaponDamage.FindTarget(hit);
      if (target == null || !damaged.Add(target))
      {
        continue;
      }
      WeaponDamage.Damage(target, damage * falloff);
      WeaponDamage.Push(hit, Flat(hit.transform.position - center).normalized * knockback * falloff);
    }

    ExplosionFlash.Spawn(center, radius, flashColor);
    BarrelBlast.Spawn(center, radius, flashColor, lightIntensity, lightSeconds);
    for (int i = 0; i < 10; i++)
    {
      EnemyBullet.SpawnSmokePuff(center + Random.insideUnitSphere * radius * 0.35f, Random.Range(0.6f, 1.1f));
    }
    Destroy(gameObject);
  }

  private IEnumerator ExplodeAfter(float seconds)
  {
    yield return new WaitForSeconds(seconds);
    Explode();
  }

  private Vector3 Center()
  {
    Bounds bounds = new Bounds(transform.position, Vector3.zero);
    bool any = false;
    foreach (Renderer r in GetComponentsInChildren<Renderer>())
    {
      if (any) bounds.Encapsulate(r.bounds);
      else bounds = r.bounds;
      any = true;
    }
    return any ? bounds.center : transform.position + Vector3.up * 0.6f;
  }

  private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}

// O clarão de um barril: uma luz forte e quente que se apaga em pouco mais de um segundo
public class BarrelBlast : MonoBehaviour
{
  private Light flash;
  private float intensity;
  private float seconds;
  private float age;

  public static Light Spawn(Vector3 position, float radius, Color color, float intensity, float seconds)
  {
    GameObject go = new GameObject("Clarao_Barril");
    go.transform.position = position + Vector3.up * 0.5f;
    BarrelBlast blast = go.AddComponent<BarrelBlast>();
    blast.flash = go.AddComponent<Light>();
    blast.flash.type = LightType.Point;
    blast.flash.color = new Color(color.r, color.g, color.b);
    blast.flash.range = radius * 3f;
    blast.flash.intensity = intensity;
    blast.flash.shadows = LightShadows.None;
    blast.intensity = intensity;
    blast.seconds = seconds;
    return blast.flash;
  }

  public void Update()
  {
    age += Time.deltaTime;
    float t = Mathf.Clamp01(age / seconds);
    flash.intensity = intensity * (1f - t) * (1f - t);
    if (t >= 1f) Destroy(gameObject);
  }
}
