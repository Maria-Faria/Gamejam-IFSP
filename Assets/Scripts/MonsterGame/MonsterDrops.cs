using UnityEngine;

// O que um monstro deixa ao morrer: moedas (para a loja de armas), às vezes um coração que devolve vida,
// e a mesma chance de power-up dos inimigos de sombra. Monstros maiores soltam mais moedas; os chefes
// soltam um punhado delas e sempre um coração. Colocado em todo prefab de monstro por Roguelike/Build
// Monsters.
[RequireComponent(typeof(Roguelike.Health))]
public class MonsterDrops : MonoBehaviour
{
  [Tooltip("Chance (0 a 1) de soltar um power-up ao morrer.")]
  [Range(0f, 1f)] [SerializeField] private float powerUpDropChance = 0.08f;
  [Tooltip("Chance (0 a 1) de soltar um coração.")]
  [Range(0f, 1f)] [SerializeField] private float heartDropChance = 0.12f;
  [Tooltip("Vida que um coração devolve.")]
  [SerializeField] private int heartHealing = 25;
  [Tooltip("Moedas que um chefe solta (em várias moedas).")]
  [SerializeField] private int bossCoins = 30;

  private Roguelike.Health health;

  public void Awake()
  {
    health = GetComponent<Roguelike.Health>();
    health.Died += OnDied;
  }

  private void OnDestroy()
  {
    if (health != null)
    {
      health.Died -= OnDied;
    }
  }

  // Moedas pelo tamanho do monstro (altura em relação à jogadora)
  public static int CoinsFor(Roguelike.Monster monster)
  {
    if (monster == null) return 1;
    if (monster.GetComponent<Roguelike.BossBrain>() != null) return 30;
    float relative = monster.Size / Roguelike.MonsterScale.PlayerSize;
    return relative < 0.7f ? 1 : relative < 1.05f ? 2 : 3;
  }

  private void OnDied()
  {
    Roguelike.Monster monster = GetComponent<Roguelike.Monster>();
    bool boss = monster != null && monster.GetComponent<Roguelike.BossBrain>() != null;
    Vector3 at = transform.position;

    if (boss)
    {
      int pieces = 10;
      for (int i = 0; i < pieces; i++) LootPickup.Spawn(LootPickup.Kind.Coin, at, Mathf.CeilToInt(bossCoins / (float)pieces));
      LootPickup.Spawn(LootPickup.Kind.Heart, at, heartHealing * 2);
    }
    else
    {
      int coins = CoinsFor(monster);
      for (int i = 0; i < coins; i++) LootPickup.Spawn(LootPickup.Kind.Coin, at, 1);
      if (Random.value < heartDropChance) LootPickup.Spawn(LootPickup.Kind.Heart, at, heartHealing);
    }

    PowerUpPickup.TrySpawn(at, powerUpDropChance);
  }
}
