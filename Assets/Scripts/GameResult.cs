using UnityEngine;
using UnityEngine.SceneManagement;

// Tela de fim de partida: vitória (farol todo aceso) ou derrota (vida zerada)
public class GameResult : MonoBehaviour
{
  public enum Outcome { Victory, Defeat }

  private const string MenuScene = "Menu";
  private const float FadeSeconds = 0.8f;

  // Trilhas em Assets/Audios/Resources
  private const string VictoryMusic = "Projeto_UV_Trilha_Vitoria_60s";
  private const string DefeatMusic = "Projeto_UV_Trilha_Derrota_60s";
  private const float MusicVolume = 0.5f;
  private const float MusicFadeSeconds = 1.5f;

  // Imagens em Assets/Screens/Resources (1672 x 941), com os botões já desenhados
  private const float RefWidth = 1672f;
  private const float RefHeight = 941f;
  private static readonly Rect VictoryPlayButton = Rect.MinMaxRect(484f, 803f, 828f, 892f);
  private static readonly Rect VictoryMenuButton = Rect.MinMaxRect(848f, 803f, 1188f, 892f);
  private static readonly Rect DefeatPlayButton = Rect.MinMaxRect(496f, 803f, 850f, 892f);
  private static readonly Rect DefeatMenuButton = Rect.MinMaxRect(864f, 803f, 1208f, 892f);

  private static GameResult current;

  private Outcome outcome;
  private string details;
  private Texture2D screen;
  private float showAt;
  private float fade;
  private bool paused;
  private readonly float[] hover = new float[2];
  private AudioSource music;
  private BackgroundMusic[] levelMusic;

  private GUIStyle titleStyle;
  private GUIStyle textStyle;
  private GUIStyle buttonStyle;
  private float styleScale = -1f;

  public static bool IsShowing => current != null;

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void ResetStatics()
  {
    current = null;
  }

  public static void Show(Outcome outcome, string details, float delay = 0f)
  {
    if (current != null) return;

    current = new GameObject("Resultado_Partida").AddComponent<GameResult>();
    current.outcome = outcome;
    current.details = details;
    current.screen = Resources.Load<Texture2D>(outcome == Outcome.Victory ? "victory" : "defeat");
    current.showAt = Time.unscaledTime + delay;
  }

