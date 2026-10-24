using UnityEngine;

// Efeitos de status que os inimigos aplicam na jogadora: atordoamento, veneno, lentidão e empurrão.
// PlayerMovement e PlayerShooting consultam este componente; PlayerAnimation mostra os efeitos.
public class PlayerStatus : MonoBehaviour
{
  [Header("Veneno")]
  [Tooltip("Intervalo, em segundos, entre dois danos de veneno. O total causado continua sendo dps x segundos.")]
  [SerializeField] private float poisonTickInterval = 0.75f;

  [Header("Empurrão")]
  [Tooltip("Quão rápido o empurrão perde força (por segundo). Maior = empurrão mais curto.")]
  [SerializeField] private float knockbackDamping = 8f;
  [Tooltip("Velocidade máxima de empurrão acumulado.")]
  [SerializeField] private float maxKnockbackSpeed = 25f;

  [Header("Interface")]
  [SerializeField] private bool showStatusGUI = true;

  private PlayerHealth health;
  private bool subscribed;

  private float stunTimer;
  private float stunDuration;
  private float poisonTimer;
  private float poisonDuration;
  private float poisonDps;
  private float poisonPending;
  private float poisonTickTimer;
  private float slowTimer;
  private float slowDuration;
  private float slowMultiplier = 1f;
  private Vector3 knockbackVelocity;

  public bool IsStunned => stunTimer > 0f;
  public bool IsPoisoned => poisonTimer > 0f;
  public bool IsSlowed => slowTimer > 0f;

  public float StunRemaining => stunTimer;
  public float PoisonRemaining => poisonTimer;
  public float SlowRemaining => slowTimer;
  public float PoisonDamagePerSecond => IsPoisoned ? poisonDps : 0f;
  // Multiplicador de velocidade de movimento atual (1 quando não está lenta)
  public float SpeedMultiplier => IsSlowed ? slowMultiplier : 1f;
  // Velocidade de empurrão atual, já diminuindo com o tempo (aplicada pelo PlayerMovement)
  public Vector3 KnockbackVelocity => knockbackVelocity;
  public bool IsKnockedBack => knockbackVelocity.sqrMagnitude > 0.01f;

  public event System.Action<string> StatusApplied;

  // Procura o PlayerStatus a partir de qualquer parte da jogadora (colisor, modelo, arma...).
  // Se a jogadora ainda não tem o componente, ele é adicionado. Retorna null se não for a jogadora.
  public static PlayerStatus Of(Component anyPartOfPlayer)
  {
    if (anyPartOfPlayer == null)
    {
      return null;
    }

    PlayerStatus status = anyPartOfPlayer.GetComponentInParent<PlayerStatus>();
    if (status != null)
    {
      return status;
    }

    PlayerHealth playerHealth = anyPartOfPlayer.GetComponentInParent<PlayerHealth>();
    if (playerHealth != null)
    {
      return playerHealth.gameObject.AddComponent<PlayerStatus>();
    }

    PlayerMovement playerMovement = anyPartOfPlayer.GetComponentInParent<PlayerMovement>();
    return playerMovement != null ? playerMovement.gameObject.AddComponent<PlayerStatus>() : null;
  }

  public void OnEnable()
  {
    FindHealth();
  }

  public void OnDisable()
  {
    if (health != null && subscribed) health.Died -= OnDied;
    subscribed = false;
  }

  public void Update()
  {
    FindHealth();
    float dt = Time.deltaTime;

    stunTimer = Mathf.Max(0f, stunTimer - dt);
    slowTimer = Mathf.Max(0f, slowTimer - dt);
    UpdatePoison(dt);

    knockbackVelocity *= Mathf.Exp(-knockbackDamping * dt);
    if (knockbackVelocity.sqrMagnitude < 0.01f)
    {
      knockbackVelocity = Vector3.zero;
    }
  }

  // Não pode andar, atirar nem trocar de arma. Vários atordoamentos ficam com o maior tempo restante.
  public void Stun(float seconds)
  {
    if (IsDeadOrInvalid(seconds))
    {
      return;
    }

    if (seconds > stunTimer)
    {
      stunTimer = seconds;
      stunDuration = seconds;
    }
    StatusApplied?.Invoke("stun");
  }

  // Dano contínuo via PlayerHealth.TakeDamage. Renova a duração e mantém o dps mais forte.
  public void Poison(float damagePerSecond, float seconds)
  {
    if (IsDeadOrInvalid(seconds) || damagePerSecond <= 0f)
    {
      return;
    }

    if (!IsPoisoned)
    {
      poisonDps = 0f;
      poisonTickTimer = poisonTickInterval;
    }

    poisonDps = Mathf.Max(poisonDps, damagePerSecond);
    if (seconds > poisonTimer)
    {
      poisonTimer = seconds;
      poisonDuration = seconds;
    }
    StatusApplied?.Invoke("poison");
  }

