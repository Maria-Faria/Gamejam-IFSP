using UnityEngine;

// O palco escondido da escolha de personagem: as sete cientistas em 3D (os mesmos modelos rigados do jogo,
// paradas na pose de respiração, cada uma girando devagar), longe da cena e numa camada só delas, cada uma
// filmada por uma câmera própria numa RenderTexture. O carrossel do menu (CharacterSelect) desenha essas
// texturas no lugar dos retratos. As câmeras só renderizam enquanto o carrossel está na tela.
public class CharacterPreviewStage : MonoBehaviour
{
  private const int Layer = 31;
  private const int Width = 512;
  private const int Height = 768;
  private static readonly Vector3 Origin = new Vector3(0f, -500f, 0f);
  private const float Spacing = 8f;

  private static CharacterPreviewStage instance;

  private Camera[] cameras;
  private RenderTexture[] textures;
  private Transform[] turntables;
  private int lastUsedFrame = -10;
  private int focused = -1;

  public static CharacterPreviewStage Ensure()
  {
    if (instance != null) return instance;
    instance = new GameObject("Palco_Escolha_Personagem").AddComponent<CharacterPreviewStage>();
    instance.Build();
    return instance;
  }

  // Textura da cientista `index`, ou null sem os modelos gerados
  public Texture TextureFor(int index)
  {
    lastUsedFrame = Time.frameCount;
    if (textures == null || index < 0 || index >= textures.Length || cameras[index] == null) return null;
    if (!cameras[index].enabled) SetRendering(true);
    return textures[index];
  }

  // A do centro do carrossel ergue a pistola, como quem está pronta para jogar
  public void Focus(int index)
  {
    focused = index;
  }

  private void Build()
  {
    transform.position = Origin;
    int count = CharacterRoster.Count;
    cameras = new Camera[count];
    textures = new RenderTexture[count];
    turntables = new Transform[count];

    // luz principal de frente (do lado das câmeras), um preenchimento frio do outro lado e um contorno atrás
    AddLight("Luz_Principal", Quaternion.Euler(28f, -25f, 0f), 1.5f, new Color(1f, 0.96f, 0.9f));
    AddLight("Luz_Preenchimento", Quaternion.Euler(15f, 40f, 0f), 0.6f, new Color(0.75f, 0.85f, 1f));
    AddLight("Luz_Contorno", Quaternion.Euler(30f, 170f, 0f), 0.8f, new Color(0.7f, 0.6f, 1f));

    for (int i = 0; i < count; i++)
    {
      CharacterRig prefab = CharacterRoster.LoadPrefab(i);
      if (prefab == null) continue;

      Transform table = new GameObject($"Giratorio_{i}").transform;
      table.SetParent(transform, false);
      table.localPosition = new Vector3(i * Spacing, 0f, 0f);
      table.localRotation = Quaternion.Euler(0f, 180f, 0f);
      turntables[i] = table;
      CharacterRig rig = Instantiate(prefab, table, false);
      foreach (Transform t in rig.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
      foreach (Renderer r in rig.GetComponentsInChildren<Renderer>(true))
      {
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
      }

      textures[i] = new RenderTexture(Width, Height, 24) { name = $"Personagem_{i}", antiAliasing = 4 };
      GameObject camObject = new GameObject($"Camera_{i}");
      camObject.transform.SetParent(transform, false);
      float h = rig.Height;
      camObject.transform.localPosition = new Vector3(i * Spacing, h * 0.55f, -h * 2.4f);
      camObject.transform.LookAt(table.position + Vector3.up * h * 0.5f);
      Camera cam = camObject.AddComponent<Camera>();
      cam.targetTexture = textures[i];
      cam.cullingMask = 1 << Layer;
      cam.clearFlags = CameraClearFlags.SolidColor;
      cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
      cam.fieldOfView = 28f;
      cam.nearClipPlane = 0.1f;
      cam.farClipPlane = 30f;
      cam.enabled = false;
      cameras[i] = cam;
    }
  }

  private void AddLight(string name, Quaternion rotation, float intensity, Color color)
  {
    GameObject go = new GameObject(name);
    go.transform.SetParent(transform, false);
    go.transform.rotation = rotation;
    Light light = go.AddComponent<Light>();
    light.type = LightType.Directional;
    light.intensity = intensity;
    light.color = color;
    light.cullingMask = 1 << Layer;
    light.shadows = LightShadows.None;
  }

  private void SetRendering(bool on)
  {
    foreach (Camera cam in cameras)
    {
      if (cam != null) cam.enabled = on;
    }
  }

  public void Update()
  {
    if (Time.frameCount - lastUsedFrame > 3)
    {
      SetRendering(false);
      return;
    }

    for (int i = 0; i < turntables.Length; i++)
    {
      if (turntables[i] == null) continue;
      // a do centro fica de frente; as outras giram devagar
      float target = i == focused ? 180f + Mathf.Sin(Time.unscaledTime * 0.8f) * 18f : 180f + Time.unscaledTime * 25f + i * 50f;
      turntables[i].localRotation = Quaternion.Slerp(turntables[i].localRotation, Quaternion.Euler(0f, target, 0f), 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
      CharacterRig rig = turntables[i].GetComponentInChildren<CharacterRig>();
      if (rig != null) rig.SetAim(i == focused);
    }
  }

  public void OnDestroy()
  {
    if (textures != null)
    {
      foreach (RenderTexture texture in textures)
      {
        if (texture != null) texture.Release();
      }
    }
    if (instance == this) instance = null;
  }
}
