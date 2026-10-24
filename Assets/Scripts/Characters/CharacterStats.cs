using UnityEngine;

// Os atributos da cientista em jogo: os da personagem escolhida (CharacterRoster) mais o que os cristais
// já melhoraram. Cada cristal pego melhora um atributo sorteado (Enhance). Vida e mana máximas são
// aplicadas no PlayerHealth / PlayerMana; velocidade, cadência e dano são lidos por PlayerMovement e
// PlayerShooting.
public class CharacterStats : MonoBehaviour
{
  public enum Attribute { Vida, Velocidade, Cadencia, Dano, Energia }

  private static readonly string[] Labels = { "Vida", "Velocidade", "Cadência de tiro", "Dano", "Energia" };

  [Tooltip("Quanto cada cristal melhora o atributo sorteado.")]
  [SerializeField] private float healthStep = 15f;
  [SerializeField] private float speedStep = 0.08f;
  [SerializeField] private float fireRateStep = 0.1f;
  [SerializeField] private float damageStep = 0.12f;
  [SerializeField] private float manaStep = 20f;
  [SerializeField] private float manaRegenStep = 0.1f;

  private CharacterRoster.Stats stats;
  private readonly int[] boosts = new int[5];
  private bool applied;

  public int CharacterIndex { get; private set; }
  public string CharacterName => CharacterRoster.Get(CharacterIndex).Name;
  public float MaxHealth => stats.maxHealth;
  public float MoveSpeed => stats.moveSpeed;
  public float FireRate => stats.fireRate;
  public float Damage => stats.damage;
  public float MaxMana => stats.maxMana;
  public float ManaRegen => stats.manaRegen;
  public int Boosts(Attribute attribute) => boosts[(int)attribute];
  public static string Label(Attribute attribute) => Labels[(int)attribute];

  public static CharacterStats Of(Component anyPart)
  {
    return anyPart != null ? anyPart.GetComponentInParent<CharacterStats>() : null;
  }

  // Multiplicadores para quem ainda não tem uma personagem (testes, cenas antigas): tudo normal
  public static float SpeedOf(Component c) { CharacterStats s = Of(c); return s != null ? s.MoveSpeed : 1f; }
  public static float FireRateOf(Component c) { CharacterStats s = Of(c); return s != null ? s.FireRate : 1f; }
  public static float DamageOf(Component c) { CharacterStats s = Of(c); return s != null ? s.Damage : 1f; }

  public void Choose(int index)
  {
    CharacterIndex = Mathf.Clamp(index, 0, CharacterRoster.Count - 1);
    stats = CharacterRoster.Get(CharacterIndex).Stats;
    System.Array.Clear(boosts, 0, boosts.Length);
    applied = false;
    Apply(true);
  }

  public void Start()
  {
    Apply(true);
  }

  // Melhora um atributo sorteado; devolve qual foi
  public Attribute Enhance()
  {
    Attribute attribute = (Attribute)Random.Range(0, boosts.Length);
    Enhance(attribute);
    return attribute;
  }

  public void Enhance(Attribute attribute)
  {
    switch (attribute)
    {
      case Attribute.Vida: stats.maxHealth += healthStep; break;
      case Attribute.Velocidade: stats.moveSpeed += speedStep; break;
      case Attribute.Cadencia: stats.fireRate += fireRateStep; break;
      case Attribute.Dano: stats.damage += damageStep; break;
      case Attribute.Energia: stats.maxMana += manaStep; stats.manaRegen += manaRegenStep; break;
    }
    boosts[(int)attribute]++;
    Apply(false, attribute == Attribute.Vida ? healthStep : 0f);

    var map = GuardiasDaLuz.MapSystem.StandaloneLighthouseMapSystem.Instance;
    if (map != null) map.ShowBannerMessage($"💎 Cristal: {Label(attribute)} melhorou! (+{boosts[(int)attribute]})");
  }

  private void Apply(bool fill, float heal = 0f)
  {
    if (stats.maxHealth <= 0f) stats = CharacterRoster.Get(CharacterIndex).Stats;
    applied = true;
    PlayerHealth health = GetComponent<PlayerHealth>();
    if (health != null) health.SetMax(stats.maxHealth, fill, heal);
    PlayerMana mana = GetComponent<PlayerMana>();
    if (mana != null) mana.SetMax(stats.maxMana, stats.manaRegen, fill);
  }

  public void OnGUI()
  {
    GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 13 };
    style.normal.textColor = new Color(0.85f, 0.9f, 1f);
    string boostsText = "";
    for (int i = 0; i < boosts.Length; i++)
    {
      if (boosts[i] > 0) boostsText += $"  {Labels[i]} +{boosts[i]}";
    }
    GUI.Label(new Rect(20f, Screen.height - 20f, 600f, 20f), $"{CharacterName}{boostsText}", style);
  }
}
