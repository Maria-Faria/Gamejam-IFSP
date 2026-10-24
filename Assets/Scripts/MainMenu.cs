using GuardiasDaLuz;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
  private const string VolumeKey = "volume_geral";
  private const string NicknameKey = "coop_apelido";
  private const string HostIpKey = "coop_ip_host";

  [Tooltip("Nome da cena carregada ao clicar em Jogar (precisa estar na lista de cenas do build).")]
  [SerializeField] private string gameScene = "Intro";

  [Header("Imagens")]
  [SerializeField] private Texture2D background;
  [SerializeField] private Texture2D playButton;
  [SerializeField] private Texture2D multiplayerButton;
  [SerializeField] private Texture2D settingsButton;
  [SerializeField] private Texture2D quitButton;

  [Header("Layout (proporções da tela)")]
  [Tooltip("Largura dos botões em relação à largura da tela.")]
  [SerializeField] private float buttonWidth = 0.24f;
  [Tooltip("Altura onde começa a coluna de botões (0 = topo, 1 = base).")]
  [SerializeField] private float buttonsTop = 0.45f;
  [SerializeField] private float buttonSpacing = 0.012f;
  [SerializeField] private float fadeSeconds = 0.8f;

  [Header("Janelas (Assets/Modals)")]
  [SerializeField] private Texture2D modalWindow;
  [SerializeField] private Texture2D modalCloseButton;
  [SerializeField] private Texture2D modalPrimaryButton;
  [SerializeField] private Texture2D modalSecondaryButton;
  [SerializeField] private Texture2D modalFieldPlate;
  [SerializeField] private Texture2D modalSliderKnob;

  [Header("Escolha de personagem")]
  [SerializeField] private CharacterSelect characterSelect = new CharacterSelect();

  private enum Panel { None, Multiplayer, Settings, CharacterSelect }

  private Texture2D[] buttons;
  private float[] hover;
  private Rect[] buttonRects;
  private Vector3 lastMousePosition;
  private int selected;
  private Panel panel;
  private BackgroundMusic music;
  private float fade = 1f;
  private bool starting;
  private float volume;
  private bool fullscreen;

  private string nicknameText;
  private string hostIpText;
  private string portText = "7777";

  // Medidas tiradas de Modals/01_tutorial_window.png (1448 x 1086), as mesmas da janela de informações da escolha de personagem
  private const float ModalWidth = 1448f;
  private const float ModalHeight = 1086f;

  private GUIStyle titleStyle;
  private GUIStyle textStyle;
  private GUIStyle labelStyle;
  private GUIStyle buttonTextStyle;
  private GUIStyle fieldStyle;
  private float modalScale;
  private Vector2 modalOrigin;
  private readonly float[] modalHover = new float[8];
  private bool draggingSlider;

  public void Start()
  {
    Time.timeScale = 1f;
    Cursor.lockState = CursorLockMode.None;
    Cursor.visible = true;

    buttons = new[] { playButton, multiplayerButton, settingsButton, quitButton };
    hover = new float[buttons.Length];
    buttonRects = new Rect[buttons.Length];
    lastMousePosition = Input.mousePosition;

    volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
    fullscreen = Screen.fullScreen;
    AudioListener.volume = volume;

    music = GetComponent<BackgroundMusic>();

    nicknameText = PlayerPrefs.GetString(NicknameKey, "Guardiã");
    hostIpText = PlayerPrefs.GetString(HostIpKey, "127.0.0.1");
  }

  public void Update()
  {
    float target = starting ? 1f : 0f;
    fade = Mathf.MoveTowards(fade, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));

    if (music != null && starting)
    {
      music.FadeMultiplier = 1f - fade;
    }

    if (starting)
    {
      if (fade >= 1f) SceneManager.LoadScene(gameScene);
      return;
    }

    if (panel == Panel.CharacterSelect)
    {
      if (characterSelect.HandleInput(out bool back)) starting = true;
      else if (back) panel = Panel.None;
      return;
    }

    if (panel != Panel.None)
    {
      if (Input.GetKeyDown(KeyCode.Escape)) panel = Panel.None;
      return;
    }

    if (Input.mousePosition != lastMousePosition)
    {
      lastMousePosition = Input.mousePosition;
      Vector2 guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
      for (int i = 0; i < buttonRects.Length; i++)
      {
        if (buttonRects[i].Contains(guiMouse)) selected = i;
      }
    }

    if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) selected = (selected + 1) % buttons.Length;
    if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) selected = (selected + buttons.Length - 1) % buttons.Length;
    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) Activate(selected);
  }

  private void Activate(int index)
  {
    switch (index)
    {
      case 0:
        // jogar sozinha: encerra qualquer sala cooperativa aberta, depois escolhe a personagem
        if (LunaNetworkSyncManager.Instance != null) LunaNetworkSyncManager.Instance.StopNetwork();
        CoopSession.ResetToSingleplayer();
        characterSelect.Open();
        panel = Panel.CharacterSelect;
        break;
      case 1:
        panel = Panel.Multiplayer;
        break;
      case 2:
        panel = Panel.Settings;
        break;
      default:
        Quit();
        break;
    }
  }

  private void Quit()
  {
#if UNITY_EDITOR
    UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
  }

  public void OnGUI()
  {
    CreateStyles();

    if (background != null)
    {
      GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), background, ScaleMode.ScaleAndCrop);
    }

    if (panel == Panel.None)
    {
      DrawButtons();
    }
    else if (panel == Panel.CharacterSelect)
    {
      GUI.color = new Color(0f, 0f, 0f, 0.55f);
      GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
      GUI.color = Color.white;
      if (characterSelect.Draw(!starting)) starting = true;
    }
    else
    {
      DrawPanel();
    }

    if (fade > 0f)
    {
      GUI.color = new Color(0f, 0f, 0f, fade);
      GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
      GUI.color = Color.white;
    }
  }

  private void DrawButtons()
  {
    float width = Mathf.Min(Screen.width * buttonWidth, Screen.height * 0.42f);
    float y = Screen.height * buttonsTop;

    for (int i = 0; i < buttons.Length; i++)
    {
      Texture2D texture = buttons[i];
      if (texture == null) continue;

      float height = width * texture.height / texture.width;
      Rect rect = new Rect((Screen.width - width) * 0.5f, y, width, height);
      y += height + Screen.height * buttonSpacing;

      buttonRects[i] = rect;

      if (Event.current.type == EventType.Repaint)
      {
        hover[i] = Mathf.MoveTowards(hover[i], selected == i ? 1f : 0f, Time.unscaledDeltaTime * 8f);
      }

      float pulse = i == 0 ? Mathf.Sin(Time.unscaledTime * 3f) * 0.012f : 0f;
      float scale = 1f + hover[i] * 0.06f + pulse;
      Rect drawn = new Rect(rect.center.x - rect.width * scale * 0.5f, rect.center.y - rect.height * scale * 0.5f, rect.width * scale, rect.height * scale);

      GUI.color = Color.Lerp(new Color(0.85f, 0.85f, 0.85f), Color.white, hover[i]);
      GUI.DrawTexture(drawn, texture, ScaleMode.ScaleToFit);
      GUI.color = Color.white;

      if (!starting && GUI.Button(rect, GUIContent.none, GUIStyle.none))
      {
        selected = i;
        Activate(i);
      }
    }
  }

  // Janela no padrão de Assets/Modals: título na faixa de cima, conteúdo no meio, "Voltar" na faixa de baixo
  private void DrawPanel()
  {
    GUI.color = new Color(0f, 0f, 0f, 0.6f);
    GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
    GUI.color = Color.white;

    modalScale = Mathf.Min(Screen.width * 0.72f / ModalWidth, Screen.height * 0.88f / ModalHeight);
    modalOrigin = new Vector2((Screen.width - ModalWidth * modalScale) * 0.5f, (Screen.height - ModalHeight * modalScale) * 0.5f);
    if (modalWindow != null) GUI.DrawTexture(Box(0f, 0f, ModalWidth, ModalHeight), modalWindow, ScaleMode.StretchToFill);

    Text(Box(180f, 100f, 1090f, 80f), panel == Panel.Multiplayer ? "MULTIPLAYER (CO-OP ATÉ 4)" : "CONFIGURAÇÕES", titleStyle, 54f);

    if (panel == Panel.Multiplayer)
    {
      DrawMultiplayer();
    }
    else
    {
      DrawSettings();
    }

    if (ModalButton(0, modalCloseButton, Center(1372f, 74f, 150f, 110f), null, true)) panel = Panel.None;
    if (ModalButton(1, modalPrimaryButton, Center(ModalWidth * 0.5f, 942f, 400f, 142f), "VOLTAR", true)) panel = Panel.None;
  }

  // Sala cooperativa (até 4 jogadoras na rede local / online por IP), pelo LunaNetworkSyncManager.
  private void DrawMultiplayer()
  {
    LunaNetworkSyncManager net = LunaNetworkSyncManager.EnsureExists();

    Text(Box(110f, 235f, 220f, 90f), "Apelido:", labelStyle, 32f);
    nicknameText = ModalField(Box(340f, 235f, 990f, 90f), nicknameText, 16);
    Text(Box(110f, 340f, 220f, 90f), "IP do host:", labelStyle, 32f);
    hostIpText = ModalField(Box(340f, 340f, 600f, 90f), hostIpText, 40);
    Text(Box(965f, 340f, 125f, 90f), "Porta:", labelStyle, 32f);
    portText = ModalField(Box(1100f, 340f, 230f, 90f), portText, 5);

    int port = int.TryParse(portText, out int parsed) && parsed > 0 && parsed < 65536 ? parsed : 7777;
    CoopSession.playerNickname = string.IsNullOrWhiteSpace(nicknameText) ? "Guardiã" : nicknameText.Trim().Replace("|", "");

    Rect left = Center(430f, 530f, 560f, 200f);
    Rect right = Center(1020f, 530f, 560f, 200f);
    float logTop = 630f;
    bool offline = net.currentRole == LunaNetworkSyncManager.NetworkRole.Offline;
    if (offline)
    {
      if (ModalButton(2, modalPrimaryButton, left, "CRIAR SALA (HOST)", true))
      {
        PlayerPrefs.SetString(NicknameKey, CoopSession.playerNickname);
        CoopSession.customDungeonSeed = CoopSession.NewSeed();
        net.StartHost(port);
      }
      if (ModalButton(3, modalSecondaryButton, right, "ENTRAR NA SALA", true))
      {
        PlayerPrefs.SetString(NicknameKey, CoopSession.playerNickname);
        PlayerPrefs.SetString(HostIpKey, hostIpText.Trim());
        net.StartClient(hostIpText, port, CoopSession.playerNickname);
      }
    }
    else
    {
      bool host = net.currentRole == LunaNetworkSyncManager.NetworkRole.Host;
      if (ModalButton(2, modalPrimaryButton, left, host ? "INICIAR EXPEDIÇÃO" : "AGUARDANDO O HOST...", host))
      {
        CoopSession.StartExpedition();
      }
      if (ModalButton(3, modalSecondaryButton, right, "DESCONECTAR", true))
      {
        net.StopNetwork();
        CoopSession.ResetToSingleplayer();
        CoopSession.multiplayerConnectionLog = "Desconectada.";
      }
      Text(Box(110f, 625f, 1230f, 60f), host ? $"Sala aberta — {net.ConnectedPeersCount + 1}/4 jogadoras (semente {CoopSession.customDungeonSeed})"
                                             : (net.IsConnected ? $"Conectada como Jogadora {net.MyPlayerId + 1}" : "Conectando..."), labelStyle, 32f);
      logTop = 690f;
    }

    Text(Box(110f, logTop, 1230f, 850f - logTop), string.IsNullOrEmpty(CoopSession.multiplayerConnectionLog)
      ? "Crie uma sala e passe o seu IP para as amigas (mesma rede ou porta liberada), ou digite o IP do host e entre."
      : CoopSession.multiplayerConnectionLog, textStyle, 32f);
  }

  private void DrawSettings()
  {
    Text(Box(110f, 240f, 560f, 60f), $"Volume geral: {Mathf.RoundToInt(volume * 100f)}%", labelStyle, 32f);
    float newVolume = ModalSlider(Box(110f, 320f, 560f, 70f), volume);
    if (!Mathf.Approximately(newVolume, volume))
    {
      volume = newVolume;
      AudioListener.volume = volume;
      PlayerPrefs.SetFloat(VolumeKey, volume);
    }

    if (ModalButton(4, modalSecondaryButton, Center(390f, 520f, 620f, 221f), fullscreen ? "TELA CHEIA: LIGADA" : "TELA CHEIA: DESLIGADA", true))
    {
      fullscreen = !fullscreen;
      Screen.fullScreen = fullscreen;
    }

    Text(Box(760f, 230f, 580f, 310f), "<b><color=#FFCC59>Controles</color></b>\nWASD / setas: correr\nEspaço / botão esquerdo do mouse: atirar\n1 a 7 / roda do mouse: trocar de arma\nE: consertar postes e pegar itens", textStyle, 32f);
    Text(Box(760f, 570f, 580f, 110f), "<b><color=#FFCC59>Créditos</color></b>\nProjeto UV — Game Jam IFSP", textStyle, 32f);
  }

  // Desenha o texto com a maior fonte (até o tamanho de referência) que cabe na caixa
  private void Text(Rect rect, string text, GUIStyle style, float referenceSize)
  {
    HudScale.FitFont(style, text, rect, Mathf.RoundToInt(referenceSize * modalScale));
    GUI.Label(rect, text, style);
  }

  private Rect Box(float x, float y, float w, float h)
  {
    return new Rect(modalOrigin.x + x * modalScale, modalOrigin.y + y * modalScale, w * modalScale, h * modalScale);
  }

  private Rect Center(float cx, float cy, float w, float h)
  {
    return Box(cx - w * 0.5f, cy - h * 0.5f, w, h);
  }

  // Botão com a arte das janelas: cresce e clareia com o mouse em cima, como os da escolha de personagem
  private bool ModalButton(int index, Texture2D texture, Rect rect, string label, bool enabled)
  {
    bool hovered = enabled && rect.Contains(Event.current.mousePosition);
    if (Event.current.type == EventType.Repaint)
    {
      modalHover[index] = Mathf.MoveTowards(modalHover[index], hovered ? 1f : 0f, Time.unscaledDeltaTime * 8f);
    }

    float grow = 1f + modalHover[index] * 0.08f;
    Rect drawn = new Rect(rect.center.x - rect.width * grow * 0.5f, rect.center.y - rect.height * grow * 0.5f, rect.width * grow, rect.height * grow);
    Color tint = enabled ? Color.Lerp(new Color(0.85f, 0.85f, 0.85f), Color.white, modalHover[index]) : new Color(0.5f, 0.5f, 0.55f, 0.8f);
    GUI.color = tint;
    if (texture != null) GUI.DrawTexture(drawn, texture, ScaleMode.ScaleToFit);
    GUI.color = Color.white;
    if (!string.IsNullOrEmpty(label))
    {
      GUI.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.6f);
      Text(InsideButtonArt(texture, drawn), label, buttonTextStyle, 28f);
      GUI.color = Color.white;
    }

    return enabled && GUI.Button(rect, GUIContent.none, GUIStyle.none);
  }

  // Parte de dentro da pílula de 03/04_button (a arte tem brilho e borda em volta), onde o texto cabe sem encostar
  public static Rect InsideButtonArt(Texture2D texture, Rect area)
  {
    float aspect = texture != null ? (float)texture.width / texture.height : area.width / Mathf.Max(area.height, 1f);
    float width = Mathf.Min(area.width, area.height * aspect);
    float height = width / aspect;
    return new Rect(area.center.x - width * 0.38f, area.center.y - height * 0.24f, width * 0.76f, height * 0.48f);
  }

  private string ModalField(Rect rect, string text, int maxLength)
  {
    if (modalFieldPlate != null) GUI.DrawTexture(rect, modalFieldPlate, ScaleMode.StretchToFill);
    // O TextField do Unity só alinha bem à esquerda, então a caixa de edição tem a largura do texto
    // e fica centralizada no miolo da placa (longe das pontas em ângulo)
    text = text ?? "";
    float maxWidth = rect.width * 0.8f;
    fieldStyle.fontSize = Mathf.RoundToInt(Mathf.Min(32f * modalScale, rect.height * 0.42f));
    Vector2 size = fieldStyle.CalcSize(new GUIContent(text));
    float lineHeight = fieldStyle.CalcSize(new GUIContent("Ág")).y;
    float width = Mathf.Min(maxWidth, size.x + fieldStyle.fontSize * 0.6f);
    Rect inner = new Rect(rect.center.x - width * 0.5f, rect.center.y - lineHeight * 0.5f, width, lineHeight);

    // clicar em qualquer parte da placa também começa a digitar
    string controlName = $"campo_{Mathf.RoundToInt(rect.x)}_{Mathf.RoundToInt(rect.y)}";
    Event e = Event.current;
    if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition) && !inner.Contains(e.mousePosition))
    {
      GUI.FocusControl(controlName);
      e.Use();
    }
    GUI.SetNextControlName(controlName);
    return GUI.TextField(inner, text, maxLength, fieldStyle);
  }

  // Trilho fino em neon com o anel de Modals/10_progress como pino
  private float ModalSlider(Rect rect, float value)
  {
    float knob = rect.height;
    float left = rect.x + knob * 0.5f;
    float width = rect.width - knob;
    Rect track = new Rect(left, rect.center.y - rect.height * 0.09f, width, rect.height * 0.18f);

    GUI.color = new Color(0.05f, 0.12f, 0.35f, 0.95f);
    GUI.DrawTexture(track, Texture2D.whiteTexture);
    GUI.color = new Color(0.2f, 0.85f, 1f);
    GUI.DrawTexture(new Rect(track.x, track.y, track.width * value, track.height), Texture2D.whiteTexture);
    GUI.color = Color.white;

    Event e = Event.current;
    if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition)) draggingSlider = true;
    if (e.rawType == EventType.MouseUp) draggingSlider = false;
    if (draggingSlider && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
    {
      value = Mathf.Clamp01((e.mousePosition.x - left) / Mathf.Max(width, 1f));
      e.Use();
    }

    Rect thumb = new Rect(left + width * value - knob * 0.5f, rect.y, knob, knob);
    if (modalSliderKnob != null) GUI.DrawTexture(thumb, modalSliderKnob, ScaleMode.ScaleToFit);
    return value;
  }

  private void CreateStyles()
  {
    if (titleStyle != null) return;

    titleStyle = new GUIStyle(GUI.skin.label)
    {
      fontStyle = FontStyle.Bold,
      alignment = TextAnchor.MiddleCenter
    };
    titleStyle.normal.textColor = new Color(0.85f, 0.95f, 1f);

    textStyle = new GUIStyle(GUI.skin.label)
    {
      richText = true,
      wordWrap = true,
      alignment = TextAnchor.MiddleLeft
    };
    textStyle.normal.textColor = new Color(0.92f, 0.94f, 1f);

    labelStyle = new GUIStyle(textStyle) { wordWrap = false };

    buttonTextStyle = new GUIStyle(GUI.skin.label)
    {
      fontStyle = FontStyle.Bold,
      alignment = TextAnchor.MiddleCenter
    };
    buttonTextStyle.normal.textColor = new Color(0.92f, 0.96f, 1f);

    // sem fundo: a placa de Modals/09_title_plate desenhada por baixo faz o papel da caixa
    fieldStyle = new GUIStyle(GUI.skin.label)
    {
      alignment = TextAnchor.MiddleLeft,
      clipping = TextClipping.Clip,
      wordWrap = false,
      padding = new RectOffset(0, 0, 0, 0),
      margin = new RectOffset(0, 0, 0, 0)
    };
    fieldStyle.normal.textColor = fieldStyle.hover.textColor = fieldStyle.focused.textColor = Color.white;
  }

#if UNITY_EDITOR
  // Preenche as janelas vazias com a arte de Assets/Modals
  public void OnValidate()
  {
    AssignIfEmpty(ref modalWindow, "01_tutorial_window");
    AssignIfEmpty(ref modalCloseButton, "07_close");
    AssignIfEmpty(ref modalPrimaryButton, "03_primary_button");
    AssignIfEmpty(ref modalSecondaryButton, "04_secondary_button");
    AssignIfEmpty(ref modalFieldPlate, "09_title_plate");
    AssignIfEmpty(ref modalSliderKnob, "10_progress");
  }

  private static void AssignIfEmpty(ref Texture2D slot, string name)
  {
    if (slot == null) slot = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Modals/{name}.png");
  }
#endif

  public void OnDisable()
  {
    PlayerPrefs.Save();
  }
}