  public void Update()
  {
    if (Time.unscaledTime < showAt) return;

    if (!paused)
    {
      paused = true;
      Time.timeScale = 0f;
      Cursor.lockState = CursorLockMode.None;
      Cursor.visible = true;
      StartMusic();
    }

    UpdateMusic();
    fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / FadeSeconds);
    if (fade < 1f) return;

    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) PlayAgain();
    else if (Input.GetKeyDown(KeyCode.Escape)) GoToMenu();
  }

  // A música da fase some e a trilha do resultado entra em loop
  private void StartMusic()
  {
    levelMusic = FindObjectsByType<BackgroundMusic>();

    AudioClip clip = Resources.Load<AudioClip>(outcome == Outcome.Victory ? VictoryMusic : DefeatMusic);
    if (clip == null) return;

    music = gameObject.AddComponent<AudioSource>();
    music.clip = clip;
    music.loop = true;
    music.spatialBlend = 0f;
    music.priority = 0;
    music.volume = 0f;
    music.Play();
  }

  private void UpdateMusic()
  {
    float step = Time.unscaledDeltaTime / MusicFadeSeconds;
    foreach (BackgroundMusic level in levelMusic)
    {
      if (level != null) level.FadeMultiplier = Mathf.MoveTowards(level.FadeMultiplier, 0f, step);
    }
    if (music != null) music.volume = Mathf.MoveTowards(music.volume, MusicVolume, step * MusicVolume);
  }

  public void OnGUI()
  {
    if (Time.unscaledTime < showAt) return;

    GUI.depth = -100;
    GUI.color = new Color(0f, 0f, 0f, fade);
    GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
    GUI.color = new Color(1f, 1f, 1f, fade);

    if (screen != null) DrawImageScreen();
    else DrawTextScreen();

    GUI.color = Color.white;
  }

  private void DrawImageScreen()
  {
    float scale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
    Vector2 origin = new Vector2((Screen.width - RefWidth * scale) * 0.5f, (Screen.height - RefHeight * scale) * 0.5f);
    GUI.DrawTexture(new Rect(origin.x, origin.y, RefWidth * scale, RefHeight * scale), screen, ScaleMode.StretchToFill);

    bool victory = outcome == Outcome.Victory;
    if (ImageButton(0, victory ? VictoryPlayButton : DefeatPlayButton, origin, scale)) PlayAgain();
    if (ImageButton(1, victory ? VictoryMenuButton : DefeatMenuButton, origin, scale)) GoToMenu();
  }

  // O botão já está na imagem: ao passar o mouse, redesenha o mesmo pedaço um pouco maior
  private bool ImageButton(int index, Rect source, Vector2 origin, float scale)
  {
    Rect rect = new Rect(origin.x + source.x * scale, origin.y + source.y * scale, source.width * scale, source.height * scale);
    bool hovered = rect.Contains(Event.current.mousePosition);
    if (Event.current.type == EventType.Repaint)
    {
      hover[index] = Mathf.MoveTowards(hover[index], hovered ? 1f : 0f, Time.unscaledDeltaTime * 8f);
    }

    if (hover[index] > 0f)
    {
      float grow = 1f + hover[index] * 0.06f;
      Rect drawn = new Rect(rect.center.x - rect.width * grow * 0.5f, rect.center.y - rect.height * grow * 0.5f, rect.width * grow, rect.height * grow);
      Rect uv = new Rect(source.x / RefWidth, 1f - source.yMax / RefHeight, source.width / RefWidth, source.height / RefHeight);
      GUI.DrawTextureWithTexCoords(drawn, screen, uv);
    }

    return GUI.Button(rect, GUIContent.none, GUIStyle.none) && fade >= 1f;
  }

  // Usada só se as imagens não forem encontradas
  private void DrawTextScreen()
  {
    float scale = Mathf.Max(0.75f, Screen.height / 1080f);
    CreateStyles(scale);

    bool victory = outcome == Outcome.Victory;
    float width = Mathf.Min(Screen.width * 0.8f, 1000f * scale);
    float x = (Screen.width - width) * 0.5f;
    float y = Screen.height * 0.28f;

    titleStyle.normal.textColor = victory ? new Color(1f, 0.82f, 0.3f) : new Color(1f, 0.35f, 0.35f);
    GUI.Label(new Rect(x, y, width, 110f * scale), victory ? "VITÓRIA!" : "DERROTA", titleStyle);
    y += 120f * scale;

    float textHeight = textStyle.CalcHeight(new GUIContent(details), width);
    GUI.Label(new Rect(x, y, width, textHeight), details, textStyle);
    y += textHeight + 50f * scale;

    float buttonWidth = 300f * scale;
    float buttonHeight = 64f * scale;
    float spacing = 30f * scale;
    float left = (Screen.width - buttonWidth * 2f - spacing) * 0.5f;
    bool interactable = fade >= 1f;

    if (GUI.Button(new Rect(left, y, buttonWidth, buttonHeight), victory ? "Continuar" : "Tentar novamente", buttonStyle) && interactable) PlayAgain();
    if (GUI.Button(new Rect(left + buttonWidth + spacing, y, buttonWidth, buttonHeight), "Menu principal", buttonStyle) && interactable) GoToMenu();
  }

  private void PlayAgain()
  {
    Load(SceneManager.GetActiveScene().buildIndex);
  }

  private void GoToMenu()
  {
    Load(SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{MenuScene}.unity"));
  }

  private void Load(int buildIndex)
  {
    Time.timeScale = 1f;
    if (buildIndex < 0) buildIndex = SceneManager.GetActiveScene().buildIndex;
    SceneManager.LoadScene(buildIndex);
  }

  public void OnDestroy()
  {
    if (current == this) current = null;
    if (paused) Time.timeScale = 1f;
  }

  private void CreateStyles(float scale)
  {
    if (titleStyle != null && Mathf.Approximately(styleScale, scale)) return;
    styleScale = scale;

    titleStyle = new GUIStyle(GUI.skin.label)
    {
      fontSize = Mathf.RoundToInt(80f * scale),
      fontStyle = FontStyle.Bold,
      alignment = TextAnchor.MiddleCenter
    };

    textStyle = new GUIStyle(GUI.skin.label)
    {
      fontSize = Mathf.RoundToInt(28f * scale),
      alignment = TextAnchor.UpperCenter,
      wordWrap = true
    };
    textStyle.normal.textColor = new Color(0.92f, 0.94f, 1f);

    buttonStyle = new GUIStyle(GUI.skin.button)
    {
      fontSize = Mathf.RoundToInt(26f * scale),
      fontStyle = FontStyle.Bold
    };
  }
}
