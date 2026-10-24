using UnityEngine;

[System.Serializable]
public class CharacterSelect
{
  public const string SelectedCharacterKey = "personagem_escolhido";

  private struct Scientist
  {
    public string Name;
    public string Years;
    public string Field;
    public string Bio;

    public Scientist(string name, string years, string field, string bio)
    {
      Name = name;
      Years = years;
      Field = field;
      Bio = bio;
    }
  }

  // Mesma ordem do carrossel e da lista de retratos no Inspector
  private static readonly Scientist[] Scientists =
  {
    new Scientist("Enedina Alves Marques", "1913 – 1981", "Engenharia civil",
      "Primeira mulher negra a se formar em engenharia no Brasil, pela Universidade do Paraná, em 1945. Trabalhou no Plano Hidrelétrico do Paraná e na construção da Usina Capivari-Cachoeira, levando energia elétrica a muitas cidades."),
    new Scientist("Hertha Ayrton", "1854 – 1923", "Física e engenharia elétrica",
      "Estudou o arco elétrico usado nas lâmpadas de arco da iluminação pública e descobriu por que ele chiava e oscilava. Foi a primeira mulher eleita para a Institution of Electrical Engineers e recebeu a Medalha Hughes da Royal Society."),
    new Scientist("Mária Telkes", "1900 – 1995", "Energia solar",
      "Cientista húngaro-americana conhecida como a \"Rainha do Sol\". Projetou o aquecimento solar da Dover Sun House (1948), uma das primeiras casas aquecidas pelo Sol, e criou destiladores solares de água."),
    new Scientist("Chien-Shiung Wu", "1912 – 1997", "Física nuclear",
      "Física sino-americana chamada de \"Primeira-Dama da Física\". Em 1956, seu experimento mostrou que a natureza nem sempre é simétrica (a violação da paridade), um marco da física de partículas."),
    new Scientist("Patricia Bath", "1942 – 2019", "Oftalmologia",
      "Médica norte-americana que inventou o Laserphaco Probe, aparelho que usa laser para tratar a catarata. Em 1988, tornou-se a primeira médica negra a patentear uma invenção médica nos Estados Unidos."),
    new Scientist("Annie Easley", "1933 – 2011", "Matemática e computação",
      "Cientista da computação e matemática da NASA por 34 anos. Desenvolveu programas para o foguete Centaur e estudou fontes de energia, como baterias de veículos elétricos e energia solar e eólica."),
    new Scientist("Bibha Chowdhuri", "1913 – 1991", "Física de partículas",
      "Física indiana que estudou partículas vindas dos raios cósmicos. No fim dos anos 1930, com Debendra Mohan Bose, registrou em chapas fotográficas evidências de uma partícula subatômica, o méson.")
  };

  // Medidas tiradas de ChooseCharacter/example.png (1672 x 941)
  private const float RefWidth = 1672f;
  private const float RefHeight = 941f;
  private const float CenterX = 836f;
  private static readonly float[] SlotOffsets = { 0f, 262f, 472f, 668f, 820f };
  private static readonly float[] SlotHeights = { 600f, 410f, 400f, 395f, 380f };
  private static readonly float[] SlotFeet = { 705f, 668f, 668f, 668f, 668f };
  private static readonly float[] SlotBrightness = { 1f, 0.8f, 0.72f, 0.65f, 0.6f };

  [Tooltip("Retratos na ordem: Enedina, Hertha, Mária Telkes, Chien-Shiung Wu, Patricia Bath, Annie Easley, Bibha Chowdhuri.")]
  [SerializeField] private Texture2D[] portraits;
  [SerializeField] private Texture2D leftArrow;
  [SerializeField] private Texture2D rightArrow;
  [SerializeField] private Texture2D infoButton;
  [SerializeField] private Texture2D namePlate;
  [SerializeField] private Texture2D pedestal;
  [SerializeField] private Texture2D platform;
  [SerializeField] private float slideSpeed = 8f;

  [Header("Janela de informações")]
  [SerializeField] private Texture2D infoWindow;
  [SerializeField] private Texture2D closeButton;
  [SerializeField] private Texture2D previousButton;
  [SerializeField] private Texture2D nextButton;
  [SerializeField] private Texture2D primaryButton;

