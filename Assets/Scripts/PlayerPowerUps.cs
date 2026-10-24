using UnityEngine;

public enum PowerUpType
{
  Ricochet,
  Health,
  Mana,
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
  [Tooltip("Quanto a mana máxima aumenta a cada coleta. A barra também enche.")]
  [SerializeField] private float manaIncrease = 20f;

  private float ricochetTimer;
  private float moveSpeedTimer;
  private float fireRateTimer;
  private PlayerShooting shooting;
  private PlayerMovement movement;

  public int RicochetBounces => ricochetTimer > 0f ? ricochetBounces : 0;
  public float MoveSpeedMultiplier => moveSpeedTimer > 0f ? moveSpeedMultiplier : 1f;
  public float FireRateMultiplier => fireRateTimer > 0f ? fireRateMultiplier : 1f;

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
      case PowerUpType.Mana:
        PlayerMana mana = GetComponent<PlayerMana>();
        if (mana != null) mana.IncreaseMax(manaIncrease);
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
      case PowerUpType.Mana: return "Mais mana";
      case PowerUpType.MoveSpeed: return "Velocidade de movimento";
      default: return "Velocidade de tiro";
    }
  }

  public void OnGUI()
  {
    float y = Screen.height - 98f;
    DrawTimer(ref y, PowerUpType.Ricochet, ricochetTimer);
    DrawTimer(ref y, PowerUpType.MoveSpeed, moveSpeedTimer);
    DrawTimer(ref y, PowerUpType.FireRate, fireRateTimer);
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
