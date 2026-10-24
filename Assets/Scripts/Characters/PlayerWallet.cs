using UnityEngine;

// As moedas da jogadora: caem dos monstros (LootPickup) e compram as armas especiais na loja de cada
// andar (WeaponShop). Valem a partida inteira (morrer recomeça do zero).
public class PlayerWallet : MonoBehaviour
{
  [SerializeField] private int coins;

  private float flash;
  private GUIStyle style;

  public int Coins => coins;

  public static PlayerWallet Of(Component anyPart)
  {
    if (anyPart == null) return null;
    PlayerWallet wallet = anyPart.GetComponentInParent<PlayerWallet>();
    if (wallet != null) return wallet;
    PlayerMovement movement = anyPart.GetComponentInParent<PlayerMovement>();
    return movement != null ? movement.gameObject.AddComponent<PlayerWallet>() : null;
  }

  public void Add(int amount)
  {
    if (amount <= 0) return;
    coins += amount;
    flash = 1f;
  }

  public bool TrySpend(int amount)
  {
    if (amount < 0 || coins < amount) return false;
    coins -= amount;
    flash = 1f;
    return true;
  }

  public void Update()
  {
    flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 2.5f);
  }

  public void OnGUI()
  {
    if (style == null)
    {
      style = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
    }
    float scale = 1f + flash * 0.25f;
    style.fontSize = Mathf.RoundToInt(22 * scale);
    Rect box = new Rect(Screen.width - 220f, Screen.height - 230f, 200f, 34f);
    GUI.color = new Color(0f, 0f, 0f, 0.55f);
    GUI.DrawTexture(new Rect(box.x + 90f, box.y, 110f, box.height), Texture2D.whiteTexture);
    GUI.color = Color.Lerp(new Color(1f, 0.82f, 0.3f), Color.white, flash);
    GUI.Label(new Rect(box.x, box.y, box.width - 10f, box.height), $"● {coins}", style);
    GUI.color = Color.white;
  }
}
