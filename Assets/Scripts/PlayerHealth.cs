using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
  [SerializeField] private float maxHealth = 100f;
  [Tooltip("Segundos até reiniciar a cena depois que a vida chega a zero.")]
  [SerializeField] private float restartDelay = 2.5f;

  private float health;
  private float hitFlash;
  private bool dead;

  public float Health => health;
  public float MaxHealth => maxHealth;

  public event System.Action Died;

  public void Awake()
  {
    health = maxHealth;
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

    if (health <= 0f)
    {
      dead = true;
      Died?.Invoke();
      Invoke(nameof(Restart), restartDelay);
    }
  }

  private void Restart()
  {
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
