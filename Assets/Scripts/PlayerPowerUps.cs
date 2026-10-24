using UnityEngine;

public enum PowerUpType
{
  Ricochet,
  Health,
  Battery,
  MoveSpeed,
  FireRate
}

public class PlayerPowerUps : MonoBehaviour
{
  [Tooltip("Segundos de uso que ricochete, velocidade de movimento e velocidade de tiro duram. O tempo só corre atirando (ricochete e tiro) ou andando (movimento). Pegar outro igual renova o tempo.")]
  [SerializeField] private float timedDuration = 15f;
  [SerializeField] private int ricochetBounces = 2;
  [SerializeField] private float moveSpeedMultiplier = 1.4f;
  [SerializeField] private float fireRateMultiplier = 1.6f;
  [Tooltip("Quanto a vida máxima aumenta, e quanto de vida é recuperada, a cada coleta.")]
  [SerializeField] private float healthIncrease = 20f;
  [SerializeField] private float healthHeal = 40f;
  [Tooltip("Quanto de vida a bateria recupera (sem passar da vida máxima).")]
  [SerializeField] private float batteryHeal = 35f;

  private float ricochetTimer;
  private float moveSpeedTimer;
  private float fireRateTimer;
  private PlayerShooting shooting;
  private PlayerMovement movement;

  // Relíquias dos pedestais: valem até o fim da expedição e somam com os power-ups temporários
  private readonly System.Collections.Generic.List<RelicType> relics = new System.Collections.Generic.List<RelicType>();
  private int relicBounces;
  private float relicDamage = 1f;
  private float relicFireRate = 1f;
  private float relicMoveSpeed = 1f;

  public int RicochetBounces => (ricochetTimer > 0f ? ricochetBounces : 0) + relicBounces;
  public float MoveSpeedMultiplier => (moveSpeedTimer > 0f ? moveSpeedMultiplier : 1f) * relicMoveSpeed;
  public float FireRateMultiplier => (fireRateTimer > 0f ? fireRateMultiplier : 1f) * relicFireRate;
  public float DamageMultiplier => relicDamage;
  public bool TwinShot => HasRelic(RelicType.PrismLens);
  public System.Collections.Generic.IReadOnlyList<RelicType> Relics => relics;

  public bool HasRelic(RelicType relic) => relics.Contains(relic);

