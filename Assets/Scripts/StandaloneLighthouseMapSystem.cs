using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GuardiasDaLuz.MapSystem
{
    public class StandaloneLighthouseMapSystem : MonoBehaviour
    {
        public static StandaloneLighthouseMapSystem Instance { get; private set; }

        public enum RoomType
        {
            Start,
            Combat,
            GiantHall,
            TreasureChoice,
            Secret,
            BossExit
        }

        public enum ObstacleLayoutVariant
        {
            None,
            FourCornerPillars,
            TwinLBunkers,
            CentralCrossCorridors,
            Level1_PierContainers,
            Level2_SolarPanelsArray,
            Level3_SmartGridServerRacks
        }

        [Serializable]
        public class FloorStageConfig
        {
            public string floorTitle = "Nível 1 • Andar 1-1: Píer do Farol";
            public int levelNumber = 1;
            public int floorInLevel = 1;
            public int minRooms = 8;
            public int maxRooms = 15;
            public int secretRoomsCount = 2;
            public int giantHallPairsCount = 1;
            public int monstersPerRoom = 3;
            public int breakableCratesPerRoom = 16;
            public Color floorColor = new Color(0.16f, 0.17f, 0.22f);
            public Color wallColor = new Color(0.24f, 0.27f, 0.35f);
        }

        [Header("=== 1. JOGADORA & ALIADOS (INTEGRAÇÃO COM PERSONAGENS) ===")]
        [Tooltip("Arraste a Transform da Jogadora aqui. Se vazio, o mapa busca automaticamente pela Tag 'Player'.")]
        public Transform playerTransform;

        [Tooltip("Se ativado e não houver nenhuma Jogadora na cena, cria uma cápsula de teste (WASD + Tiro) para você testar o mapa sozinho!")]
        public bool spawnFallbackTestPlayerIfNoneFound = true;

        [Tooltip("Lista de Companheiros/Bots aliados. Qualquer aliado aqui (ou com a Tag 'Companion'/'Ally') é puxado para dentro da sala quando as portas fecham!")]
        public List<Transform> companionTransforms = new List<Transform>();

        [Tooltip("Quantidade atual de Cristais/Moedas da jogadora (usado pelos pedestais que têm custo).")]
        public int playerCurrencyCrystals = 25;

        [Header("=== 2. PREFABS DE MONSTROS (DA PROGRAMADORA DE PERSONAGENS) ===")]
        [Tooltip("Arraste aqui os Prefabs de monstros comuns e atiradores criados pela menina dos personagens! Se vazio, cria alvos de teste.")]
        public GameObject[] customMonsterPrefabs;

        [Tooltip("Arraste aqui o Prefab do Boss/Guardião do fim do andar! Se vazio, cria um Guardião de teste maior.")]
        public GameObject customBossMonsterPrefab;

        [Tooltip("Se não houver Prefabs de monstros atribuídos acima, criar cápsulas de monstros de teste para testar o destrancamento de salas?")]
        public bool spawnPlaceholderMonstersIfPrefabsEmpty = false;

        [Header("=== 3. PREFABS VISUAIS DE CENÁRIO (OPCIONAIS - PARA O ARTISTA 3D) ===")]
        public GameObject customFloorPrefab;
        public GameObject customWallPrefab;
        public GameObject customDoorBarrierPrefab;
        public GameObject customSmartLightPostPrefab;
        public GameObject customBreakableCratePrefab;
        public GameObject customRareDropBatteryPrefab;
        public GameObject customPedestalPrefab;

        [Header("=== 3B. KITS MODULARES DE CENÁRIO POR NÍVEL (Assets/Levels) ===")]
        [Tooltip("Um kit por Nível: posição 0 = Nível 1, 1 = Nível 2, 2 = Nível 3. Piso, paredes, portas e pilares das salas usam essas peças. Se o kit estiver vazio, o nível usa os blocos coloridos padrão.")]
        public LevelVisualTheme[] levelThemes = new LevelVisualTheme[]
        {
            new LevelVisualTheme
            {
                themeName = "Nível 1 • Laboratório", assetFolder = "Assets/Levels/L1_Lab",
                mainFloorName = "Lab_Floor_Plain", accentFloorName = "Lab_Floor_Grate"
            },
            new LevelVisualTheme
            {
                themeName = "Nível 2 • Cidade", assetFolder = "Assets/Levels/L2_City",
                mainFloorName = "City_Floor_Pavers", accentFloorName = "City_Floor_Cobble"
            },
            new LevelVisualTheme
            {
                themeName = "Nível 3 • Jardim", assetFolder = "Assets/Levels/L3_Garden",
                mainFloorName = "Garden_Floor_Grass", accentFloorName = "Garden_Floor_Stone"
            }
        };

        [Header("=== 3C. DECORAÇÃO DAS SALAS (Assets/Decor) ===")]
        [Tooltip("Um kit de objetos por Nível: posição 0 = Nível 1, 1 = Nível 2, 2 = Nível 3. Os objetos ficam encostados nas paredes, longe das portas.")]
        public RoomDecorKit[] levelDecorKits = new RoomDecorKit[]
        {
            new RoomDecorKit
            {
                kitName = "Nível 1 • Laboratório",
                standingPropNames = new[] { "server", "pc", "table", "toolbox", "crate", "barrel", "laser", "sign_warn", "bin" },
                wallMountedPropNames = new[] { "led_wall", "window", "door" },
                floorPanelNames = new[] { "led_floor" }
            },
            new RoomDecorKit
            {
                kitName = "Nível 2 • Cidade",
                standingPropNames = new[] { "trafic", "cone", "bench", "bin", "lamp", "park", "sign", "gate", "barrel", "crate", "gun" },
                wallMountedPropNames = new[] { "window", "door" },
                floorPanelNames = new string[0]
            },
            new RoomDecorKit
            {
                kitName = "Nível 3 • Jardim",
                standingPropNames = new[] { "plant", "bench", "lamp", "bin", "gate", "sign", "table", "barrel" },
                wallMountedPropNames = new[] { "window", "door" },
                floorPanelNames = new string[0]
            }
        };
        [Tooltip("Modelo do Poste Inteligente no centro das salas (usado quando 'customSmartLightPostPrefab' está vazio).")]
        public GameObject decorCenterLight;
        [Tooltip("Modelo das folhas da porta trancada (usado quando 'customDoorBarrierPrefab' está vazio).")]
        public GameObject decorMetalDoor;
        [Tooltip("Cadeado mostrado nas portas trancadas.")]
        public GameObject decorLock;
        [Tooltip("Modelos que vestem caixas quebráveis, contêineres e pedestais no lugar dos cubos comuns.")]
        public GameObject decorCrate;
        [Tooltip("Modelo que veste pilares e racks no lugar dos cubos comuns.")]
        public GameObject decorServer;
        [Tooltip("Modelo que veste pilares redondos (amarração, capacitores) no lugar dos cilindros comuns.")]
        public GameObject decorBarrel;
        [Tooltip("Modelo que veste os painéis solares no lugar dos cubos comuns.")]
        public GameObject decorSolarPanel;

        [Header("=== 3C2. PORTAL PARA O PRÓXIMO ANDAR ===")]
        [Tooltip("Material da fumaça mística do portal. No editor, o campo vazio é preenchido com Monsters/Generated/SmokeParticle.mat.")]
        public Material portalSmokeMaterial;
        [Tooltip("Material das faíscas que sobem do portal. No editor, o campo vazio é preenchido com Monsters/Generated/Mote.mat.")]
        public Material portalSparkMaterial;

        [Header("=== 3D. MONSTROS (Assets/Sprites/Monster) ===")]
        [Tooltip("Modelos sorteados para os inimigos de sombra. No editor, a lista vazia é preenchida com todos os FBX da pasta.")]
        public GameObject[] monsterModels;

        [Header("=== 3E. POWER-UPS (Assets/Powerups) ===")]
        [Tooltip("Modelo do power-up Bateria (recupera vida). No editor, o campo vazio é preenchido com battery.fbx.")]
        public GameObject powerUpBatteryModel;
        [Tooltip("Modelo dos outros power-ups, tingido com a cor de cada tipo. No editor, o campo vazio é preenchido com crystal.fbx.")]
        public GameObject powerUpCrystalModel;
        [Tooltip("Som ao pegar Mais vida. No editor, o campo vazio é preenchido a partir de Assets/Audios/Projeto_UV_4_PowerUps_SFX.")]
        public AudioClip powerUpLightCrystalSound;
        [Tooltip("Som ao pegar a bateria (power-up e bateria rara das caixas).")]
        public AudioClip powerUpEnergySound;
        [Tooltip("Som ao pegar Tiro que ricocheteia e Velocidade de tiro.")]
        public AudioClip powerUpRetroTechSound;
        [Tooltip("Som ao pegar Velocidade de movimento.")]
        public AudioClip powerUpSpecialSound;
        [Range(0f, 1f)] public float powerUpSoundVolume = 0.9f;

        [Header("=== 4. DIMENSÕES DAS SALAS & REGRAS DE LEVEL DESIGN ===")]
        public float roomWidthX = 32f;
        public float roomLengthZ = 26f;
        public float doorOpeningWidth = 5.4f;
        [Range(0f, 1f)] public float rareCrateDropChance = 0.10f;
        public bool showBuiltInMinimapHUD = true;
        public bool showBuiltInSubtitleHUD = true;
        [Tooltip("Cada sala com poste sorteia um limite total de monstros entre o mínimo e o máximo (nunca mais que 20). Depois disso, não nascem mais inimigos nela.")]
        [Range(1, MonsterQuotaCap)] public int minMonstersPerRoom = 6;
        [Range(1, MonsterQuotaCap)] public int maxMonstersPerRoom = MonsterQuotaCap;
        private const int MonsterQuotaCap = 20;
        [Tooltip("Salas sem poste (tesouro, secreta) ficam no escuro e atraem alguns monstros, mas as portas continuam abertas.")]
        [Range(0, MonsterQuotaCap)] public int minMonstersInPostlessRoom = 2;
        [Range(0, MonsterQuotaCap)] public int maxMonstersInPostlessRoom = 4;

        [Header("=== 4B. ESCURIDÃO DAS SALAS ===")]
        [Tooltip("Deixa o mapa quase todo escuro: desliga a luz direcional e baixa a luz ambiente. Cada sala recebe só algumas luzes fracas.")]
        public bool darkRooms = true;
        public Color darkAmbientColor = new Color(0.16f, 0.16f, 0.22f);
        [Range(0, 4)] public int dimLightsPerRoom = 4;
        public float dimLightIntensity = 6f;
        public float dimLightRange = 12f;
        public Color dimLightColor = new Color(0.55f, 0.65f, 1f);
        [Tooltip("Segundos para a sala clarear quando o poste é consertado (e escurecer ao entrar numa sala apagada).")]
        public float lightTransitionSeconds = 1.2f;

        [Header("=== 5. CONFIGURAÇÃO DOS 6 ANDARES (3 NÍVEIS DO FAROL) ===")]
        public FloorStageConfig[] floors = new FloorStageConfig[]
        {
            new FloorStageConfig
            {
                floorTitle = "Nível 1 • Andar 1-1: Píer & Entrada do Farol",
                levelNumber = 1, floorInLevel = 1,
                minRooms = 8, maxRooms = 15, secretRoomsCount = 1, giantHallPairsCount = 1,
                monstersPerRoom = 3, breakableCratesPerRoom = 14,
                floorColor = new Color(0.16f, 0.17f, 0.22f), wallColor = new Color(0.24f, 0.27f, 0.35f)
            },
            new FloorStageConfig
            {
                floorTitle = "Nível 1 • Andar 1-2: Fundações Costeiras",
                levelNumber = 1, floorInLevel = 2,
                minRooms = 10, maxRooms = 15, secretRoomsCount = 2, giantHallPairsCount = 1,
                monstersPerRoom = 4, breakableCratesPerRoom = 15,
                floorColor = new Color(0.14f, 0.18f, 0.24f), wallColor = new Color(0.22f, 0.29f, 0.38f)
            },
            new FloorStageConfig
            {
                floorTitle = "Nível 2 • Andar 2-1: Galeria Fotovoltaica",
                levelNumber = 2, floorInLevel = 1,
                minRooms = 15, maxRooms = 25, secretRoomsCount = 2, giantHallPairsCount = 2,
                monstersPerRoom = 4, breakableCratesPerRoom = 16,
                floorColor = new Color(0.12f, 0.20f, 0.18f), wallColor = new Color(0.18f, 0.32f, 0.28f)
            },
            new FloorStageConfig
            {
                floorTitle = "Nível 2 • Andar 2-2: Reatores Solares de Mária Telkes",
                levelNumber = 2, floorInLevel = 2,
                minRooms = 18, maxRooms = 25, secretRoomsCount = 2, giantHallPairsCount = 2,
                monstersPerRoom = 5, breakableCratesPerRoom = 16,
                floorColor = new Color(0.14f, 0.22f, 0.16f), wallColor = new Color(0.20f, 0.35f, 0.25f)
            },
            new FloorStageConfig
            {
                floorTitle = "Nível 3 • Andar 3-1: Torre de Transmissão Smart Grid",
                levelNumber = 3, floorInLevel = 1,
                minRooms = 25, maxRooms = 35, secretRoomsCount = 2, giantHallPairsCount = 3,
                monstersPerRoom = 5, breakableCratesPerRoom = 18,
                floorColor = new Color(0.16f, 0.14f, 0.24f), wallColor = new Color(0.28f, 0.22f, 0.40f)
            },
            new FloorStageConfig
            {
                floorTitle = "Nível 3 • Andar 3-2: Cúpula da Lente Mestra (Topo do Farol)",
                levelNumber = 3, floorInLevel = 2,
                minRooms = 28, maxRooms = 35, secretRoomsCount = 2, giantHallPairsCount = 3,
                monstersPerRoom = 6, breakableCratesPerRoom = 18,
                floorColor = new Color(0.20f, 0.15f, 0.28f), wallColor = new Color(0.35f, 0.25f, 0.48f)
            }
        };

        [Header("=== 6. EVENTOS DE INTEGRAÇÃO (PLUGAR SCRIPTS DE PERSONAGEM/UI) ===")]
        [Tooltip("Disparado quando um novo andar começa. Parâmetros: (Índice do Andar 0..5, Número do Nível 1..3, Nome do Andar)")]
        public UnityEvent<int, int, string> onFloorGenerated;

        [Tooltip("Disparado quando a jogadora entra numa sala. Parâmetros: (Coordenada X,Y da Sala, Tipo da Sala em Texto)")]
        public UnityEvent<Vector2Int, string> onRoomEntered;

        [Tooltip("Disparado quando a jogadora conserta um Poste Inteligente! A menina dos personagens pode ligar o menu de Power-up (Hades) aqui!")]
        public UnityEvent<GameObject> onSmartPostRepaired;

        [Tooltip("Disparado quando a jogadora pega um Item Único ou pega a Relíquia da Sala Secreta! Parâmetro: ID do Item.")]
        public UnityEvent<string> onSpecialItemAcquired;

        [Tooltip("Disparado quando a jogadora coleta uma Célula de Bateria rara dropada de uma Caixa Quebrável!")]
        public UnityEvent<float> onRareRecoveryItemCollected;

        [Tooltip("Disparado quando a jogadora acende a Luz Mestra no Topo do Farol (Fim do Nível 3 / Andar 6)!")]
        public UnityEvent onLighthouseMasterBeaconLit;

        public class MapRoomNode
        {
            public Vector2Int gridCoord;
            public Vector3 worldCenter;
            public RoomType roomType = RoomType.Combat;
            public ObstacleLayoutVariant obstacleVariant = ObstacleLayoutVariant.FourCornerPillars;
            public bool isVisited = false;
            public bool isClearedAndLit = false;
            public bool isSecretRoom = false;
            public bool isSecretRevealed = false;
            public int distanceFromStart = 0;

            public MapRoomNode mergedGiantPartner = null;
            public GameObject roomRoot;
            public List<GameObject> doorBarriers = new List<GameObject>();
            public List<StandaloneSmartPost> roomPosts = new List<StandaloneSmartPost>();
            public List<GameObject> roomMonsters = new List<GameObject>();
            public int monsterQuota = 0;
            public int monstersSpawned = 0;
            public bool doorsUnlocked = false;

            public int GetAliveMonsterCount()
            {
                int alive = 0;
                for (int i = roomMonsters.Count - 1; i >= 0; i--)
                {
                    GameObject m = roomMonsters[i];
                    if (m != null && m.activeInHierarchy)
                    {
                        alive++;
                    }
                }
                if (mergedGiantPartner != null)
                {
                    for (int i = mergedGiantPartner.roomMonsters.Count - 1; i >= 0; i--)
                    {
                        GameObject m = mergedGiantPartner.roomMonsters[i];
                        if (m != null && m.activeInHierarchy)
                        {
                            alive++;
                        }
                    }
                }
                return alive;
            }
        }

        public int currentFloorIndex = 0;

        // Se definido (modo cooperativo), a semente aleatória de cada andar: todas as jogadoras geram o mesmo mapa.
        public static Func<int, int> FloorSeedProvider;
        // Acessibilidade: no minimapa, salas ainda não visitadas aparecem só como "?".
        public static bool HideUnvisitedRoomsOnMinimap = false;
        public Vector2Int currentPlayerRoomCoord = Vector2Int.zero;
        public bool isLighthouseCompleted = false;

        private readonly Dictionary<Vector2Int, MapRoomNode> dungeonGrid = new Dictionary<Vector2Int, MapRoomNode>();
        private readonly Dictionary<string, GameObject> sharedBordersDoors = new Dictionary<string, GameObject>();
        private readonly List<GameObject> spawnedFloorObjects = new List<GameObject>();
        private readonly HashSet<Vector2Int> placedThemePillars = new HashSet<Vector2Int>();
        // Relíquias já postas em pedestais neste andar: a mesma única não aparece em duas salas
        private readonly List<RelicType> offeredRelics = new List<RelicType>();
        private LevelVisualTheme activeTheme;
        private RoomDecorKit activeDecorKit;

        private const float WallHeight = 3.4f;
        private const float WallThickness = 0.7f;
        private string activeSubtitleBanner = "";
        private string hoverPromptBanner = "";
        private float bannerShownAt = -100f;
        private const float BannerSeconds = 7f;
        private const float BannerFadeSeconds = 1f;
        private GUIStyle bannerStyle;
        private GUIStyle promptStyle;
        private GUIStyle mapTitleStyle;
        private GUIStyle legendStyle;
        private float hudStyleScale = -1f;

        private readonly List<Light> sceneDirectionalLights = new List<Light>();
        private readonly List<float> sceneDirectionalIntensities = new List<float>();
        private readonly List<Light> dimRoomLights = new List<Light>();
        private UnityEngine.Rendering.AmbientMode originalAmbientMode;
        private Color originalAmbientSky, originalAmbientEquator, originalAmbientGround;
        private float originalReflectionIntensity;
        private float roomBrightness = 1f;
        private float targetRoomBrightness = 1f;
        private bool currentRoomLit;

        public float CurrentRoomBrightness => darkRooms ? roomBrightness : 0f;

        public IReadOnlyDictionary<Vector2Int, MapRoomNode> DungeonGrid => dungeonGrid;

        private void Awake()
        {
            Instance = this;
#if UNITY_EDITOR
            AutoAssignLevelThemes();
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && AutoAssignLevelThemes())
                {
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            };
        }

        private bool AutoAssignLevelThemes()
        {
            bool changed = false;
            if (levelThemes == null) return false;
            foreach (var theme in levelThemes)
            {
                if (theme != null) changed |= theme.AutoAssignFromFolder();
            }
            if (levelDecorKits != null)
            {
                foreach (var kit in levelDecorKits)
                {
                    if (kit != null) changed |= kit.AutoAssignFromFolder();
                }
            }
            changed |= AssignDecorIfEmpty(ref decorCenterLight, "center_light");
            changed |= AssignDecorIfEmpty(ref decorMetalDoor, "metal_door");
            changed |= AssignDecorIfEmpty(ref decorLock, "lock");
            changed |= AssignDecorIfEmpty(ref decorCrate, "crate");
            changed |= AssignDecorIfEmpty(ref decorServer, "server");
            changed |= AssignDecorIfEmpty(ref decorBarrel, "barrel");
            changed |= AssignDecorIfEmpty(ref decorSolarPanel, "led_floor");
            changed |= AssignMaterialIfEmpty(ref portalSmokeMaterial, "Assets/Monsters/Generated/SmokeParticle.mat");
            changed |= AssignMaterialIfEmpty(ref portalSparkMaterial, "Assets/Monsters/Generated/Mote.mat");
            changed |= AssignMonstersIfEmpty();
            changed |= AssignPowerUpIfEmpty(ref powerUpBatteryModel, PowerUpPickup.BatteryModelName);
            changed |= AssignPowerUpIfEmpty(ref powerUpCrystalModel, PowerUpPickup.CrystalModelName);
            changed |= AssignPowerUpSoundIfEmpty(ref powerUpLightCrystalSound, PowerUpPickup.LightCrystalSoundName);
            changed |= AssignPowerUpSoundIfEmpty(ref powerUpEnergySound, PowerUpPickup.EnergySoundName);
            changed |= AssignPowerUpSoundIfEmpty(ref powerUpRetroTechSound, PowerUpPickup.RetroTechSoundName);
            changed |= AssignPowerUpSoundIfEmpty(ref powerUpSpecialSound, PowerUpPickup.SpecialSoundName);
            return changed;
        }

        private static bool AssignPowerUpIfEmpty(ref GameObject slot, string modelName)
        {
            if (slot != null) return false;
            slot = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Powerups/{modelName}.fbx");
            return slot != null;
        }

        private static bool AssignPowerUpSoundIfEmpty(ref AudioClip slot, string soundName)
        {
            if (slot != null) return false;
            slot = PowerUpPickup.LoadSoundAsset(soundName);
            return slot != null;
        }

        private bool AssignMonstersIfEmpty()
        {
            const string monsterFolder = "Assets/Sprites/Monster";
            if ((monsterModels != null && monsterModels.Length > 0) || !UnityEditor.AssetDatabase.IsValidFolder(monsterFolder)) return false;

            List<GameObject> found = new List<GameObject>();
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Model", new[] { monsterFolder }))
            {
                GameObject model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (model != null) found.Add(model);
            }
            if (found.Count == 0) return false;

            monsterModels = found.ToArray();
            return true;
        }

        private static bool AssignDecorIfEmpty(ref GameObject slot, string propName)
        {
            if (slot != null) return false;
            slot = RoomDecorKit.LoadDecor(propName);
            return slot != null;
        }

        private static bool AssignMaterialIfEmpty(ref Material slot, string path)
        {
            if (slot != null) return false;
            slot = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            return slot != null;
        }
#endif

        private void Start()
        {
            EnsurePlayerExistsOrFind();
            ApplyDarkAmbience();
            GenerateFloor(0);
            if (GetComponent<EnemySpawner>() == null) gameObject.AddComponent<EnemySpawner>();
            if (GetComponent<MonsterDirector>() == null) gameObject.AddComponent<MonsterDirector>();
        }

        private void ApplyDarkAmbience()
        {
            if (!darkRooms) return;

            originalAmbientMode = RenderSettings.ambientMode;
            originalAmbientSky = RenderSettings.ambientSkyColor;
            originalAmbientEquator = RenderSettings.ambientEquatorColor;
            originalAmbientGround = RenderSettings.ambientGroundColor;
            originalReflectionIntensity = RenderSettings.reflectionIntensity;

            foreach (Light l in FindObjectsByType<Light>())
            {
                if (l.type != LightType.Directional) continue;
                sceneDirectionalLights.Add(l);
                sceneDirectionalIntensities.Add(l.intensity);
            }

            roomBrightness = targetRoomBrightness = 0f;
            ApplyRoomBrightness(0f);
        }

        private void SetRoomLit(bool lit)
        {
            if (!darkRooms) return;
            currentRoomLit = lit;
            targetRoomBrightness = lit ? 1f : 0f;
        }

        public int GetRoomPostCount(MapRoomNode room)
        {
            if (room == null) return 0;
            return room.roomPosts.Count + (room.mergedGiantPartner != null ? room.mergedGiantPartner.roomPosts.Count : 0);
        }

        public bool RoomAttractsMonsters(MapRoomNode room)
        {
            return room != null && (GetRoomPostCount(room) == 0 || !room.isClearedAndLit);
        }

        private float GetRoomPostBrightness(MapRoomNode room)
        {
            float best = -1f;
            foreach (var post in room.roomPosts)
            {
                if (post != null && post.isRepaired) best = Mathf.Max(best, post.SmartBrightness);
            }
            if (room.mergedGiantPartner != null)
            {
                foreach (var post in room.mergedGiantPartner.roomPosts)
                {
                    if (post != null && post.isRepaired) best = Mathf.Max(best, post.SmartBrightness);
                }
            }
            return best < 0f ? 1f : best;
        }

        private void UpdateRoomBrightness()
        {
            if (!darkRooms) return;

            if (currentRoomLit && dungeonGrid.TryGetValue(currentPlayerRoomCoord, out MapRoomNode room))
            {
                targetRoomBrightness = GetRoomPostBrightness(room);
            }
            if (Mathf.Approximately(roomBrightness, targetRoomBrightness)) return;

            float step = lightTransitionSeconds > 0f ? Time.deltaTime / lightTransitionSeconds : 1f;
            roomBrightness = Mathf.MoveTowards(roomBrightness, targetRoomBrightness, step);
            ApplyRoomBrightness(roomBrightness);
        }

        private void ApplyRoomBrightness(float t)
        {
            if (t >= 1f)
            {
                RenderSettings.ambientMode = originalAmbientMode;
                DynamicGI.UpdateEnvironment();
            }
            else
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = Color.Lerp(darkAmbientColor, originalAmbientSky, t);
                RenderSettings.ambientEquatorColor = Color.Lerp(darkAmbientColor, originalAmbientEquator, t);
                RenderSettings.ambientGroundColor = Color.Lerp(darkAmbientColor, originalAmbientGround, t);
            }
            RenderSettings.reflectionIntensity = Mathf.Lerp(0.1f, originalReflectionIntensity, t);

            for (int i = 0; i < sceneDirectionalLights.Count; i++)
            {
                Light l = sceneDirectionalLights[i];
                if (l == null) continue;
                l.intensity = sceneDirectionalIntensities[i] * t;
                l.enabled = t > 0f;
            }

            dungeonGrid.TryGetValue(currentPlayerRoomCoord, out MapRoomNode current);
            foreach (Light l in dimRoomLights)
            {
                if (l == null) continue;
                l.intensity = dimLightIntensity * (1f - t);
                l.enabled = t < 1f && current != null && IsPositionInsideRoomOrPartner(l.transform.position, current);
            }
        }

        private void SpawnDimRoomLights(MapRoomNode node)
        {
            if (!darkRooms || dimLightsPerRoom <= 0) return;

            float ix = roomWidthX * 0.5f - 3f;
            float iz = roomLengthZ * 0.5f - 3f;
            Vector3[] spots =
            {
                new Vector3(-ix, 2.6f, iz), new Vector3(ix, 2.6f, -iz),
                new Vector3(ix, 2.6f, iz), new Vector3(-ix, 2.6f, -iz)
            };

            for (int i = 0; i < dimLightsPerRoom; i++)
            {
                GameObject lObj = new GameObject("Luz_Fraca");
                lObj.transform.SetParent(node.roomRoot.transform);
                lObj.transform.localPosition = spots[i];
                Light l = lObj.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = dimLightRange;
                l.intensity = dimLightIntensity * (1f - roomBrightness);
                l.enabled = roomBrightness < 1f;
                l.color = dimLightColor;
                l.shadows = LightShadows.None;
                dimRoomLights.Add(l);
                LightField.Ignore(l);
            }
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                EnsurePlayerExistsOrFind();
                if (playerTransform == null) return;
            }

            UpdatePlayerRoomTracking(playerTransform.position);
            UpdateRoomCompletion();
            UpdateRoomBrightness();
        }

        public void SetPlayer(Transform newPlayer)
        {
            playerTransform = newPlayer;
        }

        public void RegisterCompanion(Transform allyTransform)
        {
            if (allyTransform != null && !companionTransforms.Contains(allyTransform))
            {
                companionTransforms.Add(allyTransform);
                EnsureAllCompanionsInsideCurrentRoom();
            }
        }

        public void RegisterRoomMonster(MapRoomNode room, GameObject monsterObj)
        {
            if (room == null || monsterObj == null) return;
            if (room.roomMonsters.Contains(monsterObj)) return;
            room.roomMonsters.Add(monsterObj);
            room.monstersSpawned++;
        }

        public void NotifyMonsterKilled(GameObject monsterObj)
        {
            if (monsterObj == null) return;
            foreach (var kvp in dungeonGrid)
            {
                kvp.Value.roomMonsters.Remove(monsterObj);
            }
        }

        public static bool TryHitMapBreakable(Collider hitCollider, float damage = 1f)
        {
            if (hitCollider == null) return false;

            StandaloneSecretCrackedWall secretWall = hitCollider.GetComponentInParent<StandaloneSecretCrackedWall>();
            if (secretWall != null)
            {
                secretWall.RegisterProjectileHit();
                return true;
            }

            StandaloneBreakableCrate crate = hitCollider.GetComponentInParent<StandaloneBreakableCrate>();
            if (crate != null)
            {
                crate.TakeHit();
                return true;
            }

            return false;
        }

        public void SetHoverPrompt(string prompt)
        {
            hoverPromptBanner = prompt;
        }

        public void ShowBannerMessage(string message)
        {
            activeSubtitleBanner = message;
            bannerShownAt = Time.unscaledTime;
        }

        private void EnsurePlayerExistsOrFind()
        {
            if (playerTransform != null) return;

            GameObject tagged = GameObject.FindWithTag("Player");
            if (tagged != null)
            {
                playerTransform = tagged.transform;
                return;
            }

            GameObject named = GameObject.Find("Jogadora_Lider") ?? GameObject.Find("Player");
            if (named != null)
            {
                playerTransform = named.transform;
                return;
            }

            if (spawnFallbackTestPlayerIfNoneFound)
            {
                GameObject testPlayer = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                testPlayer.name = "Jogadora_Teste_MapaStandalone";
                testPlayer.transform.position = new Vector3(0f, 1f, 0f);
                Renderer r = testPlayer.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(0.95f, 0.35f, 0.9f);

                Light pLight = testPlayer.AddComponent<Light>();
                pLight.type = LightType.Point;
                pLight.range = 14f;
                pLight.intensity = 2.8f;
                pLight.color = new Color(1f, 0.95f, 0.8f);

                testPlayer.AddComponent<StandaloneFallbackPlayerTester>();
                playerTransform = testPlayer.transform;
            }
        }

        public void GenerateFloor(int floorIdx)
        {
            if (floorIdx < 0 || floorIdx >= floors.Length) return;
            currentFloorIndex = floorIdx;

            foreach (var obj in spawnedFloorObjects)
            {
                if (obj != null) Destroy(obj);
            }
            spawnedFloorObjects.Clear();
            dungeonGrid.Clear();
            sharedBordersDoors.Clear();
            dimRoomLights.Clear();
            offeredRelics.Clear();

            if (FloorSeedProvider != null) UnityEngine.Random.InitState(FloorSeedProvider(floorIdx));

            FloorStageConfig cfg = floors[floorIdx];
            activeTheme = GetThemeForLevel(cfg.levelNumber);
            int decorIdx = cfg.levelNumber - 1;
            activeDecorKit = levelDecorKits != null && decorIdx >= 0 && decorIdx < levelDecorKits.Length ? levelDecorKits[decorIdx] : null;
            placedThemePillars.Clear();
            int targetNormalRooms = UnityEngine.Random.Range(cfg.minRooms, cfg.maxRooms + 1);

            MapRoomNode startNode = new MapRoomNode
            {
                gridCoord = Vector2Int.zero,
                worldCenter = Vector3.zero,
                roomType = RoomType.Start,
                obstacleVariant = ObstacleLayoutVariant.None,
                isVisited = true,
                distanceFromStart = 0
            };
            dungeonGrid[Vector2Int.zero] = startNode;

            List<Vector2Int> coordsList = new List<Vector2Int> { Vector2Int.zero };
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            int safety = 0;
            while (dungeonGrid.Count < targetNormalRooms && safety < 600)
            {
                safety++;
                Vector2Int fromCoord = coordsList[UnityEngine.Random.Range(0, coordsList.Count)];
                Vector2Int dir = dirs[UnityEngine.Random.Range(0, dirs.Length)];
                Vector2Int candidate = fromCoord + dir;

                if (!dungeonGrid.ContainsKey(candidate))
                {
                    MapRoomNode newNode = new MapRoomNode
                    {
                        gridCoord = candidate,
                        worldCenter = new Vector3(candidate.x * roomWidthX, 0f, candidate.y * roomLengthZ),
                        roomType = RoomType.Combat,
                        obstacleVariant = PickObstacleVariantForLevel(cfg.levelNumber),
                        distanceFromStart = dungeonGrid[fromCoord].distanceFromStart + 1
                    };
                    dungeonGrid[candidate] = newNode;
                    coordsList.Add(candidate);
                }
            }

            MapRoomNode furthestNode = startNode;
            foreach (var kvp in dungeonGrid)
            {
                if (kvp.Value.distanceFromStart > furthestNode.distanceFromStart)
                {
                    furthestNode = kvp.Value;
                }
            }
            furthestNode.roomType = RoomType.BossExit;
            furthestNode.obstacleVariant = PickObstacleVariantForLevel(cfg.levelNumber);

            List<MapRoomNode> candidates = new List<MapRoomNode>();
            foreach (var kvp in dungeonGrid)
            {
                if (kvp.Value != startNode && kvp.Value != furthestNode)
                {
                    candidates.Add(kvp.Value);
                }
            }

            if (candidates.Count > 0)
            {
                int idx = UnityEngine.Random.Range(0, candidates.Count);
                candidates[idx].roomType = RoomType.TreasureChoice;
                candidates[idx].obstacleVariant = ObstacleLayoutVariant.None;
                candidates[idx].isClearedAndLit = true;
                candidates.RemoveAt(idx);
            }

            AssignGiantHalls(cfg.giantHallPairsCount, dirs);

            SpawnSecretRoomNodes(cfg.secretRoomsCount, dirs);

            foreach (var kvp in dungeonGrid)
            {
                BuildRoomGeometryAndContents(kvp.Value, cfg);
            }

            currentPlayerRoomCoord = Vector2Int.zero;
            if (playerTransform != null)
            {
                playerTransform.position = new Vector3(0f, 1f, -3.5f);
            }
            EnterRoom(startNode);

            ShowBannerMessage($"🗼 {cfg.floorTitle} ({dungeonGrid.Count} Salas) — Conserte o Poste Inicial [E] e derrote os monstros para abrir as portas!");
            onFloorGenerated?.Invoke(currentFloorIndex, cfg.levelNumber, cfg.floorTitle);
        }

        private LevelVisualTheme GetThemeForLevel(int levelNumber)
        {
            int idx = levelNumber - 1;
            if (levelThemes == null || idx < 0 || idx >= levelThemes.Length) return null;
            LevelVisualTheme theme = levelThemes[idx];
            return theme != null && theme.IsUsable ? theme : null;
        }

        private ObstacleLayoutVariant PickObstacleVariantForLevel(int levelNumber)
        {
            ObstacleLayoutVariant levelUnique = ObstacleLayoutVariant.Level1_PierContainers;
            if (levelNumber == 2) levelUnique = ObstacleLayoutVariant.Level2_SolarPanelsArray;
            else if (levelNumber >= 3) levelUnique = ObstacleLayoutVariant.Level3_SmartGridServerRacks;

            if (UnityEngine.Random.value < 0.45f)
                return levelUnique;

            int r = UnityEngine.Random.Range(0, 3);
            if (r == 0) return ObstacleLayoutVariant.FourCornerPillars;
            if (r == 1) return ObstacleLayoutVariant.TwinLBunkers;
            return ObstacleLayoutVariant.CentralCrossCorridors;
        }

        private void AssignGiantHalls(int desiredPairs, Vector2Int[] dirs)
        {
            int created = 0;
            foreach (var kvp in dungeonGrid)
            {
                if (created >= desiredPairs) break;
                MapRoomNode a = kvp.Value;
                if (a.roomType != RoomType.Combat || a.mergedGiantPartner != null) continue;

                foreach (var d in dirs)
                {
                    if (dungeonGrid.TryGetValue(a.gridCoord + d, out MapRoomNode b))
                    {
                        if (b.roomType == RoomType.Combat && b.mergedGiantPartner == null)
                        {
                            a.roomType = RoomType.GiantHall;
                            b.roomType = RoomType.GiantHall;
                            a.mergedGiantPartner = b;
                            b.mergedGiantPartner = a;
                            created++;
                            break;
                        }
                    }
                }
            }
        }

        private void SpawnSecretRoomNodes(int count, Vector2Int[] dirs)
        {
            List<Vector2Int> emptyNeighbors = new List<Vector2Int>();
            foreach (var kvp in dungeonGrid)
            {
                if (kvp.Value.roomType == RoomType.Start || kvp.Value.roomType == RoomType.BossExit)
                    continue;

                foreach (var d in dirs)
                {
                    Vector2Int n = kvp.Key + d;
                    if (!dungeonGrid.ContainsKey(n) && !emptyNeighbors.Contains(n))
                    {
                        emptyNeighbors.Add(n);
                    }
                }
            }

            int spawned = 0;
            while (spawned < count && emptyNeighbors.Count > 0)
            {
                int pick = UnityEngine.Random.Range(0, emptyNeighbors.Count);
                Vector2Int secretCoord = emptyNeighbors[pick];
                emptyNeighbors.RemoveAt(pick);

                MapRoomNode secretNode = new MapRoomNode
                {
                    gridCoord = secretCoord,
                    worldCenter = new Vector3(secretCoord.x * roomWidthX, 0f, secretCoord.y * roomLengthZ),
                    roomType = RoomType.Secret,
                    obstacleVariant = ObstacleLayoutVariant.None,
                    isSecretRoom = true,
                    isSecretRevealed = false,
                    isClearedAndLit = true
                };
                dungeonGrid[secretCoord] = secretNode;
                spawned++;
            }
        }

        private void BuildRoomGeometryAndContents(MapRoomNode node, FloorStageConfig cfg)
        {
            GameObject root = new GameObject($"Sala_{node.gridCoord.x}_{node.gridCoord.y}_{node.roomType}_{node.obstacleVariant}");
            root.transform.SetParent(transform);
            root.transform.position = node.worldCenter;
            node.roomRoot = root;
            spawnedFloorObjects.Add(root);

            if (activeTheme != null)
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Asfalto_Sala";
                floor.transform.SetParent(root.transform);
                floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
                floor.transform.localScale = new Vector3(roomWidthX, 1f, roomLengthZ);
                floor.GetComponent<Renderer>().enabled = false;

                bool specialRoom = node.roomType == RoomType.BossExit || node.roomType == RoomType.TreasureChoice || node.roomType == RoomType.Secret;
                activeTheme.BuildFloor(root.transform, roomWidthX, roomLengthZ, specialRoom);
            }
            else if (customFloorPrefab != null)
            {
                GameObject f = Instantiate(customFloorPrefab, node.worldCenter + new Vector3(0f, -0.5f, 0f), Quaternion.identity, root.transform);
                f.name = "Asfalto_Sala";
            }
            else
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Asfalto_Sala";
                floor.transform.SetParent(root.transform);
                floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
                floor.transform.localScale = new Vector3(roomWidthX, 1f, roomLengthZ);
                Renderer fr = floor.GetComponent<Renderer>();
                if (fr != null)
                {
                    fr.material.color = node.roomType == RoomType.BossExit ? new Color(0.25f, 0.14f, 0.28f)
                                      : node.roomType == RoomType.TreasureChoice ? new Color(0.26f, 0.22f, 0.12f)
                                      : node.roomType == RoomType.Secret ? new Color(0.18f, 0.12f, 0.26f)
                                      : cfg.floorColor;
                }
            }

            BuildWallOrDoor(node, Vector2Int.up, cfg.wallColor);
            BuildWallOrDoor(node, Vector2Int.down, cfg.wallColor);
            BuildWallOrDoor(node, Vector2Int.right, cfg.wallColor);
            BuildWallOrDoor(node, Vector2Int.left, cfg.wallColor);
            PlaceThemePillarsAtRoomCorners(node);

            SpawnBreakableCrates(node, cfg.breakableCratesPerRoom);

            switch (node.roomType)
            {
                case RoomType.Start:
                    node.roomPosts.Add(SpawnSmartPost(node.worldCenter + new Vector3(0f, 0f, 2f), node, false));
                    break;

                case RoomType.Combat:
                case RoomType.GiantHall:
                    BuildRoomObstacles(node, cfg.wallColor);
                    node.roomPosts.Add(SpawnSmartPost(node.worldCenter, node, false));
                    SpawnRoomMonsters(node, cfg.monstersPerRoom, false);
                    break;

                case RoomType.BossExit:
                    BuildRoomObstacles(node, cfg.wallColor);
                    bool isTopOfLighthouse = (currentFloorIndex == floors.Length - 1);
                    node.roomPosts.Add(SpawnSmartPost(node.worldCenter, node, isTopOfLighthouse));
                    SpawnRoomMonsters(node, cfg.monstersPerRoom + 2, true);
                    break;

                case RoomType.TreasureChoice:
                    BuildTreasureRoomPedestals(node);
                    break;

                case RoomType.Secret:
                    BuildSecretRoomPedestal(node);
                    break;
            }

            SpawnDimRoomLights(node);

            if (activeDecorKit != null)
            {
                bool[] doorSides =
                {
                    dungeonGrid.ContainsKey(node.gridCoord + Vector2Int.up), dungeonGrid.ContainsKey(node.gridCoord + Vector2Int.down),
                    dungeonGrid.ContainsKey(node.gridCoord + Vector2Int.right), dungeonGrid.ContainsKey(node.gridCoord + Vector2Int.left)
                };
                activeDecorKit.Decorate(node.roomRoot.transform, node.worldCenter, roomWidthX, roomLengthZ, WallThickness,
                    doorSides, doorOpeningWidth * 0.5f + 1.5f);
            }

            if (node.roomPosts.Count > 0)
            {
                int maxQuota = Mathf.Min(maxMonstersPerRoom, MonsterQuotaCap);
                int minQuota = Mathf.Clamp(minMonstersPerRoom, 1, maxQuota);
                node.monstersSpawned = node.roomMonsters.Count;
                node.monsterQuota = Mathf.Max(UnityEngine.Random.Range(minQuota, maxQuota + 1), node.monstersSpawned);
            }
            else
            {
                int maxQuota = Mathf.Min(maxMonstersInPostlessRoom, MonsterQuotaCap);
                node.monsterQuota = UnityEngine.Random.Range(Mathf.Clamp(minMonstersInPostlessRoom, 0, maxQuota), maxQuota + 1);
            }
        }

        public bool IsRoomComplete(MapRoomNode room)
        {
            return room.isClearedAndLit && room.GetAliveMonsterCount() == 0;
        }

        private void UpdateRoomCompletion()
        {
            if (!dungeonGrid.TryGetValue(currentPlayerRoomCoord, out MapRoomNode room)) return;
            if (room.doorsUnlocked || !IsRoomComplete(room)) return;

            room.doorsUnlocked = true;
            SetRoomDoorsOpen(room, true);
            if (room.mergedGiantPartner != null)
            {
                room.mergedGiantPartner.doorsUnlocked = true;
                SetRoomDoorsOpen(room.mergedGiantPartner, true);
            }

            if (room.roomType == RoomType.BossExit)
            {
                OpenFloorExit(room);
            }
            else
            {
                ShowBannerMessage("Poste consertado e monstros derrotados! As portas desta sala se abriram!");
            }
        }

        // Derrotar o chefão abre a escada para o próximo andar; no último andar, é a vitória
        private void OpenFloorExit(MapRoomNode bossRoom)
        {
            if (currentFloorIndex < floors.Length - 1)
            {
                SpawnLighthouseStaircasePortal(bossRoom.worldCenter + new Vector3(0f, 0f, 5.0f));
                ShowBannerMessage("Chefão derrotado! A escadaria do farol se abriu: entre no portal ciano ao norte para subir ao próximo andar.");
            }
            else
            {
                isLighthouseCompleted = true;
                ShowBannerMessage("TOPO DO FAROL ACESO! O Feixe Mestre iluminou toda a costa!");
                onLighthouseMasterBeaconLit?.Invoke();
                if (!CoopSession.IsCoop)
                {
                    GameResult.Show(GameResult.Outcome.Victory, "Você derrotou os chefões de todos os andares e acendeu o topo do farol!", 3f);
                }
            }
        }

        private void BuildRoomObstacles(MapRoomNode node, Color wallColor)
        {
            Transform parent = node.roomRoot.transform;
            switch (node.obstacleVariant)
            {
                case ObstacleLayoutVariant.FourCornerPillars:
                    Vector3[] corners =
                    {
                        new Vector3(-7.5f, 1.4f, 5.5f), new Vector3(7.5f, 1.4f, 5.5f),
                        new Vector3(-7.5f, 1.4f, -5.5f), new Vector3(7.5f, 1.4f, -5.5f)
                    };
                    foreach (var pos in corners)
                        CreateBlock(parent, "Pilar_Cobertura", pos, new Vector3(2.3f, 2.8f, 2.3f), wallColor * 0.85f, decorServer);
                    break;

                case ObstacleLayoutVariant.TwinLBunkers:
                    Color c = wallColor * 0.82f;
                    CreateBlock(parent, "Pilar_Bunker_Esq1", new Vector3(-7.5f, 1.2f, 0f), new Vector3(1.5f, 2.4f, 6.5f), c, decorCrate);
                    CreateBlock(parent, "Pilar_Bunker_Esq2", new Vector3(-5.2f, 1.2f, 3.0f), new Vector3(3.5f, 2.4f, 1.5f), c, decorCrate);
                    CreateBlock(parent, "Pilar_Bunker_Dir1", new Vector3(7.5f, 1.2f, 0f), new Vector3(1.5f, 2.4f, 6.5f), c, decorCrate);
                    CreateBlock(parent, "Pilar_Bunker_Dir2", new Vector3(5.2f, 1.2f, -3.0f), new Vector3(3.5f, 2.4f, 1.5f), c, decorCrate);
                    break;

                case ObstacleLayoutVariant.CentralCrossCorridors:
                    Color cw = wallColor * 0.88f;
                    CreateBlock(parent, "Pilar_Mureta_NO", new Vector3(-6.0f, 1.1f, 4.5f), new Vector3(5.0f, 2.2f, 1.3f), cw, decorCrate);
                    CreateBlock(parent, "Pilar_Mureta_NE", new Vector3(6.0f, 1.1f, 4.5f), new Vector3(5.0f, 2.2f, 1.3f), cw, decorCrate);
                    CreateBlock(parent, "Pilar_Mureta_SO", new Vector3(-6.0f, 1.1f, -4.5f), new Vector3(5.0f, 2.2f, 1.3f), cw, decorCrate);
                    CreateBlock(parent, "Pilar_Mureta_SE", new Vector3(6.0f, 1.1f, -4.5f), new Vector3(5.0f, 2.2f, 1.3f), cw, decorCrate);
                    break;

                case ObstacleLayoutVariant.Level1_PierContainers:
                    CreateBlock(parent, "Pilar_Container_Maritimo_1", new Vector3(-8.2f, 1.35f, 4.8f), new Vector3(5.5f, 2.7f, 2.4f), new Color(0.18f, 0.35f, 0.55f), decorCrate);
                    CreateBlock(parent, "Pilar_Container_Maritimo_2", new Vector3(8.2f, 1.35f, -4.8f), new Vector3(5.5f, 2.7f, 2.4f), new Color(0.55f, 0.25f, 0.18f), decorCrate);
                    CreateCylinder(parent, "Pilar_Amarracao_1", new Vector3(7.0f, 0.9f, 5.0f), new Vector3(1.6f, 0.9f, 1.6f), new Color(0.25f, 0.25f, 0.28f), decorBarrel);
                    CreateCylinder(parent, "Pilar_Amarracao_2", new Vector3(-7.0f, 0.9f, -5.0f), new Vector3(1.6f, 0.9f, 1.6f), new Color(0.25f, 0.25f, 0.28f), decorBarrel);
                    break;

                case ObstacleLayoutVariant.Level2_SolarPanelsArray:
                    Vector3[] panels =
                    {
                        new Vector3(-8.0f, 1.2f, 5.2f), new Vector3(8.0f, 1.2f, 5.2f),
                        new Vector3(-8.0f, 1.2f, -5.2f), new Vector3(8.0f, 1.2f, -5.2f)
                    };
                    foreach (var p in panels)
                    {
                        GameObject panel = CreateBlock(parent, "Pilar_Painel_Solar", p, new Vector3(4.2f, 0.35f, 2.6f), new Color(0.12f, 0.45f, 0.78f), decorSolarPanel);
                        panel.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
                    }
                    CreateCylinder(parent, "Pilar_Capacitor_1", new Vector3(-4.5f, 1.1f, 0f), new Vector3(1.3f, 1.1f, 1.3f), new Color(0.25f, 0.85f, 0.55f), decorBarrel);
                    CreateCylinder(parent, "Pilar_Capacitor_2", new Vector3(4.5f, 1.1f, 0f), new Vector3(1.3f, 1.1f, 1.3f), new Color(0.25f, 0.85f, 0.55f), decorBarrel);
                    break;

                case ObstacleLayoutVariant.Level3_SmartGridServerRacks:
                    Vector3[] racks =
                    {
                        new Vector3(-8.5f, 1.6f, 4.2f), new Vector3(-8.5f, 1.6f, -4.2f),
                        new Vector3(8.5f, 1.6f, 4.2f), new Vector3(8.5f, 1.6f, -4.2f)
                    };
                    foreach (var rp in racks)
                        CreateBlock(parent, "Pilar_Rack_SmartGrid", rp, new Vector3(1.8f, 3.2f, 3.4f), new Color(0.18f, 0.14f, 0.28f), decorServer);
                    break;
            }
        }

        private GameObject CreateBlock(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color, GameObject model = null)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.localPosition = localPos;
            obj.transform.localScale = scale;
            Renderer r = obj.GetComponent<Renderer>();
            if (r != null) r.material.color = color;
            DressPrimitive(obj, model);
            return obj;
        }

        // Esconde o cubo/cilindro (fica só o colisor) e encaixa o modelo no mesmo volume
        public static void DressPrimitive(GameObject primitive, GameObject model)
        {
            if (model == null) return;

            Renderer r = primitive.GetComponent<Renderer>();
            if (r != null) r.enabled = false;

            MeshFilter filter = primitive.GetComponent<MeshFilter>();
            Vector3 volume = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.size : Vector3.one;
            Bounds bounds = RoomDecorKit.GetLocalBounds(model);

            GameObject wrapper = new GameObject($"Modelo_{model.name}");
            wrapper.transform.SetParent(primitive.transform, false);
            wrapper.transform.localScale = new Vector3(volume.x / Mathf.Max(bounds.size.x, 0.01f),
                volume.y / Mathf.Max(bounds.size.y, 0.01f), volume.z / Mathf.Max(bounds.size.z, 0.01f));

            GameObject instance = Instantiate(model, wrapper.transform, false);
            instance.transform.localPosition = model.transform.localPosition - bounds.center;
            foreach (Camera camera in instance.GetComponentsInChildren<Camera>(true)) Destroy(camera.gameObject);
            foreach (Light light in instance.GetComponentsInChildren<Light>(true)) Destroy(light.gameObject);
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) Destroy(collider);
        }

        private GameObject CreateWallBlock(Transform parent, Vector2Int dir, Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject wall = CreateBlock(parent, "Parede_Sala", localPos, scale, color);
            if (activeTheme == null) return wall;

            wall.GetComponent<Renderer>().enabled = false;
            bool alongX = scale.x >= scale.z;
            Vector3 inward = new Vector3(-dir.x, 0f, -dir.y);
            activeTheme.BuildWall(parent, new Vector3(localPos.x, 0f, localPos.z), alongX ? scale.x : scale.z, alongX, inward,
                scale.y, alongX ? scale.z : scale.x);
            return wall;
        }

        private void PlaceThemePillarsAtRoomCorners(MapRoomNode node)
        {
            if (activeTheme == null || activeTheme.post == null) return;

            float hx = roomWidthX * 0.5f;
            float hz = roomLengthZ * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz),
                new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz)
            };

            foreach (var offset in corners)
            {
                Vector3 world = node.worldCenter + offset;
                Vector2Int key = new Vector2Int(Mathf.RoundToInt(world.x * 10f), Mathf.RoundToInt(world.z * 10f));
                if (!placedThemePillars.Add(key)) continue;

                activeTheme.PlacePost(node.roomRoot.transform, world, WallHeight, WallThickness);
            }
        }

        private GameObject CreateCylinder(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color, GameObject model = null)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.localPosition = localPos;
            obj.transform.localScale = scale;
            Renderer r = obj.GetComponent<Renderer>();
            if (r != null) r.material.color = color;
            DressPrimitive(obj, model);
            return obj;
        }

        private string GetSharedBorderKey(Vector2Int a, Vector2Int b)
        {
            if (a.x < b.x || (a.x == b.x && a.y < b.y))
                return $"{a.x},{a.y}_{b.x},{b.y}";
            return $"{b.x},{b.y}_{a.x},{a.y}";
        }

        private void BuildWallOrDoor(MapRoomNode node, Vector2Int dir, Color wallColor)
        {
            Vector2Int neighborCoord = node.gridCoord + dir;
            bool hasNeighbor = dungeonGrid.TryGetValue(neighborCoord, out MapRoomNode neighborNode);

            if (hasNeighbor && node.mergedGiantPartner == neighborNode)
                return;

            float wallHeight = WallHeight;
            float wallThickness = WallThickness;
            bool isCanonicalOwner = (node.gridCoord.x < neighborCoord.x) ||
                                    (node.gridCoord.x == neighborCoord.x && node.gridCoord.y < neighborCoord.y);
            bool isSecretBorder = hasNeighbor && (node.isSecretRoom || neighborNode.isSecretRoom);

            if (dir.y != 0)
            {
                float zPos = dir.y * (roomLengthZ * 0.5f);
                if (!hasNeighbor)
                {
                    CreateWallBlock(node.roomRoot.transform, dir,new Vector3(0f, wallHeight * 0.5f, zPos),
                        new Vector3(roomWidthX, wallHeight, wallThickness), wallColor);
                }
                else
                {
                    if (isCanonicalOwner)
                    {
                        float sideWidth = (roomWidthX - doorOpeningWidth) * 0.5f;
                        float sideOffset = (doorOpeningWidth * 0.5f) + (sideWidth * 0.5f);
                        CreateWallBlock(node.roomRoot.transform, dir,new Vector3(-sideOffset, wallHeight * 0.5f, zPos),
                            new Vector3(sideWidth, wallHeight, wallThickness), wallColor);
                        CreateWallBlock(node.roomRoot.transform, dir,new Vector3(sideOffset, wallHeight * 0.5f, zPos),
                            new Vector3(sideWidth, wallHeight, wallThickness), wallColor);
                    }

                    if (isSecretBorder)
                    {
                        MapRoomNode secretRef = node.isSecretRoom ? node : neighborNode;
                        GetOrCreateSecretWall(node.gridCoord, neighborCoord,
                            node.worldCenter + new Vector3(0f, wallHeight * 0.5f, zPos),
                            new Vector3(doorOpeningWidth, wallHeight, wallThickness), wallColor, secretRef);
                    }
                    else
                    {
                        GameObject door = GetOrCreateSharedDoor(node.gridCoord, neighborCoord,
                            node.worldCenter + new Vector3(0f, wallHeight * 0.45f, zPos),
                            new Vector3(doorOpeningWidth, wallHeight * 0.9f, wallThickness * 0.85f));
                        node.doorBarriers.Add(door);
                    }
                }
            }
            else if (dir.x != 0)
            {
                float xPos = dir.x * (roomWidthX * 0.5f);
                if (!hasNeighbor)
                {
                    CreateWallBlock(node.roomRoot.transform, dir,new Vector3(xPos, wallHeight * 0.5f, 0f),
                        new Vector3(wallThickness, wallHeight, roomLengthZ), wallColor);
                }
                else
                {
                    if (isCanonicalOwner)
                    {
                        float sideLength = (roomLengthZ - doorOpeningWidth) * 0.5f;
                        float sideOffset = (doorOpeningWidth * 0.5f) + (sideLength * 0.5f);
                        CreateWallBlock(node.roomRoot.transform, dir,new Vector3(xPos, wallHeight * 0.5f, -sideOffset),
                            new Vector3(wallThickness, wallHeight, sideLength), wallColor);
                        CreateWallBlock(node.roomRoot.transform, dir,new Vector3(xPos, wallHeight * 0.5f, sideOffset),
                            new Vector3(wallThickness, wallHeight, sideLength), wallColor);
                    }

                    if (isSecretBorder)
                    {
                        MapRoomNode secretRef = node.isSecretRoom ? node : neighborNode;
                        GetOrCreateSecretWall(node.gridCoord, neighborCoord,
                            node.worldCenter + new Vector3(xPos, wallHeight * 0.5f, 0f),
                            new Vector3(wallThickness, wallHeight, doorOpeningWidth), wallColor, secretRef);
                    }
                    else
                    {
                        GameObject door = GetOrCreateSharedDoor(node.gridCoord, neighborCoord,
                            node.worldCenter + new Vector3(xPos, wallHeight * 0.45f, 0f),
                            new Vector3(wallThickness * 0.85f, wallHeight * 0.9f, doorOpeningWidth));
                        node.doorBarriers.Add(door);
                    }
                }
            }
        }

        private GameObject GetOrCreateSharedDoor(Vector2Int a, Vector2Int b, Vector3 worldPos, Vector3 scale)
        {
            string key = GetSharedBorderKey(a, b);
            if (sharedBordersDoors.TryGetValue(key, out GameObject existing) && existing != null)
                return existing;

            GameObject door;
            if (customDoorBarrierPrefab != null)
            {
                door = Instantiate(customDoorBarrierPrefab, worldPos, Quaternion.identity, transform);
            }
            else
            {
                door = GameObject.CreatePrimitive(PrimitiveType.Cube);
                door.transform.SetParent(transform);
                door.transform.position = worldPos;
                door.transform.localScale = scale;
                Renderer r = door.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(0.9f, 0.15f, 0.25f);
            }
            door.name = $"Porta_Compartilhada_{key}";
            sharedBordersDoors[key] = door;
            spawnedFloorObjects.Add(door);

            if (activeTheme != null && activeTheme.wallDoor != null)
            {
                bool alongX = scale.x >= scale.z;
                GameObject frame = activeTheme.BuildDoorFrame(transform, new Vector3(worldPos.x, 0f, worldPos.z),
                    alongX ? scale.x : scale.z, alongX, WallHeight, WallThickness, out float openingWidth);
                frame.name = $"Moldura_Porta_{key}";
                spawnedFloorObjects.Add(frame);

                if (customDoorBarrierPrefab == null)
                {
                    Vector3 barrierScale = door.transform.localScale;
                    if (alongX) barrierScale.x = openingWidth;
                    else barrierScale.z = openingWidth;
                    door.transform.localScale = barrierScale;
                }
            }

            if (customDoorBarrierPrefab == null && decorMetalDoor != null)
            {
                DecorateDoorBarrier(door);
            }
            return door;
        }

        private void DecorateDoorBarrier(GameObject door)
        {
            Vector3 size = door.transform.localScale;
            bool alongX = size.x >= size.z;
            float width = alongX ? size.x : size.z;
            Quaternion facing = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            Vector3 across = alongX ? Vector3.right : Vector3.forward;
            Vector3 normal = alongX ? Vector3.forward : Vector3.right;
            Vector3 basePos = door.transform.position - Vector3.up * size.y * 0.5f;

            door.GetComponent<Renderer>().enabled = false;

            Bounds doorBounds = RoomDecorKit.GetLocalBounds(decorMetalDoor);
            Vector3 leafScale = new Vector3(width * 0.5f / doorBounds.size.x, size.y / doorBounds.size.y, 1.5f);
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject leaf = RoomDecorKit.SpawnWrapped(decorMetalDoor, transform, basePos + across * side * width * 0.25f,
                    facing * Quaternion.Euler(0f, side < 0f ? 0f : 180f, 0f), leafScale, false, "Folha_Porta");
                leaf.transform.SetParent(door.transform, true);
            }

            if (decorLock == null) return;
            Bounds lockBounds = RoomDecorKit.GetLocalBounds(decorLock);
            float lockScale = 0.9f / Mathf.Max(lockBounds.size.y, 0.01f);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 lockPos = basePos + Vector3.up * size.y * 0.35f
                    + normal * side * (Mathf.Min(size.x, size.z) * 0.5f + lockBounds.size.z * lockScale * 0.5f + 0.05f);
                GameObject padlock = RoomDecorKit.SpawnWrapped(decorLock, transform, lockPos,
                    Quaternion.LookRotation(-normal * side, Vector3.up), Vector3.one * lockScale, false, "Cadeado_Porta");
                padlock.transform.SetParent(door.transform, true);
            }
        }

        private GameObject GetOrCreateSecretWall(Vector2Int a, Vector2Int b, Vector3 worldPos, Vector3 scale, Color wallColor, MapRoomNode secretNode)
        {
            string key = "SECRET_" + GetSharedBorderKey(a, b);
            if (sharedBordersDoors.TryGetValue(key, out GameObject existing) && existing != null)
                return existing;

            GameObject crackedWall;
            if (activeTheme != null)
            {
                crackedWall = new GameObject($"Parede_Secreta_Rachada_{key}");
                crackedWall.transform.SetParent(transform);
                crackedWall.transform.position = worldPos;
                BoxCollider col = crackedWall.AddComponent<BoxCollider>();
                col.size = scale;

                bool alongX = scale.x >= scale.z;
                Vector3 towardNormalRoom = worldPos - secretNode.worldCenter;
                towardNormalRoom.y = 0f;
                towardNormalRoom = alongX ? new Vector3(0f, 0f, Mathf.Sign(towardNormalRoom.z)) : new Vector3(Mathf.Sign(towardNormalRoom.x), 0f, 0f);
                activeTheme.BuildWall(crackedWall.transform, new Vector3(0f, -scale.y * 0.5f, 0f), alongX ? scale.x : scale.z, alongX,
                    towardNormalRoom, scale.y, alongX ? scale.z : scale.x, forceDecorated: true, tint: 0.55f);
            }
            else
            {
                crackedWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crackedWall.name = $"Parede_Secreta_Rachada_{key}";
                crackedWall.transform.SetParent(transform);
                crackedWall.transform.position = worldPos;
                crackedWall.transform.localScale = scale;

                Color darkerGrey = new Color(wallColor.r * 0.58f, wallColor.g * 0.58f, wallColor.b * 0.60f);
                Renderer r = crackedWall.GetComponent<Renderer>();
                if (r != null) r.material.color = darkerGrey;
            }

            StandaloneSecretCrackedWall scw = crackedWall.AddComponent<StandaloneSecretCrackedWall>();
            scw.secretRoom = secretNode;

            sharedBordersDoors[key] = crackedWall;
            spawnedFloorObjects.Add(crackedWall);
            return crackedWall;
        }

        private void SpawnBreakableCrates(MapRoomNode node, int count)
        {
            Vector3[] crateSpots =
            {
                new Vector3(-13.2f, 0.65f, 10.2f), new Vector3(-11.5f, 0.65f, 10.5f),
                new Vector3(-13.4f, 0.65f, 8.6f),  new Vector3(-11.8f, 0.65f, 8.8f),
                new Vector3(13.2f, 0.65f, 10.2f),  new Vector3(11.5f, 0.65f, 10.5f),
                new Vector3(13.4f, 0.65f, 8.6f),   new Vector3(11.8f, 0.65f, 8.8f),
                new Vector3(-13.2f, 0.65f, -10.2f),new Vector3(-11.5f, 0.65f, -10.5f),
                new Vector3(-13.4f, 0.65f, -8.6f), new Vector3(-11.8f, 0.65f, -8.8f),
                new Vector3(13.2f, 0.65f, -10.2f), new Vector3(11.5f, 0.65f, -10.5f),
                new Vector3(13.4f, 0.65f, -8.6f),  new Vector3(11.8f, 0.65f, -8.8f),
                new Vector3(-12.8f, 0.65f, 3.8f),  new Vector3(12.8f, 0.65f, -3.8f)
            };

            int actual = Mathf.Min(count, crateSpots.Length);
            for (int i = 0; i < actual; i++)
            {
                Vector3 worldPos = node.worldCenter + crateSpots[i];
                GameObject crate;
                if (customBreakableCratePrefab != null)
                {
                    crate = Instantiate(customBreakableCratePrefab, worldPos, Quaternion.identity, node.roomRoot.transform);
                }
                else
                {
                    crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    crate.transform.SetParent(node.roomRoot.transform);
                    crate.transform.position = worldPos;
                    crate.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
                    crate.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(-20f, 20f), 0f);
                    Renderer r = crate.GetComponent<Renderer>();
                    if (r != null) r.material.color = new Color(0.58f, 0.36f, 0.16f);
                    DressPrimitive(crate, decorCrate);
                }
                crate.name = $"Caixa_Quebravel_{i}";
                StandaloneBreakableCrate bc = crate.GetComponent<StandaloneBreakableCrate>();
                if (bc == null) bc = crate.AddComponent<StandaloneBreakableCrate>();
                bc.rareDropChance = rareCrateDropChance;
            }
        }

        private void SpawnRoomMonsters(MapRoomNode node, int count, bool includeBoss)
        {
            Physics.SyncTransforms();
            List<Vector3> occupiedPositions = new List<Vector3>();

            for (int m = 0; m < count; m++)
            {
                bool isBoss = (includeBoss && m == 0);
                float clearance = isBoss ? 1.35f : 0.95f;

                float angle = (360f / count) * m * Mathf.Deg2Rad;
                Vector3 preferredPos = node.worldCenter + new Vector3(Mathf.Cos(angle) * 9.0f, 1.0f, Mathf.Sin(angle) * 7.0f);
                Vector3 safePos = FindSafeMonsterSpawnPosition(node, preferredPos, clearance, occupiedPositions);
                occupiedPositions.Add(safePos);

                GameObject monsterObj = CreateOrInstantiateMonster(safePos, isBoss, m, node.roomRoot.transform);
                if (monsterObj != null)
                {
                    monsterObj.SetActive(false);
                    node.roomMonsters.Add(monsterObj);
                }
            }
        }

        private GameObject CreateOrInstantiateMonster(Vector3 worldPos, bool isBoss, int index, Transform parent)
        {
            GameObject spawned = null;

            if (isBoss && customBossMonsterPrefab != null)
            {
                spawned = Instantiate(customBossMonsterPrefab, worldPos, Quaternion.identity, parent);
            }
            else if (customMonsterPrefabs != null && customMonsterPrefabs.Length > 0)
            {
                GameObject prefab = customMonsterPrefabs[index % customMonsterPrefabs.Length];
                if (prefab != null)
                {
                    spawned = Instantiate(prefab, worldPos, Quaternion.identity, parent);
                }
            }

            if (spawned == null && spawnPlaceholderMonstersIfPrefabsEmpty)
            {
                spawned = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                spawned.name = isBoss ? "Boss_Teste_Mapa" : $"Monstro_Teste_Mapa_{index}";
                spawned.transform.SetParent(parent);
                spawned.transform.position = worldPos;
                spawned.transform.localScale = isBoss ? new Vector3(1.8f, 1.9f, 1.8f) : new Vector3(1.1f, 1.1f, 1.1f);
                Renderer r = spawned.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material.color = isBoss ? new Color(0.55f, 0.05f, 0.25f) : new Color(0.22f, 0.05f, 0.35f);
                }
                StandalonePlaceholderMonsterTestAI testAI = spawned.AddComponent<StandalonePlaceholderMonsterTestAI>();
                testAI.maxHP = isBoss ? 120f : 40f;
            }

            return spawned;
        }

        private Vector3 FindSafeMonsterSpawnPosition(MapRoomNode node, Vector3 preferredPos, float clearanceRadius, List<Vector3> occupied)
        {
            if (IsSpawnPointFree(node, preferredPos, clearanceRadius, occupied))
                return preferredPos;

            float[] radiiX = { 5.2f, 6.8f, 8.5f, 10.2f, 3.8f };
            float[] radiiZ = { 3.8f, 5.0f, 6.4f, 7.6f, 2.8f };
            int steps = 24;
            float baseAngle = Mathf.Atan2(preferredPos.z - node.worldCenter.z, preferredPos.x - node.worldCenter.x);

            for (int r = 0; r < radiiX.Length; r++)
            {
                for (int s = 0; s < steps; s++)
                {
                    int signedStep = (s % 2 == 0) ? (s / 2) : -((s + 1) / 2);
                    float a = baseAngle + signedStep * (Mathf.PI * 2f / steps);
                    Vector3 candidate = node.worldCenter + new Vector3(Mathf.Cos(a) * radiiX[r], 1.0f, Mathf.Sin(a) * radiiZ[r]);
                    if (IsSpawnPointFree(node, candidate, clearanceRadius, occupied))
                        return candidate;
                }
            }
            return node.worldCenter + new Vector3(0f, 1.0f, -3.2f);
        }

        private bool IsSpawnPointFree(MapRoomNode node, Vector3 worldPos, float clearanceRadius, List<Vector3> occupied)
        {
            if (Vector2.Distance(new Vector2(worldPos.x, worldPos.z), new Vector2(node.worldCenter.x, node.worldCenter.z)) < (1.8f + clearanceRadius))
                return false;

            float halfW = roomWidthX * 0.5f;
            float halfL = roomLengthZ * 0.5f;
            Vector3[] doors =
            {
                node.worldCenter + new Vector3(0f, 1f, halfL - 1.5f),
                node.worldCenter + new Vector3(0f, 1f, -halfL + 1.5f),
                node.worldCenter + new Vector3(halfW - 1.5f, 1f, 0f),
                node.worldCenter + new Vector3(-halfW + 1.5f, 1f, 0f)
            };
            foreach (var d in doors)
            {
                if (Vector2.Distance(new Vector2(worldPos.x, worldPos.z), new Vector2(d.x, d.z)) < 3.2f)
                    return false;
            }

            foreach (var pos in occupied)
            {
                if (Vector2.Distance(new Vector2(worldPos.x, worldPos.z), new Vector2(pos.x, pos.z)) < (clearanceRadius + 1.15f))
                    return false;
            }

            if (node.roomRoot != null)
            {
                Collider[] roomCols = node.roomRoot.GetComponentsInChildren<Collider>(true);
                foreach (var rc in roomCols)
                {
                    if (rc == null || rc.isTrigger || rc.name.Contains("Asfalto")) continue;
                    Bounds b = rc.bounds;
                    b.Expand(new Vector3(clearanceRadius * 2.1f, 4f, clearanceRadius * 2.1f));
                    if (b.Contains(new Vector3(worldPos.x, b.center.y, worldPos.z)))
                        return false;
                }
            }
            return true;
        }

        private StandaloneSmartPost SpawnSmartPost(Vector3 worldPos, MapRoomNode ownerRoom, bool isLighthouseTopBeacon)
        {
            GameObject postRoot;
            if (customSmartLightPostPrefab != null)
            {
                postRoot = Instantiate(customSmartLightPostPrefab, worldPos, Quaternion.identity, ownerRoom.roomRoot.transform);
            }
            else
            {
                postRoot = new GameObject("Poste_Inteligente_Sala");
                postRoot.transform.SetParent(ownerRoom.roomRoot.transform);
                postRoot.transform.position = worldPos;
            }

            if (customSmartLightPostPrefab == null && decorCenterLight != null)
            {
                Bounds bounds = RoomDecorKit.GetLocalBounds(decorCenterLight);
                RoomDecorKit.SpawnWrapped(decorCenterLight, postRoot.transform, postRoot.transform.position, Quaternion.identity,
                    Vector3.one * (4.5f / Mathf.Max(bounds.max.y, 0.01f)), true, "Modelo_Poste");
            }
            else if (customSmartLightPostPrefab == null)
            {
                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.transform.SetParent(postRoot.transform);
                pole.transform.localPosition = new Vector3(0f, 2f, 0f);
                pole.transform.localScale = new Vector3(0.35f, 2f, 0.35f);

                GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "Bulbo_Poste";
                bulb.transform.SetParent(postRoot.transform);
                bulb.transform.localPosition = new Vector3(0f, 4.2f, 0f);
                bulb.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                Renderer br = bulb.GetComponent<Renderer>();
                if (br != null) br.material.color = new Color(1f, 0.85f, 0.2f);
            }

            StandaloneSmartPost sp = postRoot.GetComponent<StandaloneSmartPost>();
            if (sp == null) sp = postRoot.AddComponent<StandaloneSmartPost>();
            sp.ownerRoom = ownerRoom;
            sp.isTopBeacon = isLighthouseTopBeacon;
            return sp;
        }

        // Dois pedestais com relíquias diferentes: pegar uma faz a outra sumir
        private void BuildTreasureRoomPedestals(MapRoomNode node)
        {
            RelicType[] relics = RelicCatalog.Draw(2, RelicSeed(node), false, LocalPowerUps(), offeredRelics);
            offeredRelics.AddRange(relics);
            StandaloneSpecialPedestal p1 = CreateRelicPedestal(node, new Vector3(-4.2f, 1f, 0f), relics[0], "CHOICE_ITEM_A",
                "[E] Escolher {0}: {1}. O outro pedestal some.");
            StandaloneSpecialPedestal p2 = CreateRelicPedestal(node, new Vector3(4.2f, 1f, 0f), relics[1], "CHOICE_ITEM_B",
                "[E] Escolher {0}: {1}. O outro pedestal some.");

            p1.exclusivePartner = p2;
            p2.exclusivePartner = p1;
        }

        // A sala secreta guarda uma relíquia que muda o tiro (arma teleguiada ou tiro duplo) enquanto houver
        private void BuildSecretRoomPedestal(MapRoomNode node)
        {
            RelicType relic = RelicCatalog.Draw(1, RelicSeed(node), true, LocalPowerUps(), offeredRelics)[0];
            offeredRelics.Add(relic);
            CreateRelicPedestal(node, new Vector3(0f, 1.2f, 0f), relic, "SECRET_LEGENDARY_RELIC",
                "[E] Relíquia lendária — {0}: {1}");
        }

        private StandaloneSpecialPedestal CreateRelicPedestal(MapRoomNode node, Vector3 localPos, RelicType relic, string itemId, string prompt)
        {
            StandaloneSpecialPedestal pedestal = CreatePedestal(node.roomRoot.transform, localPos, RelicCatalog.GetColor(relic), itemId, 0,
                string.Format(prompt, RelicCatalog.GetName(relic), RelicCatalog.GetDescription(relic)));
            pedestal.relic = relic;
            return pedestal;
        }

        // Semente própria por sala e andar: o sorteio das relíquias não consome o Random do mapa
        private int RelicSeed(MapRoomNode node)
        {
            return node.gridCoord.x * 73856093 ^ node.gridCoord.y * 19349663 ^ (currentFloorIndex + 1) * 83492791;
        }

        private PlayerPowerUps LocalPowerUps()
        {
            return playerTransform != null ? playerTransform.GetComponent<PlayerPowerUps>() : null;
        }

        private StandaloneSpecialPedestal CreatePedestal(Transform parent, Vector3 localPos, Color color, string itemId, int cost, string prompt)
        {
            GameObject ped;
            if (customPedestalPrefab != null)
            {
                ped = Instantiate(customPedestalPrefab, parent);
                ped.transform.localPosition = localPos;
            }
            else
            {
                ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ped.transform.SetParent(parent);
                ped.transform.localPosition = localPos;
                ped.transform.localScale = new Vector3(1.3f, 1.0f, 1.3f);
                Renderer r = ped.GetComponent<Renderer>();
                if (r != null) r.material.color = color;
                DressPrimitive(ped, decorCrate);
                AddPedestalRelic(ped.transform, color);
            }
            ped.name = $"Pedestal_{itemId}";
            StandaloneSpecialPedestal sp = ped.AddComponent<StandaloneSpecialPedestal>();
            sp.itemID = itemId;
            sp.crystalCost = cost;
            sp.promptText = prompt;
            return sp;
        }

        // Cristal girando em cima do pedestal, na cor do item
        private void AddPedestalRelic(Transform pedestal, Color color)
        {
            GameObject relic = new GameObject("Reliquia_Pedestal");
            relic.transform.SetParent(pedestal, false);
            relic.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            Vector3 s = pedestal.localScale;
            relic.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            relic.AddComponent<StandalonePedestalRelicSpin>();

            GameObject model = PowerUpPickup.LoadModel(PowerUpPickup.CrystalModelName);
            if (model != null)
            {
                PowerUpPickup.CreateModelVisual(model, relic.transform, 0.9f);
                foreach (Renderer body in relic.GetComponentsInChildren<Renderer>()) PowerUpPickup.Tint(body, color);
            }

            Light glow = relic.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = color;
            glow.range = 5f;
            glow.intensity = 2f;
        }

        private void UpdatePlayerRoomTracking(Vector3 playerWorldPos)
        {
            int gx = Mathf.RoundToInt(playerWorldPos.x / roomWidthX);
            int gy = Mathf.RoundToInt(playerWorldPos.z / roomLengthZ);
            Vector2Int coord = new Vector2Int(gx, gy);

            if (coord != currentPlayerRoomCoord && dungeonGrid.TryGetValue(coord, out MapRoomNode nextRoom))
            {
                float dx = Mathf.Abs(playerWorldPos.x - nextRoom.worldCenter.x);
                float dz = Mathf.Abs(playerWorldPos.z - nextRoom.worldCenter.z);
                if (dx < (roomWidthX * 0.5f - 1.6f) && dz < (roomLengthZ * 0.5f - 1.6f))
                {
                    currentPlayerRoomCoord = coord;
                    EnterRoom(nextRoom);
                }
            }

            if (dungeonGrid.TryGetValue(currentPlayerRoomCoord, out MapRoomNode activeRoom))
            {
                EnsureAllCompanionsInsideRoom(activeRoom);
            }
        }

        public void EnsureAllCompanionsInsideCurrentRoom()
        {
            if (dungeonGrid.TryGetValue(currentPlayerRoomCoord, out MapRoomNode activeRoom))
            {
                EnsureAllCompanionsInsideRoom(activeRoom);
            }
        }

        public void EnsureAllCompanionsInsideRoom(MapRoomNode room)
        {
            if (room == null || playerTransform == null) return;

            for (int i = companionTransforms.Count - 1; i >= 0; i--)
            {
                if (companionTransforms[i] == null) companionTransforms.RemoveAt(i);
            }

            if (companionTransforms.Count == 0) return;

            Vector3 playerPos = playerTransform.position;
            Vector3 dirToCenter = (room.worldCenter - playerPos);
            dirToCenter.y = 0f;
            if (dirToCenter.sqrMagnitude < 0.01f) dirToCenter = Vector3.forward;
            else dirToCenter.Normalize();

            Vector3 rightPerp = new Vector3(-dirToCenter.z, 0f, dirToCenter.x);

            for (int i = 0; i < companionTransforms.Count; i++)
            {
                Transform ally = companionTransforms[i];
                if (ally == null) continue;

                if (!IsPositionInsideRoomOrPartner(ally.position, room))
                {
                    float sideSign = (i % 2 == 0) ? 1f : -1f;
                    float sideDist = 1.25f + (i / 2) * 0.95f;
                    Vector3 safeSpot = playerPos + dirToCenter * 2.2f + rightPerp * (sideSign * sideDist);
                    safeSpot.x = Mathf.Clamp(safeSpot.x, room.worldCenter.x - (roomWidthX * 0.5f - 2.3f), room.worldCenter.x + (roomWidthX * 0.5f - 2.3f));
                    safeSpot.z = Mathf.Clamp(safeSpot.z, room.worldCenter.z - (roomLengthZ * 0.5f - 2.3f), room.worldCenter.z + (roomLengthZ * 0.5f - 2.3f));
                    safeSpot.y = 1.0f;
                    ally.position = safeSpot;
                }
            }
        }

        private bool IsPositionInsideRoomOrPartner(Vector3 pos, MapRoomNode room)
        {
            float mx = (roomWidthX * 0.5f) - 1.15f;
            float mz = (roomLengthZ * 0.5f) - 1.15f;

            if (Mathf.Abs(pos.x - room.worldCenter.x) <= mx && Mathf.Abs(pos.z - room.worldCenter.z) <= mz)
                return true;

            if (room.mergedGiantPartner != null)
            {
                var p = room.mergedGiantPartner;
                if (Mathf.Abs(pos.x - p.worldCenter.x) <= mx && Mathf.Abs(pos.z - p.worldCenter.z) <= mz)
                    return true;
            }
            return false;
        }

        private void EnterRoom(MapRoomNode room)
        {
            room.isVisited = true;
            if (room.isSecretRoom) room.isSecretRevealed = true;
            if (room.mergedGiantPartner != null) room.mergedGiantPartner.isVisited = true;

            EnsureAllCompanionsInsideRoom(room);
            onRoomEntered?.Invoke(room.gridCoord, room.roomType.ToString());
            bool postless = GetRoomPostCount(room) == 0;
            SetRoomLit(room.isClearedAndLit && !postless);
            if (darkRooms) ApplyRoomBrightness(roomBrightness);

            if (postless)
            {
                room.doorsUnlocked = true;
                SetRoomDoorsOpen(room, true);
                string roomName = room.roomType == RoomType.Secret ? "Sala Secreta" : "Sala do Item";
                ShowBannerMessage($"{roomName}: sem poste, a escuridão atrai monstros! As portas continuam abertas. Chegue perto de um pedestal e aperte [E].");
                return;
            }

            if (!room.doorsUnlocked)
            {
                SetRoomDoorsOpen(room, false);
                foreach (var m in room.roomMonsters)
                {
                    if (m != null) m.SetActive(true);
                }
                if (room.mergedGiantPartner != null)
                {
                    SetRoomDoorsOpen(room.mergedGiantPartner, false);
                    foreach (var m in room.mergedGiantPartner.roomMonsters)
                    {
                        if (m != null) m.SetActive(true);
                    }
                }

                if (room.roomType != RoomType.Start)
                {
                    ShowBannerMessage($"⚔️ Portas Trancadas! Conserte o Poste [E] e derrote todos os monstros para abrir as portas!");
                }
            }
            else
            {
                SetRoomDoorsOpen(room, true);
                if (room.mergedGiantPartner != null) SetRoomDoorsOpen(room.mergedGiantPartner, true);
            }
        }

        public void SetRoomDoorsOpen(MapRoomNode room, bool open)
        {
            if (room == null) return;
            foreach (var door in room.doorBarriers)
            {
                if (door != null) door.SetActive(!open);
            }
        }

        public void NotifyPostRepaired(StandaloneSmartPost post)
        {
            if (post == null || post.ownerRoom == null) return;
            MapRoomNode room = post.ownerRoom;
            room.isClearedAndLit = true;
            if (room.mergedGiantPartner != null)
            {
                room.mergedGiantPartner.isClearedAndLit = true;
            }

            onSmartPostRepaired?.Invoke(post.gameObject);
            SetRoomLit(true);

            int alive = room.GetAliveMonsterCount();
            if (alive > 0)
            {
                ShowBannerMessage($"💡 Poste consertado! Derrote os {alive} monstros restantes para abrir as portas!");
            }
        }

        private void SpawnLighthouseStaircasePortal(Vector3 pos)
        {
            GameObject portal = new GameObject("Portal_Escadaria_Proximo_Andar");
            portal.transform.SetParent(transform);
            portal.transform.position = new Vector3(pos.x, 0f, pos.z);

            CapsuleCollider trigger = portal.AddComponent<CapsuleCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.4f;
            trigger.height = 2.4f;
            trigger.center = new Vector3(0f, 1.2f, 0f);

            Color cyan = new Color(0.1f, 0.95f, 1f);
            Color violet = new Color(0.62f, 0.3f, 1f);

            // fumaça mística: um redemoinho que gira em volta do centro e sobe se desfazendo
            ParticleSystem swirl = CreatePortalParticles(portal.transform, "Fumaca_Redemoinho", portalSmokeMaterial, 60);
            var main = swirl.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(cyan, violet);
            var emission = swirl.emission;
            emission.rateOverTime = 26f;
            var shape = swirl.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.3f;
            shape.radiusThickness = 0.35f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = swirl.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(1.4f, 2.1f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.radial = new ParticleSystem.MinMaxCurve(-0.25f);
            FadeInAndOut(swirl, 0.55f, 0.6f, 1.25f);

            // névoa baixa que se espalha pelo chão
            ParticleSystem mist = CreatePortalParticles(portal.transform, "Fumaca_Nevoa", portalSmokeMaterial, 30);
            main = mist.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(violet * 0.8f, cyan * 0.7f);
            emission = mist.emission;
            emission.rateOverTime = 9f;
            shape = mist.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            mist.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            FadeInAndOut(mist, 0.35f, 0.8f, 1.4f);

            // faíscas de luz subindo pelo meio
            ParticleSystem sparks = CreatePortalParticles(portal.transform, "Faiscas_Portal", portalSparkMaterial, 50);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, cyan);
            emission = sparks.emission;
            emission.rateOverTime = 18f;
            shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            velocity = sparks.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            FadeInAndOut(sparks, 1f, 1f, 0.4f);

            Light glow = new GameObject("Luz_Portal").AddComponent<Light>();
            glow.transform.SetParent(portal.transform, false);
            glow.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            glow.type = LightType.Point;
            glow.color = Color.Lerp(cyan, violet, 0.35f);
            glow.range = 7f;
            glow.intensity = 3f;

            portal.AddComponent<StandaloneNextFloorPortal>();
            spawnedFloorObjects.Add(portal);
        }

        private static ParticleSystem CreatePortalParticles(Transform parent, string name, Material material, int maxParticles)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = material != null ? material : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));

            ps.Play();
            return ps;
        }

        // Surge, fica no pico de opacidade e some enquanto cresce
        private static void FadeInAndOut(ParticleSystem ps, float peakAlpha, float sizeFrom, float sizeTo)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.25f), new GradientAlphaKey(peakAlpha * 0.6f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, sizeFrom, 1f, sizeTo));
        }

        public void AdvanceToNextFloor()
        {
            if (currentFloorIndex + 1 < floors.Length)
            {
                GenerateFloor(currentFloorIndex + 1);
            }
        }

        // Cores do minimapa, na mesma ordem da legenda
        private static readonly (string label, Color color)[] MinimapLegend =
        {
            ("Você", new Color(0.25f, 1f, 0.45f)),
            ("Saída", new Color(1f, 0.32f, 0.32f)),
            ("Item", new Color(1f, 0.82f, 0.2f)),
            ("Secreta", new Color(0.9f, 0.45f, 1f)),
            ("Acesa", new Color(1f, 0.95f, 0.6f)),
            ("Escura", new Color(0.4f, 0.42f, 0.55f)),
            ("Não visitada", new Color(0.22f, 0.24f, 0.32f))
        };

        // "Não visitada" só entra na legenda com o minimapa sem spoilers ligado
        private static int MinimapLegendCount => HideUnvisitedRoomsOnMinimap ? MinimapLegend.Length : MinimapLegend.Length - 1;

        private void OnGUI()
        {
            float scale = Mathf.Max(0.85f, Screen.height / 1080f);
            CreateHudStyles(scale);

            if (showBuiltInMinimapHUD)
            {
                DrawMinimapHUD(scale);
            }

            if (showBuiltInSubtitleHUD)
            {
                DrawBanner(scale);
                DrawHoverPrompt(scale);
            }
        }

        private void CreateHudStyles(float scale)
        {
            if (bannerStyle != null && Mathf.Approximately(hudStyleScale, scale)) return;
            hudStyleScale = scale;

            bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(31f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(Mathf.RoundToInt(22f * scale), Mathf.RoundToInt(22f * scale), Mathf.RoundToInt(12f * scale), Mathf.RoundToInt(12f * scale))
            };
            bannerStyle.normal.textColor = new Color(1f, 0.95f, 0.7f);

            promptStyle = new GUIStyle(bannerStyle) { fontSize = Mathf.RoundToInt(29f * scale) };
            promptStyle.normal.textColor = Color.white;

            mapTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(21f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            mapTitleStyle.normal.textColor = new Color(0.85f, 0.92f, 1f);

            legendStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(18f * scale),
                alignment = TextAnchor.MiddleLeft
            };
            legendStyle.normal.textColor = new Color(0.85f, 0.88f, 0.95f);
        }

        // Mensagem do andar no topo da tela; some sozinha depois de alguns segundos
        private void DrawBanner(float scale)
        {
            float age = Time.unscaledTime - bannerShownAt;
            if (string.IsNullOrEmpty(activeSubtitleBanner) || age > BannerSeconds) return;

            float alpha = Mathf.Clamp01((BannerSeconds - age) / BannerFadeSeconds);
            float width = Mathf.Min(Screen.width * 0.6f, 1100f * scale);
            DrawHudText(StripEmoji(activeSubtitleBanner), bannerStyle, (Screen.width - width) * 0.5f, 18f * scale, width, alpha, new Color(1f, 0.75f, 0.25f), scale);
        }

        // Dica de interação (postes, pedestais) acima da jogadora, no centro da tela
        private void DrawHoverPrompt(float scale)
        {
            if (string.IsNullOrEmpty(hoverPromptBanner)) return;

            float width = Mathf.Min(Screen.width * 0.55f, 980f * scale);
            DrawHudText(StripEmoji(hoverPromptBanner), promptStyle, (Screen.width - width) * 0.5f, Screen.height * 0.7f, width, 1f, new Color(0.35f, 0.8f, 1f), scale);
        }

        private static void DrawHudText(string text, GUIStyle style, float x, float y, float width, float alpha, Color accent, float scale)
        {
            float height = style.CalcHeight(new GUIContent(text), width);
            Rect box = new Rect(x, y, width, height);

            GUI.color = new Color(0.03f, 0.04f, 0.1f, 0.82f * alpha);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(accent.r, accent.g, accent.b, alpha);
            GUI.DrawTexture(new Rect(box.x, box.y, 5f * scale, box.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(box, text, style);
            GUI.color = Color.white;
        }

        // A fonte padrão do Unity não tem emojis: eles apareciam como quadrados
        private static string StripEmoji(string text)
        {
            System.Text.StringBuilder result = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
            {
                bool emoji = char.IsSurrogate(c) || (c >= '←' && c <= '⯿') || c == '️' || c == '‍';
                if (!emoji) result.Append(c);
            }
            return result.ToString().Trim();
        }

        private void DrawMinimapHUD(float scale)
        {
            const int range = 3;
            float cell = 30f * scale;
            float gap = 10f * scale;
            float step = cell + gap;
            float pad = 12f * scale;
            float titleHeight = 32f * scale;
            float legendRow = 25f * scale;
            int legendRows = Mathf.CeilToInt(MinimapLegendCount / 2f);

            float gridSize = step * (range * 2 + 1) - gap;
            float width = gridSize + pad * 2f;
            float height = titleHeight + gridSize + pad * 2f + legendRows * legendRow + pad * 0.5f;
            Rect panel = new Rect(Screen.width - width - 18f * scale, 18f * scale, width, height);

            GUI.color = new Color(0.03f, 0.04f, 0.1f, 0.78f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x, panel.y + pad * 0.5f, panel.width, titleHeight), "MAPA DO ANDAR", mapTitleStyle);

            Vector2 center = new Vector2(panel.x + panel.width * 0.5f, panel.y + titleHeight + pad + gridSize * 0.5f);
            Vector2 CellCenter(Vector2Int coord) => new Vector2(
                center.x + (coord.x - currentPlayerRoomCoord.x) * step,
                center.y - (coord.y - currentPlayerRoomCoord.y) * step);
            bool Visible(MapRoomNode node) => !(node.isSecretRoom && !node.isSecretRevealed)
                && Mathf.Abs(node.gridCoord.x - currentPlayerRoomCoord.x) <= range
                && Mathf.Abs(node.gridCoord.y - currentPlayerRoomCoord.y) <= range;

            // Passagens entre salas vizinhas
            GUI.color = new Color(0.55f, 0.6f, 0.75f, 0.8f);
            float corridor = 6f * scale;
            foreach (var kvp in dungeonGrid)
            {
                MapRoomNode node = kvp.Value;
                if (!Visible(node)) continue;
                foreach (Vector2Int dir in new[] { Vector2Int.right, Vector2Int.up })
                {
                    if (!dungeonGrid.TryGetValue(node.gridCoord + dir, out MapRoomNode neighbor) || !Visible(neighbor)) continue;
                    Vector2 a = CellCenter(node.gridCoord);
                    Vector2 b = CellCenter(neighbor.gridCoord);
                    bool merged = node.mergedGiantPartner == neighbor;
                    float thickness = merged ? cell : corridor;
                    Rect link = dir.x != 0
                        ? new Rect(a.x, a.y - thickness * 0.5f, b.x - a.x, thickness)
                        : new Rect(a.x - thickness * 0.5f, b.y, thickness, a.y - b.y);
                    if (merged) GUI.color = RoomMinimapColor(node);
                    GUI.DrawTexture(link, Texture2D.whiteTexture);
                    GUI.color = new Color(0.55f, 0.6f, 0.75f, 0.8f);
                }
            }

            foreach (var kvp in dungeonGrid)
            {
                MapRoomNode node = kvp.Value;
                if (!Visible(node)) continue;

                Vector2 c = CellCenter(node.gridCoord);
                Rect rect = new Rect(c.x - cell * 0.5f, c.y - cell * 0.5f, cell, cell);
                bool current = node.gridCoord == currentPlayerRoomCoord;
                if (current)
                {
                    float border = 3f * scale;
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(rect.x - border, rect.y - border, rect.width + border * 2f, rect.height + border * 2f), Texture2D.whiteTexture);
                }
                GUI.color = RoomMinimapColor(node);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }

            float legendY = panel.y + titleHeight + pad * 2f + gridSize;
            float columnWidth = (panel.width - pad * 2f) * 0.5f;
            float swatch = 12f * scale;
            for (int i = 0; i < MinimapLegendCount; i++)
            {
                float x = panel.x + pad + (i % 2) * columnWidth;
                float y = legendY + (i / 2) * legendRow;
                GUI.color = MinimapLegend[i].color;
                GUI.DrawTexture(new Rect(x, y + (legendRow - swatch) * 0.5f, swatch, swatch), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + swatch + 6f * scale, y, columnWidth - swatch, legendRow), MinimapLegend[i].label, legendStyle);
            }
            GUI.color = Color.white;
        }

        private Color RoomMinimapColor(MapRoomNode node)
        {
            if (node.gridCoord == currentPlayerRoomCoord) return MinimapLegend[0].color;
            if (HideUnvisitedRoomsOnMinimap && !node.isVisited) return MinimapLegend[6].color;
            if (node.roomType == RoomType.BossExit) return MinimapLegend[1].color;
            if (node.roomType == RoomType.TreasureChoice) return MinimapLegend[2].color;
            if (node.roomType == RoomType.Secret) return MinimapLegend[3].color;
            if (node.isClearedAndLit) return MinimapLegend[4].color;
            return MinimapLegend[5].color;
        }
    }

    public class StandaloneSmartPost : MonoBehaviour
    {
        public StandaloneLighthouseMapSystem.MapRoomNode ownerRoom;
        public bool isTopBeacon = false;
        public bool isRepaired = false;
        public float repairProgress = 0f;
        public float repairTimeRequired = 1.6f;
        [Tooltip("Depois de consertado, até esta distância da jogadora o poste fica com o brilho máximo.")]
        public float fullBrightDistance = 3f;
        [Tooltip("A partir desta distância da jogadora o poste consertado fica totalmente apagado.")]
        public float darkDistance = 14f;
        [Tooltip("Cor do brilho da lâmpada depois de consertada.")]
        public Color glowColor = new Color(1f, 0.75f, 0.2f);
        public float maxGlowIntensity = 2.5f;
        private Light postLight;
        private Material[] lampMaterials = new Material[0];
        private float appliedGlow = -1f;

        private void Start()
        {
            List<Material> materials = new List<Material>();
            foreach (Renderer lampRenderer in GetComponentsInChildren<Renderer>())
            {
                materials.AddRange(lampRenderer.materials);
            }
            lampMaterials = materials.ToArray();
            foreach (Material material in lampMaterials)
            {
                if (material.HasProperty("_BaseMap") && material.HasProperty("_EmissionMap") && material.GetTexture("_EmissionMap") == null)
                {
                    material.SetTexture("_EmissionMap", material.GetTexture("_BaseMap"));
                }
            }

            GameObject lObj = new GameObject("Luz_Poste");
            lObj.transform.SetParent(transform);
            lObj.transform.localPosition = new Vector3(0f, 4.5f, 0f);
            postLight = lObj.AddComponent<Light>();
            postLight.type = LightType.Point;
            postLight.range = isTopBeacon ? 40f : 22f;
            postLight.intensity = 0f;
            postLight.color = new Color(1f, 0.92f, 0.65f);
        }

        private float MaxIntensity => isTopBeacon ? 6.5f : 3.5f;

        public float SmartBrightness => isRepaired && postLight != null ? postLight.intensity / MaxIntensity : 0f;

        private void Update()
        {
            if (StandaloneLighthouseMapSystem.Instance == null) return;
            Transform player = StandaloneLighthouseMapSystem.Instance.playerTransform;
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.position);
            if (isRepaired)
            {
                UpdateSmartBrightness(dist);
                return;
            }

            if (dist <= 3.6f)
            {
                StandaloneLighthouseMapSystem.Instance.SetHoverPrompt($"💡 Segure [E] para consertar o Poste ({Mathf.RoundToInt((repairProgress / repairTimeRequired) * 100f)}%)");
                if (IsInteractKeyHeld())
                {
                    repairProgress += Time.deltaTime;
                    if (repairProgress >= repairTimeRequired)
                    {
                        CompleteRepair();
                    }
                }
            }
        }

        // Conserta na hora (poste consertado por outra jogadora no modo cooperativo).
        public void ForceRepair()
        {
            if (!isRepaired) CompleteRepair();
        }

        private void CompleteRepair()
        {
            isRepaired = true;
            if (postLight != null) postLight.intensity = MaxIntensity;
            SetGlow(1f);
            StandaloneLighthouseMapSystem.Instance.SetHoverPrompt("");
            StandaloneLighthouseMapSystem.Instance.NotifyPostRepaired(this);
        }

        private void UpdateSmartBrightness(float dist)
        {
            if (postLight == null || isTopBeacon) return;

            float t = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fullBrightDistance, darkDistance, dist));
            postLight.intensity = Mathf.MoveTowards(postLight.intensity, MaxIntensity * t, MaxIntensity * 2f * Time.deltaTime);
            postLight.enabled = postLight.intensity > 0f;
            SetGlow(SmartBrightness);
        }

        private void SetGlow(float brightness)
        {
            float glow = Mathf.Lerp(0.6f, maxGlowIntensity, brightness);
            if (Mathf.Abs(glow - appliedGlow) < 0.01f) return;
            appliedGlow = glow;

            foreach (Material material in lampMaterials)
            {
                if (material == null || !material.HasProperty("_EmissionColor")) continue;
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", glowColor * glow);
            }
        }

        private void OnDestroy()
        {
            foreach (Material material in lampMaterials)
            {
                Destroy(material);
            }
        }

        public static bool IsInteractKeyHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.isPressed;
