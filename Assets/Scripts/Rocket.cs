using System.Collections.Generic;
using UnityEngine;

public class Rocket : MonoBehaviour
{
  [SerializeField] private float speed = 14f;
  [SerializeField] private float lifetime = 4f;
  [SerializeField] private float explosionRadius = 3.5f;
  [SerializeField] private float explosionDamage = 25f;
  [SerializeField] private Color explosionColor = new Color(1f, 0.6f, 0.2f, 0.6f);
  [SerializeField] private AudioClip explosionSound;
  [SerializeField] private float explosionVolume = 1f;
  [Tooltip("0 = 2D (sempre audível), 1 = 3D (mais baixo quanto mais longe da câmera).")]
  [Range(0f, 1f)]
  [SerializeField] private float explosionSpatialBlend = 0f;

  private bool exploded;

  public void Configure(float newSpeed, float radius, float damage, AudioClip sound)
  {
    speed = newSpeed;
    explosionRadius = radius;
    explosionDamage = damage;
    explosionSound = sound;
  }

  public void Start()
  {
    Invoke(nameof(Explode), lifetime);
  }

  public void Update()
  {
    transform.position += transform.forward * speed * Time.deltaTime;
  }

  public void OnTriggerEnter(Collider other)
  {
    if (other.GetComponentInParent<PlayerMovement>() != null
      || other.GetComponent<LightBullet>() != null
      || other.GetComponent<Rocket>() != null)
    {
      return;
    }

    Explode();
  }

  private void Explode()
  {
    if (exploded)
    {
      return;
    }
    exploded = true;

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
      WeaponDamage.Damage(target, explosionDamage * falloff);
    }

    PlayExplosionSound(center);
    ExplosionFlash.Spawn(center, explosionRadius, explosionColor);
    Destroy(gameObject);
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
    source.volume = explosionVolume;
    source.spatialBlend = explosionSpatialBlend;
    source.Play();

    Destroy(soundObject, explosionSound.length + 0.1f);
  }
}

public class ExplosionFlash : MonoBehaviour
{
  private const float Duration = 0.3f;

  private float maxRadius;
  private Color color;
  private Material material;
  private float elapsed;

  public static void Spawn(Vector3 position, float radius, Color color)
  {
    GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    flash.name = "ExplosionFlash";
    Destroy(flash.GetComponent<Collider>());
    flash.transform.position = position;

    ExplosionFlash effect = flash.AddComponent<ExplosionFlash>();
    effect.maxRadius = radius;
    effect.color = color;
    effect.material = new Material(Shader.Find("Sprites/Default"));
    flash.GetComponent<Renderer>().material = effect.material;
    effect.Apply(0f);
  }

  public void Update()
  {
    elapsed += Time.deltaTime;
    float t = elapsed / Duration;
    if (t >= 1f)
    {
      Destroy(gameObject);
      return;
    }

    Apply(t);
  }

  private void Apply(float t)
  {
    transform.localScale = Vector3.one * Mathf.Lerp(0.3f, maxRadius * 2f, t);
    material.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
  }
}
