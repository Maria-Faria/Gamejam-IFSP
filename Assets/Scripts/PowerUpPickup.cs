using UnityEngine;

public class PowerUpPickup : MonoBehaviour
{
  private const float PickupDistance = 1.6f;
  private const float Lifetime = 30f;
  private const float BlinkTime = 5f;

  private const float MinSecondsBetweenDrops = 10f;
  private const float MaxSecondsBetweenDrops = 25f;

  private static PlayerPowerUps player;
  private static float nextDropTime;
  private static int lastType = -1;

  private PowerUpType type;
  private Renderer body;
  private float baseHeight;
  private float age;

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void ResetStatics()
  {
    player = null;
    nextDropTime = 0f;
    lastType = -1;
  }

  public static void TrySpawn(Vector3 position, float chance)
  {
    if (Time.time < nextDropTime || Random.value > chance)
    {
      return;
    }

    int count = System.Enum.GetValues(typeof(PowerUpType)).Length;
    int index = Random.Range(0, count);
    if (index == lastType)
    {
      index = (index + Random.Range(1, count)) % count;
    }

    lastType = index;
    nextDropTime = Time.time + Random.Range(MinSecondsBetweenDrops, MaxSecondsBetweenDrops);
    Spawn(position, (PowerUpType)index);
  }

  public static void Spawn(Vector3 position, PowerUpType type)
  {
    GameObject pickupObject = GameObject.CreatePrimitive(GetShape(type));
    pickupObject.name = $"PowerUp_{type}";
    Object.Destroy(pickupObject.GetComponent<Collider>());
    pickupObject.transform.position = new Vector3(position.x, 0.8f, position.z);
    pickupObject.transform.localScale = Vector3.one * 0.5f;

    PowerUpPickup pickup = pickupObject.AddComponent<PowerUpPickup>();
    pickup.type = type;
    pickup.body = pickupObject.GetComponent<Renderer>();
    pickup.baseHeight = pickupObject.transform.position.y;
    pickup.body.material.color = GetColor(type);
    pickup.body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
  }

  public static Color GetColor(PowerUpType type)
  {
    switch (type)
    {
      case PowerUpType.Ricochet: return new Color(0.3f, 0.9f, 1f);
      case PowerUpType.Health: return new Color(0.95f, 0.25f, 0.3f);
      case PowerUpType.Mana: return new Color(0.6f, 0.3f, 1f);
      case PowerUpType.MoveSpeed: return new Color(0.35f, 1f, 0.45f);
      default: return new Color(1f, 0.8f, 0.2f);
    }
  }

  private static PrimitiveType GetShape(PowerUpType type)
  {
    switch (type)
    {
      case PowerUpType.Ricochet: return PrimitiveType.Cube;
      case PowerUpType.Health: return PrimitiveType.Sphere;
      case PowerUpType.Mana: return PrimitiveType.Sphere;
      case PowerUpType.MoveSpeed: return PrimitiveType.Capsule;
      default: return PrimitiveType.Cylinder;
    }
  }

  public void Update()
  {
    age += Time.deltaTime;
    if (age >= Lifetime)
    {
      Destroy(gameObject);
      return;
    }

    transform.Rotate(30f * Time.deltaTime, 120f * Time.deltaTime, 0f);
    Vector3 position = transform.position;
    position.y = baseHeight + Mathf.Sin(age * 3f) * 0.15f;
    transform.position = position;
    body.enabled = age < Lifetime - BlinkTime || Mathf.Repeat(age * 6f, 1f) < 0.6f;

    if (player == null)
    {
      player = FindAnyObjectByType<PlayerPowerUps>();
      if (player == null) return;
    }

    Vector3 offset = player.transform.position - transform.position;
    offset.y = 0f;
    if (offset.magnitude <= PickupDistance)
    {
      player.Collect(type);
      Destroy(gameObject);
    }
  }
}
