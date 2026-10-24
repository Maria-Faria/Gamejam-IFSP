using UnityEngine;

// O que os monstros deixam cair (MonsterDrops): moedas (coin.fbx), que compram armas na loja, e corações
// (heart.fbx), que devolvem vida. Saltam do monstro, quicam no chão, giram e flutuam; perto da jogadora
// voam até ela. O coração só é pego se a vida não estiver cheia. Somem depois de um tempo, piscando antes.
public class LootPickup : MonoBehaviour
{
  public enum Kind { Coin, Heart }

  private const float Gravity = 18f;
  private const float MagnetDelay = 0.45f;
  private const float MagnetRange = 4.5f;
  private const float CollectRange = 1.1f;
  private const float BlinkTime = 5f;

  private static float lastCoinSound;

  private Kind kind;
  private int value;
  private float lifetime;
  private Vector3 velocity;
  private float groundY;
  private bool grounded;
  private float age;
  private float spinPhase;
  private Renderer[] bodies;
  private Transform visual;

  public Kind Type => kind;
  public int Value => value;

  // Moeda de `amount`; vida `amount` para coração
  public static LootPickup Spawn(Kind kind, Vector3 position, int amount)
  {
    PickupCatalog catalog = PickupCatalog.Load();
    GameObject model = catalog != null ? (kind == Kind.Coin ? catalog.coin : catalog.heart) : null;
    GameObject root = new GameObject(kind == Kind.Coin ? $"Moeda_{amount}" : "Coracao");
    root.transform.position = position + Vector3.up * 0.8f;

    float size = kind == Kind.Coin ? 0.38f + 0.06f * Mathf.Min(amount - 1, 4) : 0.5f;
    if (model != null)
    {
      PowerUpPickup.CreateModelVisual(model, root.transform, size);
    }
    else
    {
      GameObject shape = GameObject.CreatePrimitive(kind == Kind.Coin ? PrimitiveType.Cylinder : PrimitiveType.Sphere);
      Destroy(shape.GetComponent<Collider>());
      shape.transform.SetParent(root.transform, false);
      shape.transform.localScale = kind == Kind.Coin ? new Vector3(size, 0.04f, size) : Vector3.one * size;
      shape.transform.localRotation = kind == Kind.Coin ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
      shape.GetComponent<Renderer>().material.color = kind == Kind.Coin ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.3f, 0.35f);
    }

    LootPickup pickup = root.AddComponent<LootPickup>();
    pickup.kind = kind;
    pickup.value = Mathf.Max(1, amount);
    pickup.lifetime = kind == Kind.Coin ? 30f : 25f;
    pickup.visual = root.transform.childCount > 0 ? root.transform.GetChild(0) : root.transform;
    pickup.bodies = root.GetComponentsInChildren<Renderer>();
    foreach (Renderer body in pickup.bodies) body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    Vector2 side = Random.insideUnitCircle * 2.6f;
    pickup.velocity = new Vector3(side.x, Random.Range(4.5f, 6.5f), side.y);
    pickup.groundY = position.y;
    if (Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
    {
      pickup.groundY = hit.point.y;
    }
    pickup.spinPhase = Random.Range(0f, 360f);
    return pickup;
  }

  public void Update()
  {
    float dt = Time.deltaTime;
    age += dt;
    Transform player = FindPlayer();

    bool wanted = player != null && Wants(player);
    Vector3 toPlayer = player != null ? player.position + Vector3.up * 0.9f - transform.position : Vector3.zero;
    if (wanted && age > MagnetDelay && toPlayer.magnitude < MagnetRange)
    {
      // voa até a jogadora, cada vez mais rápido
      float speed = Mathf.Lerp(14f, 5f, toPlayer.magnitude / MagnetRange);
      transform.position = Vector3.MoveTowards(transform.position, player.position + Vector3.up * 0.9f, speed * dt);
      grounded = false;
      velocity = Vector3.zero;
    }
    else if (!grounded)
    {
      velocity.y -= Gravity * dt;
      transform.position += velocity * dt;
      float rest = groundY + 0.45f;
      if (transform.position.y <= rest && velocity.y < 0f)
      {
        transform.position = new Vector3(transform.position.x, rest, transform.position.z);
        velocity = new Vector3(velocity.x * 0.5f, -velocity.y * 0.35f, velocity.z * 0.5f);
        if (velocity.y < 1.2f) grounded = true;
      }
    }

    spinPhase += dt * (kind == Kind.Coin ? 220f : 90f);
    if (visual != null)
    {
      float bob = grounded ? Mathf.Sin(age * 3f) * 0.08f : 0f;
      visual.localRotation = Quaternion.Euler(0f, spinPhase, 0f);
      visual.localPosition = new Vector3(0f, bob, 0f);
    }

    if (wanted && toPlayer.magnitude < CollectRange)
    {
      Collect(player);
      return;
    }

    float left = lifetime - age;
    if (left < BlinkTime)
    {
      bool visible = Mathf.Repeat(age * 6f, 1f) < 0.6f;
      foreach (Renderer body in bodies) if (body != null) body.enabled = visible;
    }
    if (left <= 0f) Destroy(gameObject);
  }

  private bool Wants(Transform player)
  {
    if (kind == Kind.Coin) return true;
    PlayerHealth health = player.GetComponentInParent<PlayerHealth>();
    return health != null && !health.IsDead && health.Health < health.MaxHealth;
  }

  private void Collect(Transform player)
  {
    if (kind == Kind.Coin)
    {
      PlayerWallet wallet = PlayerWallet.Of(player);
      if (wallet != null) wallet.Add(value);
      // várias moedas de uma vez tocam um som só
      if (Time.unscaledTime - lastCoinSound > 0.25f)
      {
        lastCoinSound = Time.unscaledTime;
        PowerUpPickup.PlaySound(PowerUpPickup.LoadSound(PowerUpPickup.RetroTechSoundName));
      }
    }
    else
    {
      PlayerHealth health = player.GetComponentInParent<PlayerHealth>();
      if (health != null) health.Heal(value);
      PowerUpPickup.PlaySound(PowerUpPickup.LoadSound(PowerUpPickup.LightCrystalSoundName));
      var map = GuardiasDaLuz.MapSystem.StandaloneLighthouseMapSystem.Instance;
      if (map != null) map.ShowBannerMessage($"❤ +{value} de vida!");
    }
    Destroy(gameObject);
  }

  private static Transform FindPlayer()
  {
    var map = GuardiasDaLuz.MapSystem.StandaloneLighthouseMapSystem.Instance;
    if (map != null && map.playerTransform != null) return map.playerTransform;
    PlayerMovement movement = FindAnyObjectByType<PlayerMovement>();
    return movement != null ? movement.transform : null;
  }
}