#else
            return Input.GetKey(KeyCode.E);
#endif
        }

        public static bool IsInteractKeyJustPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }
    }

    public class StandaloneSecretCrackedWall : MonoBehaviour
    {
        public StandaloneLighthouseMapSystem.MapRoomNode secretRoom;
        public int hitsRemaining = 3;

        private void OnCollisionEnter(Collision col)
        {
            if (col.gameObject.name.Contains("Projetil") || col.gameObject.name.Contains("Bullet") || col.gameObject.name.Contains("Tiro"))
            {
                RegisterProjectileHit();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.name.Contains("Projetil") || other.gameObject.name.Contains("Bullet") || other.gameObject.name.Contains("Tiro"))
            {
                RegisterProjectileHit();
            }
        }

        public void RegisterProjectileHit()
        {
            hitsRemaining--;
            transform.localScale *= 0.94f;
            if (hitsRemaining <= 0)
            {
                if (secretRoom != null) secretRoom.isSecretRevealed = true;
                if (StandaloneLighthouseMapSystem.Instance != null)
                {
                    StandaloneLighthouseMapSystem.Instance.ShowBannerMessage("💥 PAREDE SECRETA QUEBRADA! Passagem revelada!");
                }
                Destroy(gameObject);
            }
        }
    }

    public class StandaloneBreakableCrate : MonoBehaviour
    {
        public float rareDropChance = 0.10f;
        public float powerUpDropChance = 0.1f;
        private bool broken;

        private void OnCollisionEnter(Collision col)
        {
            if (col.gameObject.name.Contains("Projetil") || col.gameObject.name.Contains("Bullet") || col.gameObject.name.Contains("Tiro"))
            {
                TakeHit();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.name.Contains("Projetil") || other.gameObject.name.Contains("Bullet") || other.gameObject.name.Contains("Tiro"))
            {
                TakeHit();
            }
        }

        public void TakeHit()
        {
            if (broken) return;
            broken = true;

            if (UnityEngine.Random.value <= rareDropChance)
            {
                SpawnRareRecoveryBattery(transform.position);
            }
            else
            {
                PowerUpPickup.TrySpawn(transform.position, powerUpDropChance);
            }
            Destroy(gameObject);
        }

        private void SpawnRareRecoveryBattery(Vector3 pos)
        {
            GameObject model = PowerUpPickup.LoadModel(PowerUpPickup.BatteryModelName);
            GameObject battery = new GameObject();
            if (model != null)
            {
                PowerUpPickup.CreateModelVisual(model, battery.transform, 0.9f);
            }
            else
            {
                // sem o modelo, só um brilho verde marca a bateria
                Light glow = battery.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(0.2f, 1f, 0.55f);
                glow.range = 3f;
                glow.intensity = 3f;
            }
            SphereCollider trigger = battery.AddComponent<SphereCollider>();
            trigger.radius = 0.45f;
            trigger.isTrigger = true;
            battery.name = "Bateria_Recuperacao_Rara";
            battery.transform.position = new Vector3(pos.x, 0.7f, pos.z);

            battery.AddComponent<StandaloneRareBatteryPickup>();
        }
    }

    public class StandaloneRareBatteryPickup : MonoBehaviour
    {
        public float recoveryAmount = 45f;

        private void Update()
        {
            transform.Rotate(0f, 110f * Time.deltaTime, 0f);
            if (StandaloneLighthouseMapSystem.Instance == null || StandaloneLighthouseMapSystem.Instance.playerTransform == null) return;

            if (Vector3.Distance(transform.position, StandaloneLighthouseMapSystem.Instance.playerTransform.position) < 1.8f)
            {
                PlayerHealth health = StandaloneLighthouseMapSystem.Instance.playerTransform.GetComponent<PlayerHealth>();
                if (health != null) health.IncreaseMax(0f, recoveryAmount);
                StandaloneLighthouseMapSystem.Instance.onRareRecoveryItemCollected?.Invoke(recoveryAmount);
                PowerUpPickup.PlaySound(PowerUpPickup.LoadSound(PowerUpPickup.EnergySoundName));
                StandaloneLighthouseMapSystem.Instance.ShowBannerMessage($"🔋 BATERIA RARA COLETADA! (+{recoveryAmount:0} de vida)");
                Destroy(gameObject);
            }
        }
    }

    public class StandaloneSpecialPedestal : MonoBehaviour
    {
        public string itemID;
        public RelicType relic;
        public int crystalCost = 0;
        public string promptText;
        public StandaloneSpecialPedestal exclusivePartner;
        private bool isUsed = false;

        private void Update()
        {
            if (isUsed || StandaloneLighthouseMapSystem.Instance == null) return;
            Transform player = StandaloneLighthouseMapSystem.Instance.playerTransform;
            if (player == null) return;

            if (Vector3.Distance(transform.position, player.position) <= 2.8f)
            {
                StandaloneLighthouseMapSystem.Instance.SetHoverPrompt(promptText);
                if (StandaloneSmartPost.IsInteractKeyJustPressed())
                {
                    TryActivatePedestal();
                }
            }
        }

        private void TryActivatePedestal()
        {
            var sys = StandaloneLighthouseMapSystem.Instance;
            if (crystalCost > 0)
            {
                if (sys.playerCurrencyCrystals < crystalCost)
                {
                    sys.ShowBannerMessage($"❌ Cristais insuficientes! Precisa de {crystalCost} (Você tem {sys.playerCurrencyCrystals}).");
                    return;
                }
                sys.playerCurrencyCrystals -= crystalCost;
            }

            isUsed = true;
            if (exclusivePartner != null) Destroy(exclusivePartner.gameObject);

            sys.SetHoverPrompt("");
            PlayerPowerUps powerUps = sys.playerTransform != null ? sys.playerTransform.GetComponent<PlayerPowerUps>() : null;
            if (powerUps != null) powerUps.ApplyRelic(relic);
            sys.ShowBannerMessage($"Relíquia: {RelicCatalog.GetName(relic)} — {RelicCatalog.GetDescription(relic)}!");
            sys.onSpecialItemAcquired?.Invoke(itemID);
            Destroy(gameObject);
        }
    }

    public class StandaloneNextFloorPortal : MonoBehaviour
    {
        private void Update()
        {
            if (StandaloneLighthouseMapSystem.Instance == null || StandaloneLighthouseMapSystem.Instance.playerTransform == null) return;
            Vector3 offset = StandaloneLighthouseMapSystem.Instance.playerTransform.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude < 2.2f)
            {
                StandaloneLighthouseMapSystem.Instance.AdvanceToNextFloor();
            }
        }
    }

    public class StandalonePedestalRelicSpin : MonoBehaviour
    {
        private Vector3 basePosition;

        private void Start()
        {
            basePosition = transform.localPosition;
        }

        private void Update()
        {
            transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
            transform.localPosition = basePosition + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.12f;
        }
    }

    public class StandaloneFallbackPlayerTester : MonoBehaviour
    {
        public float moveSpeed = 9f;
        private Camera mainCam;

        private void Start()
        {
            mainCam = Camera.main;
        }

        private void Update()
        {
            Vector2 move = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) move.y += 1f;
                if (Keyboard.current.sKey.isPressed) move.y -= 1f;
                if (Keyboard.current.dKey.isPressed) move.x += 1f;
                if (Keyboard.current.aKey.isPressed) move.x -= 1f;
            }