  [Header("Efeitos sonoros")]
  [SerializeField] private AudioClip navigateSound;
  [SerializeField] private AudioClip confirmSound;
  [SerializeField] private AudioClip infoSound;
  [SerializeField, Range(0f, 1f)] private float soundVolume = 0.8f;

  private const float InfoWidth = 1448f;
  private const float InfoHeight = 1086f;

  private int selected;
  private float position;
  private bool showInfo;
  private float[] buttonHover = new float[7];

  private GUIStyle nameStyle;
  private GUIStyle hintStyle;
  private GUIStyle titleStyle;
  private GUIStyle subtitleStyle;
  private GUIStyle textStyle;
  private GUIStyle buttonTextStyle;

  public int Count => Scientists.Length;
  public string SelectedName => Scientists[selected].Name;

  public void Open()
  {
    selected = Mathf.Clamp(PlayerPrefs.GetInt(SelectedCharacterKey, 0), 0, Count - 1);
    position = selected;
    showInfo = false;
  }

  // Devolve true quando a jogadora confirma a escolha
  public bool HandleInput(out bool back)
  {
    back = false;
    position = Mathf.Lerp(position, position + Wrap(selected - position), 1f - Mathf.Exp(-slideSpeed * Time.unscaledDeltaTime));

    if (showInfo)
    {
      if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.I)) showInfo = false;
      if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(-1);
      if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Step(1);
      return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ? Confirm() : false;
    }

    if (Input.GetKeyDown(KeyCode.Escape)) back = true;
    if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(-1);
    if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Step(1);
    if (Input.GetKeyDown(KeyCode.I)) OpenInfo();
    return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)
      ? Confirm()
      : false;
  }

  // Desenha a tela; devolve true quando a jogadora clica na personagem do centro
  public bool Draw(bool interactable)
  {
    CreateStyles();

    float scale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
    Vector2 origin = new Vector2((Screen.width - RefWidth * scale) * 0.5f, (Screen.height - RefHeight * scale) * 0.5f);
    Rect R(float cx, float cy, float w, float h) => new Rect(origin.x + (cx - w * 0.5f) * scale, origin.y + (cy - h * 0.5f) * scale, w * scale, h * scale);

    DrawFit(platform, R(CenterX, 690f, 1450f, 0f), true);
    DrawFit(pedestal, R(CenterX, 718f, 700f, 0f), true);

    bool confirmed = false;
    foreach (int index in DrawOrder())
    {
      float d = Wrap(index - position);
      float t = Mathf.Abs(d);
      float x = CenterX + Mathf.Sign(d) * Sample(SlotOffsets, t);
      float height = Sample(SlotHeights, t);
      float feet = Sample(SlotFeet, t);
      float brightness = Sample(SlotBrightness, t);
      float alpha = Mathf.Clamp01((3.5f - t) * 2f);

      Texture2D portrait = portraits != null && index < portraits.Length ? portraits[index] : null;
      if (portrait == null) continue;

      float width = height * portrait.width / portrait.height;
      Rect rect = R(x, feet - height * 0.5f, width, height);
      GUI.color = new Color(brightness, brightness, brightness, alpha);
      GUI.DrawTexture(rect, portrait, ScaleMode.ScaleToFit);
      GUI.color = Color.white;

      Rect clickArea = new Rect(rect.x + rect.width * 0.15f, rect.y, rect.width * 0.7f, rect.height * 0.88f);
      if (interactable && !showInfo && index == selected && t < 0.05f && GUI.Button(clickArea, GUIContent.none, GUIStyle.none))
      {
        confirmed = Confirm();
      }
    }

    Rect plate = R(CenterX, 855f, 640f, 178f);
    DrawFit(namePlate, plate, false);
    Rect plateText = new Rect(plate.x + plate.width * 0.15f, plate.y + plate.height * 0.28f, plate.width * 0.7f, plate.height * 0.44f);
    HudScale.FitFont(nameStyle, SelectedName.ToUpperInvariant(), plateText, Mathf.RoundToInt(32f * scale));
    GUI.Label(plateText, SelectedName.ToUpperInvariant(), nameStyle);

    Rect left = R(90f, 545f, 175f, 175f);
    Rect right = R(1580f, 545f, 175f, 175f);
    Rect info = R(CenterX, 745f, 115f, 115f);
    if (interactable && !showInfo)
    {
      if (IconButton(0, leftArrow, left)) Step(-1);
      if (IconButton(1, rightArrow, right)) Step(1);
      if (IconButton(2, infoButton, info)) OpenInfo();
    }
    else
    {
      DrawFit(leftArrow, left, false);
      DrawFit(rightArrow, right, false);
      DrawFit(infoButton, info, false);
    }

    const string hint = "◄ ► trocar  •  i  informações  •  Enter ou clique na cientista para jogar  •  Esc voltar";
    Rect hintRect = R(CenterX, 45f, 1400f, 56f);
    HudScale.FitFont(hintStyle, hint, hintRect, Mathf.RoundToInt(26f * scale));
    GUI.Label(hintRect, hint, hintStyle);

    if (showInfo && DrawInfo() && interactable)
    {
      confirmed = true;
    }

    return confirmed;
  }

  // Devolve true quando a jogadora clica em "Escolher"
  private bool DrawInfo()
  {
    Scientist scientist = Scientists[selected];

    GUI.color = new Color(0f, 0f, 0f, 0.6f);
    GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
    GUI.color = Color.white;

    // Medidas tiradas de Modals/01_tutorial_window.png (1448 x 1086)
    float scale = Mathf.Min(Screen.width * 0.72f / InfoWidth, Screen.height * 0.88f / InfoHeight);
    Vector2 origin = new Vector2((Screen.width - InfoWidth * scale) * 0.5f, (Screen.height - InfoHeight * scale) * 0.5f);
    Rect R(float cx, float cy, float w, float h) => new Rect(origin.x + (cx - w * 0.5f) * scale, origin.y + (cy - h * 0.5f) * scale, w * scale, h * scale);
    Rect Box(float x, float y, float w, float h) => new Rect(origin.x + x * scale, origin.y + y * scale, w * scale, h * scale);

    if (infoWindow != null) GUI.DrawTexture(Box(0f, 0f, InfoWidth, InfoHeight), infoWindow, ScaleMode.StretchToFill);

    Rect titleRect = Box(180f, 100f, 1090f, 80f);
    HudScale.FitFont(titleStyle, scientist.Name.ToUpperInvariant(), titleRect, Mathf.RoundToInt(54f * scale));
    GUI.Label(titleRect, scientist.Name.ToUpperInvariant(), titleStyle);

    Texture2D portrait = portraits != null && selected < portraits.Length ? portraits[selected] : null;
    if (portrait != null) GUI.DrawTexture(Box(90f, 250f, 400f, 590f), portrait, ScaleMode.ScaleToFit);

    string subtitle = $"{scientist.Field}  •  {scientist.Years}";
    Rect subtitleRect = Box(530f, 260f, 820f, 100f);
    Rect bioRect = Box(530f, 380f, 820f, 460f);
    HudScale.FitFont(subtitleStyle, subtitle, subtitleRect, Mathf.RoundToInt(36f * scale));
    HudScale.FitFont(textStyle, scientist.Bio, bioRect, Mathf.RoundToInt(34f * scale));
    GUI.Label(subtitleRect, subtitle, subtitleStyle);
    GUI.Label(bioRect, scientist.Bio, textStyle);

    if (IconButton(3, closeButton, R(1372f, 74f, 150f, 110f))) showInfo = false;
    if (IconButton(4, previousButton, R(190f, 942f, 150f, 114f))) Step(-1);
    if (IconButton(5, nextButton, R(1258f, 942f, 150f, 114f))) Step(1);

    Rect choose = R(InfoWidth * 0.5f, 942f, 400f, 142f);
    bool confirmed = IconButton(6, primaryButton, choose);
    Rect chooseText = MainMenu.InsideButtonArt(primaryButton, choose);
    HudScale.FitFont(buttonTextStyle, "ESCOLHER", chooseText, Mathf.RoundToInt(30f * scale));
    GUI.Label(chooseText, "ESCOLHER", buttonTextStyle);

    return confirmed && Confirm();
  }

  private bool IconButton(int index, Texture2D texture, Rect rect)
  {
    bool hovered = rect.Contains(Event.current.mousePosition);
    if (Event.current.type == EventType.Repaint)
    {
      buttonHover[index] = Mathf.MoveTowards(buttonHover[index], hovered ? 1f : 0f, Time.unscaledDeltaTime * 8f);
    }

    float grow = 1f + buttonHover[index] * 0.08f;
    Rect drawn = new Rect(rect.center.x - rect.width * grow * 0.5f, rect.center.y - rect.height * grow * 0.5f, rect.width * grow, rect.height * grow);
    GUI.color = Color.Lerp(new Color(0.85f, 0.85f, 0.85f), Color.white, buttonHover[index]);
    if (texture != null) GUI.DrawTexture(drawn, texture, ScaleMode.ScaleToFit);
    GUI.color = Color.white;

    return GUI.Button(rect, GUIContent.none, GUIStyle.none);
  }

  // Altura 0 = calcula pela proporção da imagem
  private static void DrawFit(Texture2D texture, Rect rect, bool keepAspect)
  {
    if (texture == null) return;
    if (keepAspect || rect.height <= 0f)
    {
      float height = rect.width * texture.height / texture.width;
      rect = new Rect(rect.x, rect.center.y - height * 0.5f, rect.width, height);
    }
    GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
  }

  private void Step(int direction)
  {
    selected = (selected + direction + Count) % Count;
    PlaySound(navigateSound);
  }

  private void OpenInfo()
  {
    showInfo = true;
    PlaySound(infoSound);
  }

  private bool Confirm()
  {
    PlayerPrefs.SetInt(SelectedCharacterKey, selected);
    PlayerPrefs.Save();
    PlaySound(confirmSound);
    return true;
  }

  // Cada som tem o próprio objeto, que sobrevive à troca de cena para o som de confirmação não ser cortado
  private void PlaySound(AudioClip clip)
  {
    if (clip == null) return;

    GameObject sound = new GameObject($"SFX_{clip.name}");
    Object.DontDestroyOnLoad(sound);
    AudioSource source = sound.AddComponent<AudioSource>();
    source.spatialBlend = 0f;
    source.PlayOneShot(clip, soundVolume);
    Object.Destroy(sound, clip.length + 0.1f);
  }

  // Desenha de trás para a frente: as mais distantes do centro primeiro
  private int[] DrawOrder()
  {
    int[] order = new int[Count];
    for (int i = 0; i < Count; i++) order[i] = i;
    System.Array.Sort(order, (a, b) => Mathf.Abs(Wrap(b - position)).CompareTo(Mathf.Abs(Wrap(a - position))));
    return order;
  }

  private float Wrap(float delta)
  {
    float half = Count * 0.5f;
    return Mathf.Repeat(delta + half, Count) - half;
  }

  private static float Sample(float[] table, float t)
  {
    int i = Mathf.Min(Mathf.FloorToInt(t), table.Length - 2);
    return Mathf.Lerp(table[i], table[i + 1], t - i);
  }

  private void CreateStyles()
  {
    if (nameStyle != null) return;

    nameStyle = new GUIStyle(GUI.skin.label)
    {
      fontStyle = FontStyle.Bold,
      alignment = TextAnchor.MiddleCenter
    };
    nameStyle.normal.textColor = new Color(0.92f, 0.96f, 1f);

    hintStyle = new GUIStyle(GUI.skin.label)
    {
      alignment = TextAnchor.MiddleCenter
    };
    hintStyle.normal.textColor = new Color(0.75f, 0.85f, 1f, 0.85f);

    titleStyle = new GUIStyle(GUI.skin.label)
    {
      fontStyle = FontStyle.Bold,
      alignment = TextAnchor.MiddleCenter
    };
    titleStyle.normal.textColor = new Color(0.85f, 0.95f, 1f);

    subtitleStyle = new GUIStyle(GUI.skin.label)
    {
      fontStyle = FontStyle.Italic,
      wordWrap = true,
      alignment = TextAnchor.LowerLeft
    };
    subtitleStyle.normal.textColor = new Color(1f, 0.8f, 0.35f);

    textStyle = new GUIStyle(GUI.skin.label)
    {
      wordWrap = true,
      alignment = TextAnchor.UpperLeft
    };
    textStyle.normal.textColor = new Color(0.92f, 0.94f, 1f);

    buttonTextStyle = new GUIStyle(nameStyle);
  }
}
