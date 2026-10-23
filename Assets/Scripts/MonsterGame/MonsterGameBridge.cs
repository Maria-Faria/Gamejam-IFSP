using GuardiasDaLuz.MapSystem;
using Roguelike;
using UnityEngine;

// Liga os monstros (assembly Roguelike, Assets/Scripts/Monsters) ao jogo:
// - os monstros têm como alvo a jogadora (o mesmo objeto que o mapa e a câmera usam);
// - o dano dos ataques deles vai para PlayerHealth (via MonsterPlayerHealth);
// - as armas da jogadora acham e ferem os monstros pelo WeaponDamage (FindMonsterHealth).
public static class MonsterGameBridge
{
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void Install()
  {
    MonsterTargeting.FindPlayer = FindPlayer;
    MonsterTargeting.PrepareTarget = PrepareTarget;
  }

  // A jogadora: a do mapa, se houver, senão quem tem PlayerHealth / PlayerMovement na cena.
  public static Transform FindPlayer()
  {
    StandaloneLighthouseMapSystem map = StandaloneLighthouseMapSystem.Instance;
    if (map != null && map.playerTransform != null && map.playerTransform.GetComponentInParent<PlayerMovement>() != null)
    {
      return map.playerTransform;
    }

    PlayerHealth health = Object.FindFirstObjectByType<PlayerHealth>();
    if (health != null)
    {
      return health.transform;
    }

    PlayerMovement movement = Object.FindFirstObjectByType<PlayerMovement>();
    return movement != null ? movement.transform : null;
  }

  // Garante que um alvo que é a jogadora possa levar dano dos monstros.
  public static void PrepareTarget(Transform target)
  {
    if (target == null)
    {
      return;
    }

    GameObject owner = null;
    PlayerHealth health = target.GetComponentInParent<PlayerHealth>();
    if (health != null)
    {
      owner = health.gameObject;
    }
    else
    {
      PlayerMovement movement = target.GetComponentInParent<PlayerMovement>();
      owner = movement != null ? movement.gameObject : null;
    }
    if (owner != null && owner.GetComponent<MonsterPlayerHealth>() == null)
    {
      owner.AddComponent<MonsterPlayerHealth>();
    }
  }

  // A vida do monstro dono deste collider (vivo), para as armas da jogadora.
  public static Roguelike.Health FindMonsterHealth(Collider collider)
  {
    Monster monster = collider != null ? collider.GetComponentInParent<Monster>() : null;
    if (monster == null || monster.Health == null || monster.Health.IsDead)
    {
      return null;
    }
    return monster.Health;
  }
}
