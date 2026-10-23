using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IntroComic : MonoBehaviour
{
  [Serializable]
  public class Panel
  {
    public Texture2D image;
    [Tooltip("Posição do quadrinho na página, em proporção (0 a 1) a partir do canto superior esquerdo.")]
    public Rect area;
    [Tooltip("Segundo da música em que o quadrinho surge.")]
    public float startTime;
  }

  [Tooltip("Cena carregada no fim da intro (precisa estar na lista de cenas do build).")]
  [SerializeField] private string nextScene = "SampleScene";
  [SerializeField] private Texture2D page;
  [SerializeField] private Panel[] panels;

  [Header("Música (os quadrinhos seguem o tempo da faixa)")]
  [SerializeField] private AudioClip music;
  [Range(0f, 1f)]
  [SerializeField] private float musicVolume = 0.6f;
  [SerializeField] private float musicFadeInSeconds = 2f;
  [Tooltip("Ao avançar com clique, a música pula para o trecho do quadrinho com uma queda rápida de volume, para não cortar seco.")]
  [SerializeField] private float seekDipSeconds = 0.25f;
  [Tooltip("Segundos de espera depois do fim da música antes de começar o jogo sozinho.")]
  [SerializeField] private float endHoldSeconds = 4f;
  [Tooltip("Segundos que o último quadrinho fica ampliado antes de a câmera mostrar a página inteira.")]
  [SerializeField] private float lastPanelSeconds = 22f;

  [Header("Animação")]
  [SerializeField] private float panelFadeSeconds = 1.2f;
  [Tooltip("Brilho da página antes de os quadrinhos surgirem (0 = invisível).")]
  [Range(0f, 1f)]
  [SerializeField] private float hiddenPageBrightness = 0.08f;
  [Tooltip("Brilho dos quadrinhos já vistos, para destacar o atual.")]
  [Range(0f, 1f)]
  [SerializeField] private float seenPanelBrightness = 0.65f;
  [SerializeField] private float sceneFadeSeconds = 1.2f;

  [Header("Câmera")]
  [Tooltip("Quanto da tela o quadrinho atual ocupa (1 = encosta nas bordas).")]
  [Range(0.5f, 1f)]
  [SerializeField] private float panelScreenFill = 0.8f;
  [Tooltip("Ampliação máxima da página em relação ao tamanho original da imagem. Acima disso o desenho começa a perder nitidez.")]
  [SerializeField] private float maxPageZoom = 2.2f;
  [Tooltip("Velocidade do zoom e do deslocamento entre quadrinhos.")]
  [SerializeField] private float cameraSpeed = 2.5f;

  private AudioSource source;
  private float clock;
  private float length;
  private float startFade;
  private float dip;
  private float seekTarget = -1f;
  private int shown;
  private float[] reveal;
  private float fade = 1f;
  private bool leaving;
  private GUIStyle hintStyle;
  private Vector2 viewCenter;
  private float viewScale;
  private bool viewReady;

  private float OverviewTime => panels.Length > 0 ? panels[panels.Length - 1].startTime + lastPanelSeconds : 0f;
  private bool Finished => shown >= panels.Length && clock >= OverviewTime;

  public void Start()
  {
    Time.timeScale = 1f;
    Cursor.lockState = CursorLockMode.None;
    Cursor.visible = true;
    reveal = new float[panels.Length];
    length = panels.Length > 0 ? panels[panels.Length - 1].startTime + 30f : 0f;

    if (music != null)
    {
      length = music.length;
      source = gameObject.AddComponent<AudioSource>();
      source.clip = music;
      source.loop = false;
      source.playOnAwake = false;
      source.spatialBlend = 0f;
      source.priority = 0;
      source.volume = 0f;
      source.Play();
    }
  }

  public void Update()
  {
    float dt = Time.unscaledDeltaTime;
    fade = Mathf.MoveTowards(fade, leaving ? 1f : 0f, dt / Mathf.Max(0.01f, sceneFadeSeconds));
    UpdateClock(dt);

    if (source != null)
    {
      startFade = musicFadeInSeconds > 0f ? Mathf.MoveTowards(startFade, 1f, dt / musicFadeInSeconds) : 1f;
      source.volume = musicVolume * startFade * (1f - dip) * (leaving ? 1f - fade : 1f);
    }

    if (leaving)
    {
      if (fade >= 1f) SceneManager.LoadScene(nextScene);
      return;
    }

    UpdateCamera(dt);

    while (shown < panels.Length && clock >= panels[shown].startTime) shown++;
    for (int i = 0; i < shown; i++)
    {
      reveal[i] = Mathf.MoveTowards(reveal[i], 1f, dt / Mathf.Max(0.01f, panelFadeSeconds));
    }

    if (Input.GetKeyDown(KeyCode.Escape) || clock >= length + endHoldSeconds)
    {
      leaving = true;
      return;
    }

    bool advance = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    if (!advance) return;

    if (Finished)
    {
      leaving = true;
    }
    else if (shown > 0 && reveal[shown - 1] < 1f)
    {
      reveal[shown - 1] = 1f;
    }
    else if (seekTarget < 0f)
    {
      seekTarget = shown < panels.Length ? panels[shown].startTime : OverviewTime;
    }
  }

  private void UpdateClock(float dt)
  {
    if (seekTarget >= 0f)
    {
      dip = Mathf.MoveTowards(dip, 1f, dt / Mathf.Max(0.01f, seekDipSeconds));
      if (dip >= 1f || source == null)
      {
        clock = seekTarget;
        if (source != null && seekTarget < length) source.time = seekTarget;
        seekTarget = -1f;
      }
      return;
    }

    dip = Mathf.MoveTowards(dip, 0f, dt / Mathf.Max(0.01f, seekDipSeconds));
    if (source != null && source.isPlaying)
    {
      clock = source.time;
    }
    else
    {
      clock += dt;
    }
  }

  public void OnGUI()
  {
    GUI.color = Color.black;
    GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

    if (!viewReady) UpdateCamera(0f);
    Vector2 pageSize = GetPageSize();
    Rect pageRect = new Rect(Screen.width * 0.5f - viewCenter.x * viewScale, Screen.height * 0.5f - viewCenter.y * viewScale,
      pageSize.x * viewScale, pageSize.y * viewScale);
    if (page != null)
    {
      GUI.color = new Color(hiddenPageBrightness, hiddenPageBrightness, hiddenPageBrightness, 1f);
      GUI.DrawTexture(pageRect, page);
    }

    for (int i = 0; i < shown; i++)
    {
      Panel panel = panels[i];
      if (panel.image == null) continue;

      float t = Mathf.SmoothStep(0f, 1f, reveal[i]);
      Rect area = new Rect(pageRect.x + panel.area.x * pageRect.width, pageRect.y + panel.area.y * pageRect.height,
        panel.area.width * pageRect.width, panel.area.height * pageRect.height);
      float scale = Mathf.Lerp(1.08f, 1f, t);
      Rect drawn = new Rect(area.center.x - area.width * scale * 0.5f, area.center.y - area.height * scale * 0.5f + (1f - t) * Screen.height * 0.02f,
        area.width * scale, area.height * scale);

      float brightness = i == shown - 1 || Finished ? 1f : seenPanelBrightness;
      GUI.color = new Color(brightness, brightness, brightness, t);
      GUI.DrawTexture(drawn, panel.image);
    }

    DrawHint();

    if (fade > 0f)
    {
      GUI.color = new Color(0f, 0f, 0f, fade);
      GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
    }
    GUI.color = Color.white;
  }

  private Vector2 GetPageSize()
  {
    return page != null ? new Vector2(page.width, page.height) : new Vector2(1000f, 1000f);
  }

  private void UpdateCamera(float dt)
  {
    Vector2 pageSize = GetPageSize();
    Rect target = shown == 0 || Finished ? new Rect(0f, 0f, 1f, 1f) : panels[shown - 1].area;
    float fill = shown == 0 || Finished ? 0.94f : panelScreenFill;

    Vector2 targetCenter = Vector2.Scale(target.center, pageSize);
    float targetScale = Mathf.Min(Screen.width * fill / (target.width * pageSize.x), Screen.height * fill / (target.height * pageSize.y));
    targetScale = Mathf.Min(targetScale, maxPageZoom);

    if (!viewReady)
    {
      viewCenter = targetCenter;
      viewScale = targetScale;
      viewReady = true;
      return;
    }

    float t = 1f - Mathf.Exp(-cameraSpeed * dt);
    viewCenter = Vector2.Lerp(viewCenter, targetCenter, t);
    viewScale = Mathf.Exp(Mathf.Lerp(Mathf.Log(viewScale), Mathf.Log(targetScale), t));
  }

  private void DrawHint()
  {
    if (hintStyle == null || hintStyle.fontSize != Mathf.Max(11, Mathf.RoundToInt(Screen.height * 0.018f)))
    {
      hintStyle = new GUIStyle(GUI.skin.label)
      {
        fontSize = Mathf.Max(11, Mathf.RoundToInt(Screen.height * 0.018f)),
        fontStyle = FontStyle.Bold,
        alignment = TextAnchor.LowerRight
      };
      hintStyle.normal.textColor = Color.white;
    }

    string text = Finished ? "Clique ou Espaço para começar" : "Clique ou Espaço para avançar   •   Esc para pular";
    float pulse = Finished ? 0.6f + Mathf.Sin(Time.unscaledTime * 3f) * 0.4f : 0.75f;
    Rect rect = new Rect(0f, 0f, Screen.width - 16f, Screen.height - 8f);

    GUI.color = new Color(0f, 0f, 0f, 0.8f * pulse);
    GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, hintStyle);
    GUI.color = new Color(1f, 1f, 1f, pulse);
    GUI.Label(rect, text, hintStyle);
  }
}
