using UnityEngine;

// O que um monstro deixa ao morrer: a mesma chance de power-up dos inimigos de sombra (ShadowEnemy).
// Colocado em todo prefab de monstro por Roguelike/Build Monsters.
[RequireComponent(typeof(Roguelike.Health))]
public class MonsterDrops : MonoBehaviour
{
  [Tooltip("Chance (0 a 1) de soltar um power-up ao morrer.")]
  [Range(0f, 1f)] [SerializeField] private float powerUpDropChance = 0.08f;

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

  private void OnDied()
  {
    PowerUpPickup.TrySpawn(transform.position, powerUpDropChance);
  }
}
