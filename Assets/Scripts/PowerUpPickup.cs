using UnityEngine;

public class PowerUpPickup : MonoBehaviour
{
  private const float PickupDistance = 1.6f;
  private const float Lifetime = 30f;
  private const float BlinkTime = 5f;

  private const float MinSecondsBetweenDrops = 10f;
  private const float MaxSecondsBetweenDrops = 25f;

  private const float ModelSize = 0.75f;
  public const string BatteryModelName = "battery";
  public const string CrystalModelName = "crystal";

  private const string SoundFolder = "Assets/Audios/Projeto_UV_4_PowerUps_SFX";
  public const string LightCrystalSoundName = "01_PowerUp_Cristal_de_Luz";
  public const string EnergySoundName = "02_PowerUp_Carga_Energetica";
  public const string RetroTechSoundName = "03_PowerUp_Tecnologia_Retro";
  public const string SpecialSoundName = "04_PowerUp_Melhoria_Especial";

  private static PlayerPowerUps player;
  private static float nextDropTime;
  private static int lastType = -1;

  private PowerUpType type;
  private Renderer[] bodies;
  private float baseHeight;
  private float age;

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void ResetStatics()
  {
    player = null;
    nextDropTime = 0f;
    lastType = -1;
  }

  public static void TrySpawn(Vector3 position, float chance)
  {
    if (Time.time < nextDropTime || Random.value > chance)
    {
      return;
    }

    int count = System.Enum.GetValues(typeof(PowerUpType)).Length;
    int index = Random.Range(0, count);
    if (index == lastType)
    {
      index = (index + Random.Range(1, count)) % count;
    }

    lastType = index;
    nextDropTime = Time.time + Random.Range(MinSecondsBetweenDrops, MaxSecondsBetweenDrops);
    Spawn(position, (PowerUpType)index);
  }

  public static void Spawn(Vector3 position, PowerUpType type)
  {
    GameObject model = GetModel(type);
    GameObject pickupObject;
    if (model != null)
    {
      pickupObject = new GameObject();
      CreateModelVisual(model, pickupObject.transform, ModelSize);
    }
    else
    {
      pickupObject = GameObject.CreatePrimitive(GetShape(type));
      Object.Destroy(pickupObject.GetComponent<Collider>());
      pickupObject.transform.localScale = Vector3.one * 0.5f;
    }
    pickupObject.name = $"PowerUp_{type}";
    pickupObject.transform.position = new Vector3(position.x, 0.8f, position.z);

    PowerUpPickup pickup = pickupObject.AddComponent<PowerUpPickup>();
    pickup.type = type;
    pickup.bodies = pickupObject.GetComponentsInChildren<Renderer>();
    pickup.baseHeight = pickupObject.transform.position.y;

    bool tint = model == null || type != PowerUpType.Battery;
    foreach (Renderer body in pickup.bodies)
    {
      if (tint) Tint(body, GetColor(type));
      body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
  }

  // A bateria (recupera vida) usa o modelo com as cores originais; os outros tipos usam o cristal tingido com a cor do power-up
  private static GameObject GetModel(PowerUpType type)
  {
    return LoadModel(type == PowerUpType.Battery ? BatteryModelName : CrystalModelName);
  }

  public static GameObject LoadModel(string modelName)
  {
    var map = GuardiasDaLuz.MapSystem.StandaloneLighthouseMapSystem.Instance;
    if (map != null)
    {
      GameObject assigned = modelName == BatteryModelName ? map.powerUpBatteryModel : map.powerUpCrystalModel;
      if (assigned != null) return assigned;
    }
#if UNITY_EDITOR
    return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Powerups/{modelName}.fbx");
#else
    return null;
#endif
  }

  private static string GetSoundName(PowerUpType type)
  {
    switch (type)
    {
      case PowerUpType.Health: return LightCrystalSoundName;
      case PowerUpType.Battery: return EnergySoundName;
      case PowerUpType.MoveSpeed: return SpecialSoundName;
      default: return RetroTechSoundName;
    }
  }

  public static AudioClip LoadSound(string soundName)
  {
    var map = GuardiasDaLuz.MapSystem.StandaloneLighthouseMapSystem.Instance;
    if (map != null)
    {
      AudioClip assigned = null;
      switch (soundName)
      {
        case LightCrystalSoundName: assigned = map.powerUpLightCrystalSound; break;
        case EnergySoundName: assigned = map.powerUpEnergySound; break;
        case RetroTechSoundName: assigned = map.powerUpRetroTechSound; break;
        case SpecialSoundName: assigned = map.powerUpSpecialSound; break;
      }
      if (assigned != null) return assigned;
    }
    return LoadSoundAsset(soundName);
  }

  public static AudioClip LoadSoundAsset(string soundName)
  {
#if UNITY_EDITOR
    return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundFolder}/{soundName}.wav");
#else
    return null;
#endif
  }