  // Multiplica a velocidade de movimento (0..1). Fica com a lentidão mais forte e o maior tempo.
  public void Slow(float speedMultiplier, float seconds)
  {
    if (IsDeadOrInvalid(seconds))
    {
      return;
    }

    speedMultiplier = Mathf.Clamp01(speedMultiplier);
    slowMultiplier = IsSlowed ? Mathf.Min(slowMultiplier, speedMultiplier) : speedMultiplier;
    if (seconds > slowTimer)
    {
      slowTimer = seconds;
      slowDuration = seconds;
    }
    StatusApplied?.Invoke("slow");
  }

  // Empurrão curto. O PlayerMovement aplica pelo CharacterController, então paredes seguram a jogadora.
  public void Knockback(Vector3 velocity)
  {
    FindHealth();
    if (health != null && health.IsDead)
    {
      return;
    }

    velocity.y = 0f;
    knockbackVelocity = Vector3.ClampMagnitude(knockbackVelocity + velocity, maxKnockbackSpeed);
    StatusApplied?.Invoke("knockback");
  }

  // Remove todos os efeitos (usado na morte)
  public void ClearAll()
  {
    FlushPoison();
    stunTimer = 0f;
    poisonTimer = 0f;
    poisonDps = 0f;
    slowTimer = 0f;
    slowMultiplier = 1f;
    knockbackVelocity = Vector3.zero;
  }

  private void UpdatePoison(float dt)
  {
    if (!IsPoisoned)
    {
      return;
    }

    float active = Mathf.Min(dt, poisonTimer);
    poisonTimer -= active;
    poisonPending += poisonDps * active;
    poisonTickTimer -= dt;

    if (poisonTickTimer <= 0f || poisonTimer <= 0f)
    {
      poisonTickTimer += poisonTickInterval;
      FlushPoison();
    }

    if (poisonTimer <= 0f)
    {
      poisonTimer = 0f;
      poisonDps = 0f;
    }
  }

  private void FlushPoison()
  {
    if (poisonPending <= 0f)
    {
      return;
    }

    float damage = poisonPending;
    poisonPending = 0f;
    FindHealth();
    if (health != null) health.TakeDamage(damage);
  }

  private bool IsDeadOrInvalid(float seconds)
  {
    FindHealth();
    return seconds <= 0f || (health != null && health.IsDead);
  }

  private void FindHealth()
  {
    if (health == null) health = GetComponent<PlayerHealth>();
    if (health != null && !subscribed && isActiveAndEnabled)
    {
      health.Died += OnDied;
      subscribed = true;
    }
  }

  private void OnDied()
  {
    poisonPending = 0f;
    ClearAll();
  }

  public void OnGUI()
  {
    HudScale.Begin();
    DrawHud();
    HudScale.End();
  }

  private void DrawHud()
  {
    if (!showStatusGUI || (health != null && health.IsDead))
    {
      return;
    }

    if (IsPoisoned)
    {
      float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
      GUI.color = new Color(0.2f, 0.9f, 0.2f, 0.05f + 0.05f * pulse);
      GUI.DrawTexture(new Rect(0f, 0f, HudScale.Width, HudScale.Height), Texture2D.whiteTexture);
    }

    // Etiquetas ao lado da barra de vida
    float x = 268f;
    float y = HudScale.Height - 44f;
    DrawChip(ref x, y, IsStunned, "Atordoada", stunTimer, stunDuration, new Color(0.85f, 0.65f, 0.1f));
    DrawChip(ref x, y, IsPoisoned, "Envenenada", poisonTimer, poisonDuration, new Color(0.25f, 0.65f, 0.2f));
    DrawChip(ref x, y, IsSlowed, $"Lenta {Mathf.RoundToInt(slowMultiplier * 100f)}%", slowTimer, slowDuration, new Color(0.25f, 0.5f, 0.9f));
    GUI.color = Color.white;
  }

  private static void DrawChip(ref float x, float y, bool active, string label, float timer, float duration, Color color)
  {
    if (!active)
    {
      return;
    }

    Rect chip = new Rect(x, y, 132f, 24f);
    GUI.color = new Color(0f, 0f, 0f, 0.6f);
    GUI.DrawTexture(chip, Texture2D.whiteTexture);
    GUI.color = color;
    float fill = duration > 0f ? Mathf.Clamp01(timer / duration) : 1f;
    GUI.DrawTexture(new Rect(chip.x + 2f, chip.y + 2f, (chip.width - 4f) * fill, chip.height - 4f), Texture2D.whiteTexture);
    GUI.color = Color.white;
    GUI.Label(new Rect(chip.x + 6f, chip.y + 3f, chip.width, chip.height), $"{label} {timer:0.0}s");
    x += chip.width + 6f;
  }
}
