using GuardiasDaLuz.MapSystem;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GuardiasDaLuz
{
    /// <summary>
    /// HUD DE ACESSIBILIDADE & MULTIPLAYER ("Luna") — módulo do projeto GameJAM (vinikyo), adaptado para este jogo.
    /// A vida, a mana, as armas, o minimapa e as legendas continuam sendo desenhados pelos scripts do jogo; esta HUD soma:
    /// - Menu de Acessibilidade [F1 ou Tab]: velocidade do jogo (100/75/50%, travada em 100% no co-op), alto contraste /
    ///   visão noturna, legendas visuais liga/desliga, filtro de daltonismo (Deuteranopia, Protanopia, Tritanopia),
    ///   minimapa sem spoilers, tutorial e reinício [F5]. As escolhas ficam salvas (PlayerPrefs).
    /// - Tutorial no canto esquerdo [H], com os comandos da personagem que ficam verdes [OK] ao serem testados.
    /// - Painéis das parceiras no modo cooperativo (vida e mana de cada uma) e o estado da conexão.
    /// - Tela de vitória ao acender o topo do farol (fim do Andar 3-2).
    /// </summary>
    public class MultiplayerAndAccessibilityHUD : MonoBehaviour
    {
        public static MultiplayerAndAccessibilityHUD Instance { get; private set; }

        private const string SpeedKey = "acc_velocidade";
        private const string ContrastKey = "acc_alto_contraste";
        private const string SubtitlesKey = "acc_legendas";
        private const string ColorblindKey = "acc_daltonismo";
        private const string SpoilerKey = "LUMINAS_HIDE_ROOM_SPOILERS";
        private const string TutorialKey = "acc_tutorial";

        [Header("=== TUTORIAL VISUAL NO CANTO DA TELA (Tecla H abre/fecha) ===")]
        public bool showCornerTutorialPanel = true;
        public bool stepMovedDone;
        public bool stepShotDone;
        public bool stepSwitchedShotDone;
        public bool stepRepairedPostDone;

        [Header("=== ACESSIBILIDADE ===")]
        public bool showAccessibilityMenu;
        [Range(0.5f, 1.0f)] public float accessibleGameSpeed = 1.0f;
        public bool highContrastAssistiveLight;
        public bool visualAudioSubtitles = true;
        public int colorblindModeIndex;

        private Texture2D panelTex;
        private Texture2D infoTex;
        private Texture2D cardTex;
        private Texture2D cardDoneTex;
        private Texture2D greenTex;
        private Texture2D goldTex;
        private Texture2D purpleTex;
        private Texture2D barBackTex;
        private Texture2D manaTex;
        private Volume accessibilityVolume;
        private PlayerShooting trackedShooting;
        private PlayerMovement trackedMovement;
        private WeaponType lastWeapon;
        private bool boundMapEvents;

        // Cria a HUD em toda cena que tenha o mapa do farol.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureForScene();

        private static void EnsureForScene()
        {
            if (Instance != null || FindAnyObjectByType<StandaloneLighthouseMapSystem>() == null) return;
            new GameObject("HUD_Acessibilidade_Multiplayer").AddComponent<MultiplayerAndAccessibilityHUD>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            CreateHUDTextures();
            LoadSettings();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            ApplyGameSpeed(accessibleGameSpeed);
            ApplyHighContrastMode(highContrastAssistiveLight, false);
            ApplyColorblindMode(colorblindModeIndex, false);
            ApplySubtitles(visualAudioSubtitles);
            StandaloneLighthouseMapSystem.HideUnvisitedRoomsOnMinimap = PlayerPrefs.GetInt(SpoilerKey, 0) == 1;
        }

        private void LoadSettings()
        {
            accessibleGameSpeed = PlayerPrefs.GetFloat(SpeedKey, 1f);
            highContrastAssistiveLight = PlayerPrefs.GetInt(ContrastKey, 0) == 1;
            visualAudioSubtitles = PlayerPrefs.GetInt(SubtitlesKey, 1) == 1;
            colorblindModeIndex = PlayerPrefs.GetInt(ColorblindKey, 0);
            showCornerTutorialPanel = PlayerPrefs.GetInt(TutorialKey, 1) == 1;
        }

        private void CreateHUDTextures()
        {
            panelTex = MakeTex(new Color(0.05f, 0.07f, 0.14f, 0.92f));
            infoTex = MakeTex(new Color(0.11f, 0.08f, 0.20f, 0.92f));
            cardTex = MakeTex(new Color(0.14f, 0.18f, 0.30f, 0.95f));
            cardDoneTex = MakeTex(new Color(0.10f, 0.36f, 0.20f, 0.95f));
            greenTex = MakeTex(new Color(0.22f, 0.88f, 0.38f, 0.95f));
            goldTex = MakeTex(new Color(1.0f, 0.82f, 0.22f, 0.95f));
            purpleTex = MakeTex(new Color(0.75f, 0.25f, 0.98f, 0.95f));
            barBackTex = MakeTex(new Color(0.18f, 0.08f, 0.10f, 0.90f));
            manaTex = MakeTex(new Color(0.20f, 0.85f, 1.0f, 0.95f));
        }

        private static Texture2D MakeTex(Color col)
        {
            Texture2D t = new Texture2D(2, 2);
            t.SetPixels(new[] { col, col, col, col });
            t.Apply();
            return t;
        }

        private static bool IsMultiplayerSession => CoopSession.IsCoop;

        private void Update()
        {
            TrackTutorialSteps();

            if (Input.GetKeyDown(KeyCode.H)) SetTutorialVisible(!showCornerTutorialPanel);
            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.Tab)) showAccessibilityMenu = !showAccessibilityMenu;
            if (Input.GetKeyDown(KeyCode.F5)) RestartRun();

            // no co-op a velocidade fica em 100% (sincronia de rede)
            if (IsMultiplayerSession && !Mathf.Approximately(Time.timeScale, 1f) && Time.timeScale > 0f) Time.timeScale = 1f;
        }

        private void TrackTutorialSteps()
        {
            Transform local = LunaNetworkSyncManager.FindLocalPlayer();
            if (local == null) return;

            if (trackedMovement == null) trackedMovement = local.GetComponent<PlayerMovement>();
            if (trackedShooting == null)
            {
                trackedShooting = local.GetComponent<PlayerShooting>();
                if (trackedShooting != null)
                {
                    lastWeapon = trackedShooting.CurrentWeapon;
                    trackedShooting.Fired += weapon => stepShotDone = true;
                }
            }
            if (trackedMovement != null && trackedMovement.IsMoving) stepMovedDone = true;
            if (trackedShooting != null && trackedShooting.CurrentWeapon != lastWeapon) stepSwitchedShotDone = true;

            StandaloneLighthouseMapSystem map = StandaloneLighthouseMapSystem.Instance;
            if (!boundMapEvents && map != null)
            {
                boundMapEvents = true;
                if (map.onSmartPostRepaired == null) map.onSmartPostRepaired = new UnityEngine.Events.UnityEvent<GameObject>();
                map.onSmartPostRepaired.AddListener(post => stepRepairedPostDone = true);
            }
        }

        public void SetSubtitle(string message)
        {
            if (StandaloneLighthouseMapSystem.Instance != null) StandaloneLighthouseMapSystem.Instance.ShowBannerMessage(message);
        }

        private void OnGUI()
        {
            if (panelTex == null) CreateHUDTextures();

            DrawShortcutHint();
            DrawCoopPartnerPanels();

            StandaloneLighthouseMapSystem map = StandaloneLighthouseMapSystem.Instance;
            if (showCornerTutorialPanel && !showAccessibilityMenu) DrawCornerTutorialPanels();
            if (showAccessibilityMenu) DrawAccessibilityAndMultiplayerModal();
            if (map != null && map.isLighthouseCompleted) DrawVictoryScreen();
        }

        private void DrawShortcutHint()
        {
            GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            s.normal.textColor = new Color(0.85f, 0.88f, 1f, 0.75f);
            string coop = IsMultiplayerSession ? $"  •  CO-OP {LunaNetworkSyncManager.Instance.ConnectedPeersCount + 1}P" : "";
            GUI.Label(new Rect(Screen.width * 0.5f - 220f, 4f, 440f, 18f), $"[F1] Acessibilidade  •  [H] Tutorial  •  [F5] Reiniciar{coop}", s);
        }

        // Vida e mana de cada parceira, abaixo do minimapa.
        private void DrawCoopPartnerPanels()
        {
            if (!IsMultiplayerSession) return;
            GUIStyle label = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            label.normal.textColor = Color.white;

            int index = 0;
            foreach (CoopRemoteAvatar partner in LunaNetworkSyncManager.Instance.RemoteAvatars())
            {
                float w = 240f;
                float x = Screen.width - w - 18f;
                float y = 222f + index * 62f;
                Rect bg = new Rect(x, y, w, 56f);
                GUI.DrawTexture(bg, panelTex);
                GUI.color = partner.slotColor;
                GUI.DrawTexture(new Rect(bg.x, bg.y, bg.width, 3f), Texture2D.whiteTexture);
                GUI.color = Color.white;

                Rect hp = new Rect(x + 8f, y + 8f, w - 16f, 19f);
                GUI.DrawTexture(hp, barBackTex);
                GUI.DrawTexture(new Rect(hp.x, hp.y, hp.width * Mathf.Clamp01(partner.health / Mathf.Max(1f, partner.maxHealth)), hp.height), greenTex);
                string state = partner.isDead ? "CAÍDA" : $"{Mathf.Ceil(partner.health):F0}/{partner.maxHealth:F0}";
                GUI.Label(hp, $"{partner.nickname}  VIDA {state}", label);

                Rect mana = new Rect(x + 8f, y + 31f, w - 16f, 17f);
                GUI.DrawTexture(mana, barBackTex);
                GUI.DrawTexture(new Rect(mana.x, mana.y, mana.width * Mathf.Clamp01(partner.mana / Mathf.Max(1f, partner.maxMana)), mana.height), manaTex);
                GUI.Label(mana, $"MANA {partner.mana:F0}/{partner.maxMana:F0}", label);
                index++;
            }
        }

        private void DrawCornerTutorialPanels()
        {
            float panelX = 16f;
            float panelY = 96f;
            float panelW = 340f;

            float block1H = 196f;
            Rect block1 = new Rect(panelX, panelY, panelW, block1H);
            GUI.DrawTexture(block1, panelTex);
            bool allDone = stepMovedDone && stepShotDone && stepSwitchedShotDone && stepRepairedPostDone;
            GUI.DrawTexture(new Rect(block1.x, block1.y, block1.width, 3f), allDone ? greenTex : goldTex);

            GUIStyle header = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            header.normal.textColor = allDone ? new Color(0.35f, 1f, 0.55f) : new Color(1f, 0.92f, 0.35f);

            GUILayout.BeginArea(new Rect(block1.x + 10f, block1.y + 7f, block1.width - 20f, block1.height - 12f));
            GUILayout.Label(allDone ? "[OK] COMANDOS DOMINADOS!" : "1. TESTE OS COMANDOS DA PERSONAGEM:", header);
            GUILayout.Space(4f);
            float cardW = (panelW - 28f) * 0.5f;
            const float cardH = 72f;
            GUILayout.BeginHorizontal();
            DrawCompactControlCard(cardW, cardH, stepMovedDone, "[W][A][S][D]", "Correr", "Setas também funcionam.");
            GUILayout.Space(6f);
            DrawCompactControlCard(cardW, cardH, stepShotDone, "[Mouse / Espaço]", "Atirar", "Gasta mana e munição.");
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            DrawCompactControlCard(cardW, cardH, stepSwitchedShotDone, "[1-6 / Roda]", "Trocar de arma", "6 padrões de tiro.");
            GUILayout.Space(6f);
            DrawCompactControlCard(cardW, cardH, stepRepairedPostDone, "[Segurar E]", "Consertar poste", "Abre as portas da sala.");
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            float block2Y = panelY + block1H + 8f;
            const float block2H = 150f;
            Rect block2 = new Rect(panelX, block2Y, panelW, block2H);
            GUI.DrawTexture(block2, infoTex);
            GUI.DrawTexture(new Rect(block2.x, block2.y, block2.width, 3f), purpleTex);

            GUIStyle infoHeader = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            infoHeader.normal.textColor = new Color(0.85f, 0.65f, 1f);
            GUIStyle body = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            body.normal.textColor = new Color(0.90f, 0.92f, 1f);

            GUILayout.BeginArea(new Rect(block2.x + 12f, block2.y + 8f, block2.width - 24f, block2.height - 14f));
            GUILayout.Label("DICA — O FAROL (3 NÍVEIS x 2 ANDARES):", infoHeader);
            GUILayout.Label("- Derrote os monstros e conserte o poste para abrir as portas. A sala ★FIM leva ao próximo andar.", body);
            GUILayout.Label("- Andares 1-2, 2-2 e 3-2 têm um CHEFE. Rótulos acima dos monstros avisam: Venenoso, Veloz, Atordoante, Gélido...", body);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ocultar [H]", GUILayout.Height(22f))) SetTutorialVisible(false);
            if (GUILayout.Button("[F1] Acessibilidade", GUILayout.Width(140f), GUILayout.Height(22f))) showAccessibilityMenu = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawCompactControlCard(float width, float height, bool done, string keyBadge, string title, string desc)
        {
            Rect card = GUILayoutUtility.GetRect(width, height);
            GUI.DrawTexture(card, done ? cardDoneTex : cardTex);
            GUI.DrawTexture(new Rect(card.x, card.y, card.width, 2f), done ? greenTex : goldTex);

            GUIStyle badge = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            badge.normal.textColor = done ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.90f, 0.35f);
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = Color.white;
            GUIStyle descStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true, alignment = TextAnchor.UpperCenter };
            descStyle.normal.textColor = new Color(0.85f, 0.92f, 0.98f);

            GUI.Label(new Rect(card.x + 4f, card.y + 4f, card.width - 8f, 18f), done ? $"{keyBadge} [OK]" : keyBadge, badge);
            GUI.Label(new Rect(card.x + 4f, card.y + 22f, card.width - 8f, 18f), title, titleStyle);
            GUI.Label(new Rect(card.x + 6f, card.y + 40f, card.width - 12f, 30f), desc, descStyle);
        }

        private void DrawAccessibilityAndMultiplayerModal()
        {
            bool coop = IsMultiplayerSession;
            float modalH = coop ? 420f : 370f;
            Rect r = new Rect(Screen.width * 0.5f - 270f, Screen.height * 0.5f - modalH * 0.5f, 540f, modalH);
            GUI.DrawTexture(r, panelTex);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), goldTex);
            GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(r.x, r.y + 6f, r.width, 22f), "ACESSIBILIDADE & MULTIPLAYER  (F1 ou Tab para fechar)", title);

            GUILayout.BeginArea(new Rect(r.x + 20f, r.y + 34f, r.width - 40f, r.height - 44f));

            GUILayout.Label("1. Velocidade do jogo (acessibilidade motora):");
            if (coop)
            {
                GUILayout.Label("   [TRAVADA EM 100% NO MODO COOPERATIVO (SINCRONIA DE REDE)]");
            }
            else
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(Mark("100% (Normal)", Mathf.Approximately(accessibleGameSpeed, 1f)))) ApplyGameSpeed(1f);
                if (GUILayout.Button(Mark("75% (Acessível)", Mathf.Approximately(accessibleGameSpeed, 0.75f)))) ApplyGameSpeed(0.75f);
                if (GUILayout.Button(Mark("50% (Calmo)", Mathf.Approximately(accessibleGameSpeed, 0.5f)))) ApplyGameSpeed(0.5f);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
            bool contrast = GUILayout.Toggle(highContrastAssistiveLight, " 2. Alto contraste / visão noturna assistiva");
            if (contrast != highContrastAssistiveLight) ApplyHighContrastMode(contrast, true);

            bool subtitles = GUILayout.Toggle(visualAudioSubtitles, " 3. Legendas visuais para eventos e portas");
            if (subtitles != visualAudioSubtitles) ApplySubtitles(subtitles);

            GUILayout.Space(6f);
            GUILayout.Label("4. Filtro de daltonismo:");
            GUILayout.BeginHorizontal();
            string[] names = { "Padrão", "Deuteranopia", "Protanopia", "Tritanopia" };
            for (int i = 0; i < names.Length; i++)
            {
                GUI.backgroundColor = colorblindModeIndex == i ? new Color(0.3f, 0.9f, 1f) : Color.white;
                if (GUILayout.Button(names[i], GUILayout.Height(26f))) ApplyColorblindMode(i, true);
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            bool hide = StandaloneLighthouseMapSystem.HideUnvisitedRoomsOnMinimap;
            if (GUILayout.Button(hide ? "5. Minimapa: salas não visitadas OCULTAS (?)" : "5. Minimapa: todas as salas REVELADAS", GUILayout.Height(26f)))
            {
                StandaloneLighthouseMapSystem.HideUnvisitedRoomsOnMinimap = !hide;
                PlayerPrefs.SetInt(SpoilerKey, hide ? 0 : 1);
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("[H] Mostrar / ocultar o tutorial", GUILayout.Height(26f)))
            {
                showAccessibilityMenu = false;
                SetTutorialVisible(!showCornerTutorialPanel);
            }
            if (GUILayout.Button("[F5] Reiniciar a expedição do zero", GUILayout.Height(26f))) RestartRun();

            if (coop)
            {
                GUILayout.Space(8f);
                LunaNetworkSyncManager net = LunaNetworkSyncManager.Instance;
                GUILayout.Label($"CO-OP: {(net.currentRole == LunaNetworkSyncManager.NetworkRole.Host ? "Host" : "Cliente")} • Você é a Jogadora {net.MyPlayerId + 1} • {net.ConnectedPeersCount + 1}/4 • Semente {CoopSession.customDungeonSeed}");
                if (GUILayout.Button("Sair do modo cooperativo e voltar ao menu", GUILayout.Height(26f))) CoopSession.TerminateMultiplayerSession();
            }

            GUILayout.EndArea();
            PlayerPrefs.Save();
        }

        private static string Mark(string label, bool on) => on ? $"> {label} <" : label;

        private void SetTutorialVisible(bool on)
        {
            showCornerTutorialPanel = on;
            PlayerPrefs.SetInt(TutorialKey, on ? 1 : 0);
        }

        private void DrawVictoryScreen()
        {
            Rect r = new Rect(Screen.width * 0.5f - 280f, Screen.height * 0.5f - 130f, 560f, 260f);
            GUI.DrawTexture(r, panelTex);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 4f), goldTex);
            GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(1f, 0.85f, 0.3f);
            GUIStyle body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            body.normal.textColor = new Color(0.92f, 0.94f, 1f);

            GUILayout.BeginArea(new Rect(r.x + 30f, r.y + 20f, r.width - 60f, r.height - 40f));
            GUILayout.Label("*** FAROL 100% ILUMINADO! ***", title);
            GUILayout.Space(10f);
            GUILayout.Label("Os três chefes caíram e a Luz Mestra do topo voltou a brilhar sobre a costa. Parabéns, guardiã!", body);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Jogar novamente (novo farol) [F5]", GUILayout.Height(40f))) RestartRun();
            if (GUILayout.Button("Voltar ao menu", GUILayout.Height(30f)))
            {
                if (IsMultiplayerSession) CoopSession.TerminateMultiplayerSession();
                else SceneManager.LoadScene(CoopSession.MenuScene);
            }
            GUILayout.EndArea();
        }

        private void RestartRun()
        {
            Time.timeScale = accessibleGameSpeed;
            if (IsMultiplayerSession && LunaNetworkSyncManager.Instance.currentRole != LunaNetworkSyncManager.NetworkRole.Host)
            {
                SetSubtitle("[CO-OP] Só o Host pode reiniciar a expedição.");
                return;
            }
            if (IsMultiplayerSession) LunaNetworkSyncManager.Instance.RestartRunForAll(CoopSession.playerNickname);
            else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ApplyGameSpeed(float speed)
        {
            accessibleGameSpeed = IsMultiplayerSession ? 1f : Mathf.Clamp(speed, 0.5f, 1f);
            Time.timeScale = accessibleGameSpeed;
            PlayerPrefs.SetFloat(SpeedKey, accessibleGameSpeed);
        }

        public void ApplySubtitles(bool enabled)
        {
            visualAudioSubtitles = enabled;
            PlayerPrefs.SetInt(SubtitlesKey, enabled ? 1 : 0);
            if (StandaloneLighthouseMapSystem.Instance != null) StandaloneLighthouseMapSystem.Instance.showBuiltInSubtitleHUD = enabled;
        }

        // Um Volume global só da acessibilidade (não mexe no perfil de pós-processamento da cena).
        private Volume GetAccessibilityVolume()
        {
            if (accessibilityVolume != null) return accessibilityVolume;
            GameObject vObj = new GameObject("Volume_Acessibilidade");
            vObj.transform.SetParent(transform, false);
            accessibilityVolume = vObj.AddComponent<Volume>();
            accessibilityVolume.isGlobal = true;
            accessibilityVolume.priority = 100f;
            accessibilityVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            return accessibilityVolume;
        }

        public void ApplyColorblindMode(int modeIndex, bool announce)
        {
            colorblindModeIndex = Mathf.Clamp(modeIndex, 0, 3);
            PlayerPrefs.SetInt(ColorblindKey, colorblindModeIndex);

            VolumeProfile profile = GetAccessibilityVolume().profile;
            if (!profile.TryGet(out ChannelMixer mixer)) mixer = profile.Add<ChannelMixer>(true);

            float[] m;
            switch (colorblindModeIndex)
            {
                case 1: m = new[] { 62.5f, 37.5f, 0f, 70f, 30f, 0f, 0f, 30f, 70f }; break;       // Deuteranopia
                case 2: m = new[] { 56.6f, 43.3f, 0f, 55.8f, 44.2f, 0f, 0f, 24.2f, 75.8f }; break; // Protanopia
                case 3: m = new[] { 95f, 5f, 0f, 0f, 43.3f, 56.7f, 0f, 47.5f, 52.5f }; break;     // Tritanopia
                default: m = new[] { 100f, 0f, 0f, 0f, 100f, 0f, 0f, 0f, 100f }; break;
            }
            mixer.redOutRedIn.Override(m[0]); mixer.redOutGreenIn.Override(m[1]); mixer.redOutBlueIn.Override(m[2]);
            mixer.greenOutRedIn.Override(m[3]); mixer.greenOutGreenIn.Override(m[4]); mixer.greenOutBlueIn.Override(m[5]);
            mixer.blueOutRedIn.Override(m[6]); mixer.blueOutGreenIn.Override(m[7]); mixer.blueOutBlueIn.Override(m[8]);
            mixer.active = colorblindModeIndex > 0;
            UpdateColorAdjustments();

            if (announce)
            {
                string[] names = { "Padrão (Normal)", "Deuteranopia (Verde)", "Protanopia (Vermelho)", "Tritanopia (Azul)" };
                SetSubtitle($"[ACESSIBILIDADE] Filtro de daltonismo: {names[colorblindModeIndex]}.");
            }
        }

        public void ApplyHighContrastMode(bool enabled, bool announce)
        {
            highContrastAssistiveLight = enabled;
            PlayerPrefs.SetInt(ContrastKey, enabled ? 1 : 0);
            UpdateColorAdjustments();
            if (announce)
            {
                SetSubtitle(enabled ? "[ACESSIBILIDADE] Alto contraste / visão noturna: ATIVADO!" : "[ACESSIBILIDADE] Alto contraste / visão noturna: DESATIVADO.");
            }
        }

        private void UpdateColorAdjustments()
        {
            VolumeProfile profile = GetAccessibilityVolume().profile;
            if (!profile.TryGet(out ColorAdjustments colorAdj)) colorAdj = profile.Add<ColorAdjustments>(true);
            float contrast = highContrastAssistiveLight ? 40f : colorblindModeIndex == 3 ? 22f : colorblindModeIndex > 0 ? 25f : 0f;
            colorAdj.postExposure.Override(highContrastAssistiveLight ? 1.6f : 0f);
            colorAdj.contrast.Override(contrast);
            colorAdj.saturation.Override(colorblindModeIndex > 0 ? 20f : 0f);
            colorAdj.active = highContrastAssistiveLight || colorblindModeIndex > 0;
        }
    }
}
