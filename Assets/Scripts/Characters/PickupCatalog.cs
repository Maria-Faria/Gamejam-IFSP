using UnityEngine;

// Modelos usados pelo jogo em tempo de execução que não estão em nenhuma cena: moeda e coração
// (Assets/Powerups/coin.fbx e heart.fbx) e a pistola (para a loja mostrar as armas). Preenchido por
// Roguelike/Build Characters em Assets/Characters/Resources/PickupCatalog.asset.
public class PickupCatalog : ScriptableObject
{
  public const string ResourceName = "PickupCatalog";

  public GameObject coin;
  public GameObject heart;
  public GameObject pistol;

  private static PickupCatalog loaded;

  public static PickupCatalog Load()
  {
    if (loaded == null) loaded = Resources.Load<PickupCatalog>(ResourceName);
    return loaded;
  }
}
