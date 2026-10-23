using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
  [SerializeField] private float maxHealth = 100f;
  [Tooltip("Segundos até reiniciar a cena depois que a vida chega a zero.")]
  [SerializeField] private float restartDelay = 2.5f;
  [SerializeField] private AudioClip hitSound;
  [Tooltip("Toca no golpe fatal, no lugar do som de dano.")]
  [SerializeField] private AudioClip deathSound;
  [Tooltip("Intervalo mínimo entre dois sons de dano, para vários tiros simultâneos não empilharem.")]
  [SerializeField] private float hitSoundInterval = 0.1f;

  private float health;
  private float hitFlash;
  private bool dead;
  private AudioSource audioSource;
  private float nextHitSoundTime;

  public float Health => health;
  public float MaxHealth => maxHealth;
  public bool IsDead => dead;

  public event System.Action Died;
  // Disparado a cada dano recebido (inclusive o fatal), com o valor do dano. Usado pela animação de dor.
  public event System.Action<float> Damaged;

  public void Awake()
  {
    health = maxHealth;
    audioSource = GetComponent<AudioSource>();
  }

  public void Update()
  {
    hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.deltaTime * 3f);
  }

  public void TakeDamage(float damage)
  {
    if (dead)
    {
      return;
    }

    health = Mathf.Max(0f, health - damage);
    hitFlash = 1f;
    Damaged?.Invoke(damage);

    if (health > 0f)
    {
      PlayHitSound();
    }
    else
    {
      dead = true;
      if (audioSource != null && deathSound != null) audioSource.PlayOneShot(deathSound);
      Died?.Invoke();
      Invoke(nameof(Restart), restartDelay);
    }
  }

  public void IncreaseMax(float amount, float heal)
  {
    if (dead)
    {
      return;
    }

    maxHealth += amount;
    health = Mathf.Min(maxHealth, health + heal);
  }

  private void PlayHitSound()
  {
    if (audioSource == null || hitSound == null || Time.time < nextHitSoundTime)
    {
      return;
    }

    audioSource.PlayOneShot(hitSound);
    nextHitSoundTime = Time.time + hitSoundInterval;
  }

  private void Restart()
  {
    // Espera a animação de morte terminar antes de reiniciar
    PlayerAnimation playerAnimation = GetComponent<PlayerAnimation>();
    if (playerAnimation != null && playerAnimation.IsPlayingDeath)
    {
      Invoke(nameof(Restart), 0.1f);
      return;
    }

    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
  }

  public void OnGUI()
  {
    if (hitFlash > 0f)
    {
      GUI.color = new Color(0.8f, 0f, 0f, 0.3f * hitFlash);
      GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
    }

    Rect bar = new Rect(20f, Screen.height - 44f, 240f, 24f);
    GUI.color = new Color(0f, 0f, 0f, 0.6f);
    GUI.DrawTexture(bar, Texture2D.whiteTexture);
    GUI.color = new Color(0.9f, 0.2f, 0.25f);
    GUI.DrawTexture(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * health / maxHealth, bar.height - 4f), Texture2D.whiteTexture);
    GUI.color = Color.white;
    GUI.Label(new Rect(bar.x + 8f, bar.y + 3f, bar.width, bar.height), $"Vida {Mathf.CeilToInt(health)}/{Mathf.CeilToInt(maxHealth)}");

    if (dead)
    {
      GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
      GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "A escuridão venceu...", style);
    }
  }
}
