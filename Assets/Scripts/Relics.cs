using System.Collections.Generic;
using UnityEngine;

// Itens dos pedestais (sala do tesouro e sala secreta): bônus permanentes até o fim da expedição
public enum RelicType
{
  HeartOfLight,
  FocusCrystal,
  OverclockChip,
  RicochetMirror,
  SwiftBoots,
  PrismLens,
  HomingCore
}

public static class RelicCatalog
{
  public const float HeartHealth = 30f;
  public const float FocusDamage = 0.25f;
  public const float OverclockFireRate = 0.2f;
  public const float BootsMoveSpeed = 0.15f;

  private static readonly RelicType[] Common =
  {
    RelicType.HeartOfLight, RelicType.FocusCrystal,
    RelicType.OverclockChip, RelicType.RicochetMirror, RelicType.SwiftBoots
  };

  // Mudam o jeito de atirar: só saem uma vez por expedição
  private static readonly RelicType[] Unique = { RelicType.HomingCore, RelicType.PrismLens };

  public static string GetName(RelicType relic)
  {
    switch (relic)
    {
      case RelicType.HeartOfLight: return "Coração de Luz";
      case RelicType.FocusCrystal: return "Cristal de Foco";
      case RelicType.OverclockChip: return "Chip Acelerador";
      case RelicType.RicochetMirror: return "Espelho Ricochete";
      case RelicType.SwiftBoots: return "Botas Velozes";
      case RelicType.PrismLens: return "Lente Prismática";
      default: return "Núcleo Teleguiado";
    }
  }

  public static string GetDescription(RelicType relic)
  {
    switch (relic)
    {
      case RelicType.HeartOfLight: return $"+{HeartHealth:0} de vida máxima e cura completa";
      case RelicType.FocusCrystal: return $"+{FocusDamage * 100f:0}% de dano em todos os tiros";
      case RelicType.OverclockChip: return $"+{OverclockFireRate * 100f:0}% de velocidade de tiro";
      case RelicType.RicochetMirror: return "os tiros ricocheteiam mais 1 vez nas paredes";
      case RelicType.SwiftBoots: return $"+{BootsMoveSpeed * 100f:0}% de velocidade de corrida";
      case RelicType.PrismLens: return "novo tiro: cada disparo sai em dobro, com um feixe gêmeo";
      default: return "nova arma [7]: tiros teleguiados que perseguem os monstros";
    }
  }

  public static Color GetColor(RelicType relic)
  {
    switch (relic)
    {
      case RelicType.HeartOfLight: return new Color(1f, 0.3f, 0.35f);
      case RelicType.FocusCrystal: return new Color(1f, 0.6f, 0.15f);
      case RelicType.OverclockChip: return new Color(1f, 0.9f, 0.2f);
      case RelicType.RicochetMirror: return new Color(0.3f, 0.9f, 1f);
      case RelicType.SwiftBoots: return new Color(0.35f, 1f, 0.5f);
      case RelicType.PrismLens: return new Color(1f, 0.45f, 0.95f);
      default: return new Color(0.2f, 1f, 0.85f);
    }
  }

  public static bool IsUnique(RelicType relic) => System.Array.IndexOf(Unique, relic) >= 0;

  // Sorteia relíquias diferentes entre si. A sala secreta prefere as que mudam o tiro.
  // Usa um gerador próprio para não mexer no sorteio do mapa (que precisa ser igual para todas no co-op).
  public static RelicType[] Draw(int count, int seed, bool legendary, PlayerPowerUps owner, ICollection<RelicType> alreadyOffered)
  {
    System.Random random = new System.Random(seed);
    List<RelicType> pool = new List<RelicType>();
    foreach (RelicType relic in Unique)
    {
      bool taken = (owner != null && owner.HasRelic(relic)) || (alreadyOffered != null && alreadyOffered.Contains(relic));
      if (!taken) pool.Add(relic);
    }
    if (!legendary || pool.Count < count) pool.AddRange(Common);

    RelicType[] drawn = new RelicType[Mathf.Min(count, pool.Count)];
    for (int i = 0; i < drawn.Length; i++)
    {
      int pick = random.Next(pool.Count);
      drawn[i] = pool[pick];
      pool.RemoveAt(pick);
    }
    return drawn;
  }
}
