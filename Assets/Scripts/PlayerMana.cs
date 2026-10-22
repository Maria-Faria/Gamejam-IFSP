using UnityEngine;

public class PlayerMana : MonoBehaviour
{
  [SerializeField] private float maxMana = 100f;
  [Tooltip("Mana recuperada por segundo.")]
  [SerializeField] private float regenPerSecond = 20f;

  private float mana;
  private float emptyFlash;

  public float Mana => mana;
  public float MaxMana => maxMana;

  public void Awake()
  {
    mana = maxMana;
  }

  public void Update()
  {
    mana = Mathf.Min(maxMana, mana + regenPerSecond * Time.deltaTime);
    emptyFlash = Mathf.MoveTowards(emptyFlash, 0f, Time.deltaTime * 4f);
  }

  public bool TrySpend(float amount)
  {
    if (mana < amount)
    {
      emptyFlash = 1f;
      return false;
    }

    mana -= amount;
    return true;
  }

  public void IncreaseMax(float amount)
  {
    maxMana += amount;
    mana = maxMana;
  }

  public void OnGUI()
  {
    Rect bar = new Rect(20f, Screen.height - 72f, 240f, 22f);
    GUI.color = new Color(0f, 0f, 0f, 0.6f);
    GUI.DrawTexture(bar, Texture2D.whiteTexture);
    GUI.color = Color.Lerp(new Color(0.6f, 0.3f, 1f), Color.white, emptyFlash * 0.6f);
    GUI.DrawTexture(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * mana / maxMana, bar.height - 4f), Texture2D.whiteTexture);
    GUI.color = Color.white;
    GUI.Label(new Rect(bar.x + 8f, bar.y + 2f, bar.width, bar.height), $"Mana {Mathf.FloorToInt(mana)}/{Mathf.CeilToInt(maxMana)}");
  }
}