  public void ApplyRelic(RelicType relic)
  {
    relics.Add(relic);
    switch (relic)
    {
      case RelicType.HeartOfLight:
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null) health.IncreaseMax(RelicCatalog.HeartHealth, float.MaxValue);
        break;
      case RelicType.FocusCrystal:
        relicDamage += RelicCatalog.FocusDamage;
        break;
      case RelicType.OverclockChip:
        relicFireRate += RelicCatalog.OverclockFireRate;
        break;
      case RelicType.RicochetMirror:
        relicBounces++;
        break;
      case RelicType.SwiftBoots:
        relicMoveSpeed += RelicCatalog.BootsMoveSpeed;
        break;
      case RelicType.HomingCore:
        PlayerShooting gun = GetComponent<PlayerShooting>();
        if (gun != null)
        {
          gun.UnlockWeapon(WeaponType.Homing);
          gun.SetWeapon(WeaponType.Homing);
        }
        break;
    }
  }

  public void Update()
  {
    if (shooting == null) shooting = GetComponent<PlayerShooting>();
    if (movement == null) movement = GetComponent<PlayerMovement>();

    if (shooting != null && shooting.IsFiring)
    {
      ricochetTimer = Mathf.Max(0f, ricochetTimer - Time.deltaTime);
      fireRateTimer = Mathf.Max(0f, fireRateTimer - Time.deltaTime);
    }

    if (movement != null && movement.IsMoving)
    {
      moveSpeedTimer = Mathf.Max(0f, moveSpeedTimer - Time.deltaTime);
    }
  }

  public void Collect(PowerUpType type)
  {
    // Cristais (todos menos a bateria de mana) melhoram de vez um atributo sorteado da cientista
    CharacterStats stats = GetComponent<CharacterStats>();
    if (type != PowerUpType.Mana && stats != null)
    {
      stats.Enhance();
      return;
    }

    switch (type)
    {
      case PowerUpType.Ricochet:
        ricochetTimer = timedDuration;
        break;
      case PowerUpType.MoveSpeed:
        moveSpeedTimer = timedDuration;
        break;
      case PowerUpType.FireRate:
        fireRateTimer = timedDuration;
        break;
      case PowerUpType.Health:
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null) health.IncreaseMax(healthIncrease, healthHeal);
        break;
      case PowerUpType.Battery:
        PlayerHealth charged = GetComponent<PlayerHealth>();
        if (charged != null) charged.IncreaseMax(0f, batteryHeal);
        break;
    }

    if (GuardiasDaLuz.MapSystem.StandaloneLighthouseMapSystem.Instance != null)
    {
      GuardiasDaLuz.MapSystem.StandaloneLighthouseMapSystem.Instance.ShowBannerMessage($"⚡ Power-up: {GetLabel(type)}!");
    }
  }

  public static string GetLabel(PowerUpType type)
  {
    switch (type)
    {
      case PowerUpType.Ricochet: return "Tiro que ricocheteia";
      case PowerUpType.Health: return "Mais vida";
      case PowerUpType.Battery: return "Bateria: recupera vida";
      case PowerUpType.MoveSpeed: return "Velocidade de movimento";
      default: return "Velocidade de tiro";
    }
  }

  public void OnGUI()
  {
    HudScale.Begin();
    DrawHud();
    HudScale.End();
  }

  private void DrawHud()
  {
    float y = HudScale.Height - 72f;
    DrawTimer(ref y, PowerUpType.Ricochet, ricochetTimer);
    DrawTimer(ref y, PowerUpType.MoveSpeed, moveSpeedTimer);
    DrawTimer(ref y, PowerUpType.FireRate, fireRateTimer);
    DrawRelics(y);
  }

  // Uma etiqueta por relíquia coletada (repetidas mostram "x2"), em fileiras acima dos power-ups
  private void DrawRelics(float bottom)
  {
    if (relics.Count == 0) return;

    GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
    style.normal.textColor = Color.white;
    const float height = 20f;
    const float maxWidth = 520f;
    float x = 20f;
    float y = bottom;

    foreach (RelicType relic in System.Enum.GetValues(typeof(RelicType)))
    {
      int count = 0;
      foreach (RelicType owned in relics) if (owned == relic) count++;
      if (count == 0) continue;

      string label = count > 1 ? $"{RelicCatalog.GetName(relic)} x{count}" : RelicCatalog.GetName(relic);
      float width = style.CalcSize(new GUIContent(label)).x + 16f;
      if (x > 20f && x + width > 20f + maxWidth)
      {
        x = 20f;
        y -= height + 4f;
      }

      Rect chip = new Rect(x, y, width, height);
      GUI.color = new Color(0f, 0f, 0f, 0.65f);
      GUI.DrawTexture(chip, Texture2D.whiteTexture);
      GUI.color = RelicCatalog.GetColor(relic);
      GUI.DrawTexture(new Rect(chip.x, chip.y, 4f, chip.height), Texture2D.whiteTexture);
      GUI.color = Color.white;
      GUI.Label(new Rect(chip.x + 9f, chip.y, chip.width - 9f, chip.height), label, style);
      x += width + 4f;
    }
  }

  private void DrawTimer(ref float y, PowerUpType type, float timer)
  {
    if (timer <= 0f)
    {
      return;
    }

    Rect bar = new Rect(20f, y, 240f, 20f);
    GUI.color = new Color(0f, 0f, 0f, 0.6f);
    GUI.DrawTexture(bar, Texture2D.whiteTexture);
    GUI.color = PowerUpPickup.GetColor(type);
    GUI.DrawTexture(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * timer / timedDuration, bar.height - 4f), Texture2D.whiteTexture);
    GUI.color = Color.white;
    GUI.Label(new Rect(bar.x + 8f, bar.y + 1f, bar.width, bar.height), $"{GetLabel(type)} {Mathf.CeilToInt(timer)}s");
    y -= 24f;
  }
}
