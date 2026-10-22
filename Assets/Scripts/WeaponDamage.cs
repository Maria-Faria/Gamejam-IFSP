using GuardiasDaLuz.MapSystem;
using UnityEngine;

public static class WeaponDamage
{
  public static MonoBehaviour FindTarget(Collider collider)
  {
    MonoBehaviour monster = collider.GetComponentInParent<StandalonePlaceholderMonsterTestAI>();
    if (monster != null)
    {
      return monster;
    }

    MonoBehaviour shadowEnemy = collider.GetComponentInParent<ShadowEnemy>();
    if (shadowEnemy != null)
    {
      return shadowEnemy;
    }

    MonoBehaviour crate = collider.GetComponentInParent<StandaloneBreakableCrate>();
    if (crate != null)
    {
      return crate;
    }

    return collider.GetComponentInParent<StandaloneSecretCrackedWall>();
  }

  public static void Damage(MonoBehaviour target, float damage)
  {
    switch (target)
    {
      case StandalonePlaceholderMonsterTestAI monster:
        monster.TakeDamage(damage);
        break;
      case ShadowEnemy shadowEnemy:
        shadowEnemy.TakeDamage(damage);
        break;
      case StandaloneBreakableCrate crate:
        crate.TakeHit();
        break;
      case StandaloneSecretCrackedWall wall:
        wall.RegisterProjectileHit();
        break;
    }
  }

  public static bool TryDamage(Collider collider, float damage)
  {
    MonoBehaviour target = FindTarget(collider);
    if (target == null)
    {
      return false;
    }

    Damage(target, damage);
    return true;
  }
}
