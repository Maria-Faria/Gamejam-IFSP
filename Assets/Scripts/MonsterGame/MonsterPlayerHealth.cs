using UnityEngine;

// Ponte entre os ataques dos monstros (Roguelike.Health) e a vida da jogadora (PlayerHealth).
// Os monstros procuram um Roguelike.Health no alvo; este componente é colocado automaticamente
// na jogadora (MonsterGameBridge) e repassa todo dano para PlayerHealth.TakeDamage, que continua
// sendo a única dona da vida da jogadora.
[DisallowMultipleComponent]
public class MonsterPlayerHealth : Roguelike.Health
{
  private PlayerHealth player;

  public PlayerHealth Player
  {
    get
    {
      Bind();
      return player;
    }
  }

  public override float Max => Player != null ? player.MaxHealth : base.Max;
  public override float Current => Player != null ? player.Health : 0f;
  public override bool IsDead => Player == null || player.IsDead;

  protected override void Awake()
  {
    base.Awake();
    Bind();
  }

  public override void TakeDamage(float amount, Vector3 from)
  {
    if (IsDead || amount <= 0f)
    {
      return;
    }

    if (!Invulnerable)
    {
      player.TakeDamage(amount);
    }
    RaiseDamaged(amount, from);
  }

  private void Bind()
  {
    if (player != null)
    {
      return;
    }

    player = GetComponent<PlayerHealth>();
    if (player != null)
    {
      player.Died += RaiseDied;
    }
  }

  private void OnDestroy()
  {
    if (player != null)
    {
      player.Died -= RaiseDied;
    }
  }
}
