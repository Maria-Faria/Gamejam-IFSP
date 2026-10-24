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

    // Monstros (Assets/Scripts/Monsters): a vida deles é um Roguelike.Health.
    MonoBehaviour roguelikeMonster = MonsterGameBridge.FindMonsterHealth(collider);
    if (roguelikeMonster != null)
    {
      return roguelikeMonster;
    }

    MonoBehaviour barrel = collider.GetComponentInParent<ExplosiveBarrel>();
    if (barrel != null)
    {
      return barrel;
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
      case Roguelike.Health roguelikeMonster:
        roguelikeMonster.TakeDamage(damage, roguelikeMonster.transform.position);
        break;
      case ExplosiveBarrel barrel:
        barrel.TakeHit(damage);
        break;
      case StandaloneBreakableCrate crate:
        crate.TakeHit();
        break;
      case StandaloneSecretCrackedWall wall:
        wall.RegisterProjectileHit();
        break;
    }
  }

  // Empurra o monstro dono deste collider (tiros da jogadora); outros alvos ignoram
  public static void Push(Collider collider, Vector3 velocity)
  {
    if (collider == null || velocity.sqrMagnitude < 0.01f)
    {
      return;
    }

    Roguelike.Monster monster = collider.GetComponentInParent<Roguelike.Monster>();
    if (monster != null && !monster.Dead)
    {
      monster.Push(velocity);
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
