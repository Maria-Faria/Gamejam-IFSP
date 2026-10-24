using UnityEngine;

// As sete cientistas jogáveis, na ordem do carrossel da escolha de personagem (CharacterSelect), com o
// modelo rigado de cada uma (Assets/Characters/Resources/Characters/<Chave>.prefab, gerado por
// Roguelike/Build Characters a partir de Tools/make_characters.py) e os atributos que a tornam diferente.
public static class CharacterRoster
{
  public const string ResourceFolder = "Characters";

  // Atributos de uma personagem. Multiplicadores são relativos ao padrão (1 = normal).
  [System.Serializable]
  public struct Stats
  {
    public float maxHealth;
    public float moveSpeed;
    public float fireRate;
    public float damage;
    public float maxMana;
    public float manaRegen;

    public Stats(float maxHealth, float moveSpeed, float fireRate, float damage, float maxMana, float manaRegen)
    {
      this.maxHealth = maxHealth;
      this.moveSpeed = moveSpeed;
      this.fireRate = fireRate;
      this.damage = damage;
      this.maxMana = maxMana;
      this.manaRegen = manaRegen;
    }
  }

  public struct Entry
  {
    public string Key;
    public string Name;
    public string Strength;
    public Stats Stats;

    public Entry(string key, string name, string strength, Stats stats)
    {
      Key = key;
      Name = name;
      Strength = strength;
      Stats = stats;
    }
  }

  // Cada uma é melhor em uma ou duas coisas e paga em outra: a soma fica parecida entre todas.
  //                                                               vida  veloc. cadência dano  mana  recarga
  public static readonly Entry[] All =
  {
    new Entry("Enedina", "Enedina Alves Marques", "Resistente e forte: aguenta mais e acerta com força",
      new Stats(130f, 0.92f, 0.95f, 1.15f, 90f, 1f)),
    new Entry("Hertha", "Hertha Ayrton", "Arco elétrico: atira muito rápido",
      new Stats(95f, 1f, 1.3f, 0.9f, 110f, 1.05f)),
    new Entry("Maria", "Mária Telkes", "Rainha do Sol: muita energia e recarga rápida",
      new Stats(100f, 1f, 1f, 0.95f, 150f, 1.4f)),
    new Entry("Chien", "Chien-Shiung Wu", "Física nuclear: o tiro mais forte, mas é frágil",
      new Stats(85f, 0.97f, 0.85f, 1.45f, 100f, 1f)),
    new Entry("Patricia", "Patricia Bath", "Laser de precisão: tiro rápido e certeiro",
      new Stats(95f, 1.05f, 1.15f, 1.1f, 95f, 0.95f)),
    new Entry("Annie", "Annie Easley", "Programadora ágil: rápida e equilibrada",
      new Stats(105f, 1.15f, 1.1f, 0.95f, 100f, 1f)),
    new Entry("Bibha", "Bibha Chowdhuri", "Partícula veloz: a mais rápida de todas, porém frágil",
      new Stats(80f, 1.28f, 1.05f, 1f, 110f, 1.1f)),
  };

  public static int Count => All.Length;

  public static int Selected => Mathf.Clamp(PlayerPrefs.GetInt(CharacterSelect.SelectedCharacterKey, 0), 0, All.Length - 1);

  public static Entry Get(int index) => All[Mathf.Clamp(index, 0, All.Length - 1)];

  public static CharacterRig LoadPrefab(int index)
  {
    GameObject prefab = Resources.Load<GameObject>($"{ResourceFolder}/{Get(index).Key}");
    return prefab != null ? prefab.GetComponent<CharacterRig>() : null;
  }
}
