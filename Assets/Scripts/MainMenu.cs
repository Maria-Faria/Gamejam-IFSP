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

  private GUIStyle titleStyle;
  private GUIStyle textStyle;
  private GUIStyle smallButtonStyle;

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

  private void DrawPanel()
  {
    float width = Mathf.Min(Screen.width * 0.5f, Screen.height * 0.8f);
    float height = Screen.height * 0.6f;
    Rect box = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.32f, width, height);

    GUI.color = new Color(0.55f, 0.35f, 1f, 0.9f);
    GUI.DrawTexture(new Rect(box.x - 3f, box.y - 3f, box.width + 6f, box.height + 6f), Texture2D.whiteTexture);
    GUI.color = new Color(0.08f, 0.06f, 0.16f, 0.94f);
    GUI.DrawTexture(box, Texture2D.whiteTexture);
    GUI.color = Color.white;

    float pad = box.width * 0.07f;
    Rect content = new Rect(box.x + pad, box.y + pad * 0.6f, box.width - pad * 2f, box.height - pad * 1.2f);
    GUILayout.BeginArea(content);

    if (panel == Panel.Multiplayer)
    {
      DrawMultiplayer();
    }
    else
    {
      DrawSettings();
    }

    GUILayout.FlexibleSpace();
    if (GUILayout.Button("Voltar", smallButtonStyle, GUILayout.Height(Screen.height * 0.06f)))
    {
      panel = Panel.None;
    }

    GUILayout.EndArea();
  }

  // Sala cooperativa (até 4 jogadoras na rede local / online por IP), pelo LunaNetworkSyncManager.
  private void DrawMultiplayer()
  {
    LunaNetworkSyncManager net = LunaNetworkSyncManager.EnsureExists();
    float rowHeight = Screen.height * 0.05f;

    GUILayout.Label("MULTIPLAYER (CO-OP ATÉ 4)", titleStyle);
    GUILayout.Space(Screen.height * 0.01f);

    GUILayout.BeginHorizontal();
    GUILayout.Label("Apelido:", textStyle, GUILayout.Width(Screen.height * 0.16f));
    nicknameText = GUILayout.TextField(nicknameText ?? "", 16, GUILayout.Height(rowHeight * 0.8f));
    GUILayout.EndHorizontal();
    GUILayout.BeginHorizontal();
    GUILayout.Label("IP do host:", textStyle, GUILayout.Width(Screen.height * 0.16f));
    hostIpText = GUILayout.TextField(hostIpText ?? "", 40, GUILayout.Height(rowHeight * 0.8f));
    GUILayout.Label("Porta:", textStyle, GUILayout.Width(Screen.height * 0.08f));
    portText = GUILayout.TextField(portText ?? "", 5, GUILayout.Width(Screen.height * 0.1f), GUILayout.Height(rowHeight * 0.8f));
    GUILayout.EndHorizontal();

    int port = int.TryParse(portText, out int parsed) && parsed > 0 && parsed < 65536 ? parsed : 7777;
    CoopSession.playerNickname = string.IsNullOrWhiteSpace(nicknameText) ? "Guardiã" : nicknameText.Trim().Replace("|", "");

    GUILayout.Space(Screen.height * 0.01f);
    bool offline = net.currentRole == LunaNetworkSyncManager.NetworkRole.Offline;
    if (offline)
    {
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Criar sala (Host)", smallButtonStyle, GUILayout.Height(rowHeight)))
      {
        PlayerPrefs.SetString(NicknameKey, CoopSession.playerNickname);
        CoopSession.customDungeonSeed = CoopSession.NewSeed();
        net.StartHost(port);
      }
      if (GUILayout.Button("Entrar na sala", smallButtonStyle, GUILayout.Height(rowHeight)))
      {
        PlayerPrefs.SetString(NicknameKey, CoopSession.playerNickname);
        PlayerPrefs.SetString(HostIpKey, hostIpText.Trim());
        net.StartClient(hostIpText, port, CoopSession.playerNickname);
      }
      GUILayout.EndHorizontal();
    }
    else
    {
      bool host = net.currentRole == LunaNetworkSyncManager.NetworkRole.Host;
      GUILayout.Label(host ? $"Sala aberta — {net.ConnectedPeersCount + 1}/4 jogadoras (semente {CoopSession.customDungeonSeed})"
                           : (net.IsConnected ? $"Conectada como Jogadora {net.MyPlayerId + 1}" : "Conectando..."), textStyle);
      GUILayout.BeginHorizontal();
      GUI.enabled = host;
      if (GUILayout.Button(host ? "INICIAR EXPEDIÇÃO" : "Aguardando o host...", smallButtonStyle, GUILayout.Height(rowHeight)))
      {
        CoopSession.StartExpedition();
      }
      GUI.enabled = true;
      if (GUILayout.Button("Desconectar", smallButtonStyle, GUILayout.Height(rowHeight), GUILayout.Width(Screen.height * 0.2f)))
      {
        net.StopNetwork();
        CoopSession.ResetToSingleplayer();
        CoopSession.multiplayerConnectionLog = "Desconectada.";
      }
      GUILayout.EndHorizontal();
    }

    GUILayout.Space(Screen.height * 0.01f);
    GUILayout.Label(string.IsNullOrEmpty(CoopSession.multiplayerConnectionLog)
      ? "Crie uma sala e passe o seu IP para as amigas (mesma rede ou porta liberada), ou digite o IP do host e entre."
      : CoopSession.multiplayerConnectionLog, textStyle);
  }

  private void DrawSettings()
  {
    GUILayout.Label("CONFIGURAÇÕES", titleStyle);
    GUILayout.Space(Screen.height * 0.015f);

    GUILayout.Label($"Volume geral: {Mathf.RoundToInt(volume * 100f)}%", textStyle);
    float newVolume = GUILayout.HorizontalSlider(volume, 0f, 1f, GUILayout.Height(Screen.height * 0.03f));
    if (!Mathf.Approximately(newVolume, volume))
    {
      volume = newVolume;
      AudioListener.volume = volume;
      PlayerPrefs.SetFloat(VolumeKey, volume);
    }

    GUILayout.Space(Screen.height * 0.015f);

    if (GUILayout.Button(fullscreen ? "Tela cheia: ligada" : "Tela cheia: desligada", smallButtonStyle, GUILayout.Height(Screen.height * 0.06f)))
    {
      fullscreen = !fullscreen;
      Screen.fullScreen = fullscreen;
    }

    GUILayout.Space(Screen.height * 0.015f);
    GUILayout.Label("<b>Controles</b>", textStyle);
    GUILayout.Label("WASD / setas: correr\nEspaço / botão esquerdo do mouse: atirar\n1 a 6 / roda do mouse: trocar de arma\nE: consertar postes e interagir", textStyle);
    GUILayout.Space(Screen.height * 0.02f);
    GUILayout.Label("<b>Créditos</b>", textStyle);
    GUILayout.Label("Projeto UV — Game Jam IFSP", textStyle);
  }

  private void CreateStyles()
  {
    int textSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.024f));
    if (titleStyle != null && textStyle.fontSize == textSize) return;

    titleStyle = new GUIStyle(GUI.skin.label)
    {
      fontSize = Mathf.RoundToInt(textSize * 1.8f),
      fontStyle = FontStyle.Bold,
      alignment = TextAnchor.MiddleCenter
    };
    titleStyle.normal.textColor = new Color(1f, 0.8f, 0.25f);

    textStyle = new GUIStyle(GUI.skin.label)
    {
      fontSize = textSize,
      richText = true,
      wordWrap = true
    };
    textStyle.normal.textColor = new Color(0.92f, 0.88f, 1f);

    smallButtonStyle = new GUIStyle(GUI.skin.button)
    {
      fontSize = textSize,
      fontStyle = FontStyle.Bold
    };
  }

  public void OnDisable()
  {
    PlayerPrefs.Save();
  }
}