#else
            move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
            if (move.sqrMagnitude > 1f) move.Normalize();
            transform.position += new Vector3(move.x, 0f, move.y) * moveSpeed * Time.deltaTime;

            if (mainCam != null)
            {
                mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, transform.position + new Vector3(0f, 18f, -12f), Time.deltaTime * 8f);
                mainCam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            }

            bool shoot = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) shoot = true;
#else
            if (Input.GetMouseButtonDown(0)) shoot = true;
#endif
            if (shoot && mainCam != null)
            {
#if ENABLE_INPUT_SYSTEM
                Vector2 mp = Mouse.current.position.ReadValue();
#else
                Vector2 mp = Input.mousePosition;
#endif
                Ray ray = mainCam.ScreenPointToRay(mp);
                Plane p = new Plane(Vector3.up, new Vector3(0f, 1f, 0f));
                if (p.Raycast(ray, out float enter))
                {
                    Vector3 target = ray.GetPoint(enter);
                    Vector3 dir = (target - transform.position).normalized;
                    dir.y = 0f;
                    SpawnTestBullet(transform.position + dir * 1.1f, dir);
                }
            }
        }

        private void SpawnTestBullet(Vector3 pos, Vector3 dir)
        {
            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            b.name = "Projetil_Teste_Mapa";
            b.transform.position = pos;
            b.transform.localScale = Vector3.one * 0.35f;
            Renderer r = b.GetComponent<Renderer>();
            if (r != null) r.material.color = Color.yellow;
            StandaloneFallbackBullet tb = b.AddComponent<StandaloneFallbackBullet>();
            tb.direction = dir;
            Destroy(b, 2.0f);
        }
    }

    public class StandaloneFallbackBullet : MonoBehaviour
    {
        public Vector3 direction;
        public float speed = 24f;

        private void Update()
        {
            transform.position += direction * speed * Time.deltaTime;
            Collider[] hits = Physics.OverlapSphere(transform.position, 0.35f);
            foreach (var h in hits)
            {
                if (h == null || h.name.Contains("Asfalto") || h.name.Contains("Jogadora_Teste")) continue;

                if (StandaloneLighthouseMapSystem.TryHitMapBreakable(h, 25f))
                {
                    Destroy(gameObject);
                    return;
                }

                var testMonster = h.GetComponentInParent<StandalonePlaceholderMonsterTestAI>();
                if (testMonster != null)
                {
                    testMonster.TakeDamage(25f);
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }

    public class StandalonePlaceholderMonsterTestAI : MonoBehaviour
    {
        public float maxHP = 40f;
        [Range(0f, 1f)] public float powerUpDropChance = 0.08f;
        public void TakeDamage(float dmg)
        {
            if (maxHP <= 0f) return;
            maxHP -= dmg;
            if (maxHP <= 0f)
            {
                PowerUpPickup.TrySpawn(transform.position, powerUpDropChance);
                if (StandaloneLighthouseMapSystem.Instance != null)
                    StandaloneLighthouseMapSystem.Instance.playerCurrencyCrystals += 3;
                Destroy(gameObject);
            }
        }
    }
}
