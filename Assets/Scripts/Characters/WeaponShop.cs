using UnityEngine;

// A loja de armas, uma sala por andar (RoomType.Shop do mapa). As seis armas especiais ficam em pedestais
// em semicírculo, cada uma girando sobre o seu com o nome e o preço em cima; perto de uma, [E] compra com
// as moedas que os monstros soltam. Comprada uma vez, a arma fica com a jogadora até o fim da partida (as
// lojas dos andares seguintes mostram "comprada").
public class WeaponShop : MonoBehaviour
{
  public struct Offer
  {
    public WeaponType Weapon;
    public int Price;
    public Color Color;
    public string Description;

    public Offer(WeaponType weapon, int price, Color color, string description)
    {
      Weapon = weapon;
      Price = price;
      Color = color;
      Description = description;
    }
  }

  public static readonly Offer[] Offers =
  {
    new Offer(WeaponType.AimedTriple, 30, new Color(0.65f, 0.45f, 1f), "três tiros mirados"),
    new Offer(WeaponType.RapidFire, 45, new Color(1f, 0.85f, 0.35f), "rajada contínua"),
    new Offer(WeaponType.SineWave, 50, new Color(0.35f, 1f, 0.85f), "dois tiros em onda"),
    new Offer(WeaponType.Shotgun, 60, new Color(1f, 0.5f, 0.3f), "leque de 7 tiros, empurra forte"),
    new Offer(WeaponType.EightWayBurst, 70, new Color(0.4f, 0.75f, 1f), "tiros para todos os lados"),
    new Offer(WeaponType.Exploding, 90, new Color(1f, 0.35f, 0.6f), "projétil que explode em área"),
  };

  public static int PriceOf(WeaponType weapon)
  {
    foreach (Offer offer in Offers) if (offer.Weapon == weapon) return offer.Price;
    return 0;
  }

  // Monta a loja no centro da sala
  public static WeaponShop Build(Transform roomRoot, Vector3 center)
  {
    GameObject root = new GameObject("Loja_de_Armas");
    root.transform.SetParent(roomRoot, false);
    root.transform.position = center;
    WeaponShop shop = root.AddComponent<WeaponShop>();

    // balcão com a placa
    GameObject counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
    counter.name = "Balcao";
    counter.transform.SetParent(root.transform, false);
    counter.transform.localPosition = new Vector3(0f, 0.55f, 5.5f);
    counter.transform.localScale = new Vector3(6f, 1.1f, 1.2f);
    counter.GetComponent<Renderer>().material.color = new Color(0.18f, 0.2f, 0.26f);
    Label(root.transform, "LOJA DE ARMAS", new Vector3(0f, 2.6f, 5.5f), 0.12f, new Color(0.3f, 1f, 1f));
    Label(root.transform, "[E] perto de uma arma para comprar", new Vector3(0f, 1.9f, 5.5f), 0.045f, new Color(0.85f, 0.9f, 1f));

    // luz da loja: uma sala segura e clara
    GameObject lamp = new GameObject("Luz_Loja");
    lamp.transform.SetParent(root.transform, false);
    lamp.transform.localPosition = new Vector3(0f, 6f, 1f);
    Light light = lamp.AddComponent<Light>();
    light.type = LightType.Point;
    light.color = new Color(0.75f, 0.95f, 1f);
    light.range = 18f;
    light.intensity = 3f;

    for (int i = 0; i < Offers.Length; i++)
    {
      float angle = Mathf.Lerp(-70f, 70f, i / (float)(Offers.Length - 1));
      Vector3 at = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, 5.5f) + new Vector3(0f, 0f, -2f);
      WeaponStand.Create(root.transform, at, Offers[i]);
    }
    return shop;
  }

  public static TextMesh Label(Transform parent, string text, Vector3 localPosition, float size, Color color)
  {
    GameObject go = new GameObject($"Texto_{text}");
    go.transform.SetParent(parent, false);
    go.transform.localPosition = localPosition;
    TextMesh mesh = go.AddComponent<TextMesh>();
    mesh.text = text;
    mesh.characterSize = size;
    mesh.fontSize = 48;
    mesh.anchor = TextAnchor.MiddleCenter;
    mesh.alignment = TextAlignment.Center;
    mesh.color = color;
    Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    if (font != null)
    {
      mesh.font = font;
      go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }
    go.AddComponent<FaceCamera>();
    return mesh;
  }
}

// Um pedestal da loja com uma arma
public class WeaponStand : MonoBehaviour
{
  private const float Reach = 2.4f;

  private WeaponShop.Offer offer;
  private Transform display;
  private TextMesh priceLabel;
  private bool prompting;

  public WeaponShop.Offer Offer => offer;

