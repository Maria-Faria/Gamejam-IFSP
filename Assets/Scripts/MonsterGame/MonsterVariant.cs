using Roguelike;
using UnityEngine;

// Variações de monstro aplicadas na hora em que ele nasce (MonsterDirector). Nenhuma muda as cores do
// monstro: cada uma muda atributos e o que o golpe faz na jogadora, e aparece como um rótulo acima dele.
public enum MonsterVariantType
{
  None,
  Venenoso,     // golpes envenenam a jogadora
  Veloz,        // anda e ataca bem mais rápido, com menos vida
  Atordoante,   // golpes atordoam por um instante (com recarga, para não prender a jogadora)
  Gelido,       // golpes deixam a jogadora lenta
  Brutamontes,  // maior, muito mais vida, golpes empurram a jogadora
  Divisor       // ao morrer, se divide em duas bolas de sombra
}

[DisallowMultipleComponent]
public class MonsterVariant : MonoBehaviour
{
  [SerializeField] private MonsterVariantType type;

  [Header("Efeitos na jogadora")]
  [SerializeField] private float poisonDamagePerSecond = 3f;
  [SerializeField] private float poisonSeconds = 3f;
  [SerializeField] private float stunSeconds = 0.75f;
  [Tooltip("Segundos até o mesmo monstro poder atordoar de novo.")]
  [SerializeField] private float stunCooldown = 3f;
  [SerializeField] private float slowMultiplier = 0.55f;
  [SerializeField] private float slowSeconds = 2.2f;
  [SerializeField] private float knockbackSpeed = 11f;

  private Monster monster;
  private float nextStunTime;
  private static bool installed;

  public MonsterVariantType Type => type;
  public Monster Monster => monster;

  public static string GetLabel(MonsterVariantType type)
  {
    switch (type)
    {
      case MonsterVariantType.Venenoso: return "Venenoso";
      case MonsterVariantType.Veloz: return "Veloz";
      case MonsterVariantType.Atordoante: return "Atordoante";
      case MonsterVariantType.Gelido: return "Gélido";
      case MonsterVariantType.Brutamontes: return "Brutamontes";
      case MonsterVariantType.Divisor: return "Divisor";
      default: return "";
    }
  }

  // Cor só do rótulo (o monstro continua com as cores dele).
  public static Color GetLabelColor(MonsterVariantType type)
  {
    switch (type)
    {
      case MonsterVariantType.Venenoso: return new Color(0.45f, 1f, 0.35f);
      case MonsterVariantType.Veloz: return new Color(1f, 0.85f, 0.25f);
      case MonsterVariantType.Atordoante: return new Color(0.6f, 0.85f, 1f);
      case MonsterVariantType.Gelido: return new Color(0.45f, 1f, 1f);
      case MonsterVariantType.Brutamontes: return new Color(1f, 0.55f, 0.25f);
      case MonsterVariantType.Divisor: return new Color(1f, 0.5f, 0.85f);
      default: return Color.white;
    }
  }

  // Aplica a variação num monstro recém-nascido. effectScale escala veneno e empurrão com o andar.
  public static MonsterVariant Apply(Monster target, MonsterVariantType type, float effectScale = 1f)
  {
    if (target == null || type == MonsterVariantType.None)
    {
      return null;
    }

    MonsterVariant variant = target.gameObject.AddComponent<MonsterVariant>();
    variant.type = type;
    variant.monster = target;
    variant.poisonDamagePerSecond *= effectScale;
    variant.knockbackSpeed *= Mathf.Lerp(1f, effectScale, 0.5f);

    Roguelike.Health health = target.Health;
    switch (type)
    {
      case MonsterVariantType.Venenoso:
        target.DamageScale *= 0.8f;
        break;
      case MonsterVariantType.Veloz:
        target.SpeedScale *= 1.45f;
        target.CooldownScale *= 0.7f;
        if (target.Animator != null) target.Animator.speed *= 1.2f;
        ScaleHealth(health, 0.8f);
        break;
      case MonsterVariantType.Atordoante:
        target.DamageScale *= 0.9f;
        break;
      case MonsterVariantType.Gelido:
        ScaleHealth(health, 1.1f);
        break;
      case MonsterVariantType.Brutamontes:
        target.transform.localScale *= 1.3f;
        target.SpeedScale *= 0.85f;
        target.DamageScale *= 1.3f;
        ScaleHealth(health, 2.2f);
        break;
      case MonsterVariantType.Divisor:
        ScaleHealth(health, 1.2f);
        break;
    }
    return variant;
  }

  private static void ScaleHealth(Roguelike.Health health, float factor)
  {
    if (health != null)
    {
      health.SetMax(health.Max * factor);
    }
  }

  public void Awake()
  {
    monster = GetComponent<Monster>();
    Install();
  }

  // Um único ouvinte para todos os golpes de monstro: aplica o efeito da variação de quem bateu.
  private static void Install()
  {
    if (installed)
    {
      return;
    }
    installed = true;
    Roguelike.Health.HitByMonster += OnHitByMonster;
  }

  private static void OnHitByMonster(Roguelike.Health victim, float damage, Monster source)
  {
    if (victim == null || source == null)
    {
      return;
    }

    MonsterVariant variant = source.GetComponent<MonsterVariant>();
    if (variant == null)
    {
      return;
    }

    PlayerStatus status = PlayerStatus.Of(victim);
    if (status != null)
    {
      variant.ApplyHitEffect(status, victim.transform.position);
    }
  }

  private void ApplyHitEffect(PlayerStatus status, Vector3 victimPosition)
  {
    switch (type)
    {
      case MonsterVariantType.Venenoso:
        status.Poison(poisonDamagePerSecond, poisonSeconds);
        break;
      case MonsterVariantType.Atordoante:
        if (Time.time >= nextStunTime)
        {
          nextStunTime = Time.time + stunCooldown;
          status.Stun(stunSeconds);
        }
        break;
      case MonsterVariantType.Gelido:
        status.Slow(slowMultiplier, slowSeconds);
        break;
      case MonsterVariantType.Brutamontes:
        Vector3 away = victimPosition - transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = transform.forward;
        status.Knockback(away.normalized * knockbackSpeed);
        break;
    }
  }
}