  // Som 2D, para tocar com o mesmo volume onde quer que a câmera esteja
  public static void PlaySound(AudioClip clip)
  {
    if (clip == null) return;

    var map = GuardiasDaLuz.MapSystem.StandaloneLighthouseMapSystem.Instance;
    GameObject sound = new GameObject($"SFX_{clip.name}");
    AudioSource source = sound.AddComponent<AudioSource>();
    source.spatialBlend = 0f;
    source.PlayOneShot(clip, map != null ? map.powerUpSoundVolume : 0.9f);
    Destroy(sound, clip.length + 0.1f);
  }

  public static GameObject CreateModelVisual(GameObject model, Transform parent, float size)
  {
    Bounds bounds = GuardiasDaLuz.MapSystem.RoomDecorKit.GetLocalBounds(model);
    float scale = size / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.01f);

    GameObject wrapper = new GameObject($"Modelo_{model.name}");
    wrapper.transform.SetParent(parent, false);
    wrapper.transform.localScale = Vector3.one * scale;

    GameObject instance = Object.Instantiate(model, wrapper.transform, false);
    instance.transform.localPosition = model.transform.localPosition - bounds.center;
    foreach (Camera camera in instance.GetComponentsInChildren<Camera>(true)) Object.Destroy(camera.gameObject);
    foreach (Light light in instance.GetComponentsInChildren<Light>(true)) Object.Destroy(light.gameObject);
    foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
    return wrapper;
  }

  public static void Tint(Renderer body, Color color)
  {
    foreach (Material material in body.materials)
    {
      material.color = color;
      if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
    }
  }

  public static Color GetColor(PowerUpType type)
  {
    switch (type)
    {
      case PowerUpType.Ricochet: return new Color(0.3f, 0.9f, 1f);
      case PowerUpType.Health: return new Color(0.95f, 0.25f, 0.3f);
      case PowerUpType.Battery: return new Color(0.3f, 1f, 0.55f);
      case PowerUpType.MoveSpeed: return new Color(0.35f, 1f, 0.45f);
      default: return new Color(1f, 0.8f, 0.2f);
    }
  }

  private static PrimitiveType GetShape(PowerUpType type)
  {
    switch (type)
    {
      case PowerUpType.Ricochet: return PrimitiveType.Sphere;
      case PowerUpType.Health: return PrimitiveType.Sphere;
      case PowerUpType.Battery: return PrimitiveType.Sphere;
      case PowerUpType.MoveSpeed: return PrimitiveType.Capsule;
      default: return PrimitiveType.Capsule;
    }
  }

  public void Update()
  {
    age += Time.deltaTime;
    if (age >= Lifetime)
    {
      Destroy(gameObject);
      return;
    }

    transform.Rotate(30f * Time.deltaTime, 120f * Time.deltaTime, 0f);
    Vector3 position = transform.position;
    position.y = baseHeight + Mathf.Sin(age * 3f) * 0.15f;
    transform.position = position;
    bool visible = age < Lifetime - BlinkTime || Mathf.Repeat(age * 6f, 1f) < 0.6f;
    foreach (Renderer body in bodies)
    {
      body.enabled = visible;
    }

    if (player == null)
    {
      player = FindAnyObjectByType<PlayerPowerUps>();
      if (player == null) return;
    }

    Vector3 offset = player.transform.position - transform.position;
    offset.y = 0f;
    if (offset.magnitude <= PickupDistance)
    {
      player.Collect(type);
      PlaySound(LoadSound(GetSoundName(type)));
      Destroy(gameObject);
    }
  }
}