  public static WeaponStand Create(Transform parent, Vector3 localPosition, WeaponShop.Offer offer)
  {
    GameObject root = new GameObject($"Pedestal_{offer.Weapon}");
    root.transform.SetParent(parent, false);
    root.transform.localPosition = localPosition;
    WeaponStand stand = root.AddComponent<WeaponStand>();
    stand.offer = offer;

    GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pillar.name = "Pedestal";
    pillar.transform.SetParent(root.transform, false);
    pillar.transform.localPosition = new Vector3(0f, 0.5f, 0f);
    pillar.transform.localScale = new Vector3(0.9f, 0.5f, 0.9f);
    Material stone = pillar.GetComponent<Renderer>().material;
    stone.color = new Color(0.22f, 0.24f, 0.3f);
    stone.EnableKeyword("_EMISSION");
    stone.SetColor("_EmissionColor", offer.Color * 0.25f);

    stand.display = new GameObject("Arma").transform;
    stand.display.SetParent(root.transform, false);
    stand.display.localPosition = new Vector3(0f, 1.45f, 0f);
    PickupCatalog catalog = PickupCatalog.Load();
    if (catalog != null && catalog.pistol != null)
    {
      GameObject gun = Instantiate(catalog.pistol, stand.display, false);
      gun.transform.localScale = Vector3.one * 3f;
      foreach (Renderer r in gun.GetComponentsInChildren<Renderer>())
      {
        foreach (Material m in r.materials)
        {
          m.EnableKeyword("_EMISSION");
          m.SetColor("_EmissionColor", offer.Color * 0.35f);
        }
      }
    }

    GameObject glowObject = new GameObject("Brilho");
    glowObject.transform.SetParent(root.transform, false);
    glowObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
    Light glow = glowObject.AddComponent<Light>();
    glow.type = LightType.Point;
    glow.color = offer.Color;
    glow.range = 3f;
    glow.intensity = 2f;
    LightField.Ignore(glow);

    WeaponShop.Label(root.transform, PlayerShooting.GetName(offer.Weapon), new Vector3(0f, 2.45f, 0f), 0.055f, offer.Color);
    stand.priceLabel = WeaponShop.Label(root.transform, "", new Vector3(0f, 2.15f, 0f), 0.045f, new Color(1f, 0.85f, 0.35f));
    return stand;
  }

  public void Update()
  {
    if (display != null) display.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);

    var map = GuardiasDaLuz.MapSystem.StandaloneLighthouseMapSystem.Instance;
    Transform player = map != null ? map.playerTransform : null;
    PlayerShooting shooting = player != null ? player.GetComponentInParent<PlayerShooting>() : null;
    bool owned = shooting != null && shooting.Owns(offer.Weapon);
    if (priceLabel != null) priceLabel.text = owned ? "comprada" : $"● {offer.Price} moedas";
    if (player == null) return;

    bool near = Vector3.Distance(Flat(transform.position), Flat(player.position)) <= Reach;
    if (!near)
    {
      if (prompting) map.SetHoverPrompt("");
      prompting = false;
      return;
    }

    PlayerWallet wallet = PlayerWallet.Of(player);
    int coins = wallet != null ? wallet.Coins : 0;
    prompting = true;
    string name = PlayerShooting.GetName(offer.Weapon);
    map.SetHoverPrompt(owned
      ? $"{name}: já é sua (troque de arma com 1-7 ou a roda do mouse)"
      : $"🛒 [E] Comprar {name} ({offer.Description}) por {offer.Price} moedas — você tem {coins}");

    if (!owned && GuardiasDaLuz.MapSystem.StandaloneSmartPost.IsInteractKeyJustPressed())
    {
      Buy(shooting, wallet);
    }
  }

  public bool Buy(PlayerShooting shooting, PlayerWallet wallet)
  {
    var map = GuardiasDaLuz.MapSystem.StandaloneLighthouseMapSystem.Instance;
    string name = PlayerShooting.GetName(offer.Weapon);
    if (shooting == null || wallet == null || shooting.Owns(offer.Weapon)) return false;
    if (!wallet.TrySpend(offer.Price))
    {
      if (map != null) map.ShowBannerMessage($"❌ Moedas insuficientes para {name}: precisa de {offer.Price}, você tem {wallet.Coins}.");
      return false;
    }
    shooting.Unlock(offer.Weapon);
    PowerUpPickup.PlaySound(PowerUpPickup.LoadSound(PowerUpPickup.SpecialSoundName));
    if (map != null) map.ShowBannerMessage($"✨ Você comprou {name}! Ela já está na sua mão.");
    return true;
  }

  private void OnDisable()
  {
    var map = GuardiasDaLuz.MapSystem.StandaloneLighthouseMapSystem.Instance;
    if (prompting && map != null) map.SetHoverPrompt("");
    prompting = false;
  }

  private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}

// Textos 3D da loja sempre de frente para a câmera
public class FaceCamera : MonoBehaviour
{
  public void LateUpdate()
  {
    Camera camera = Camera.main;
    if (camera != null) transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, camera.transform.up);
  }
}
