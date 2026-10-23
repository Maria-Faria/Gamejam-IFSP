using System.Collections;
using System.Collections.Generic;
using GuardiasDaLuz.MapSystem;
using Roguelike;
using UnityEngine;

// Decide quais monstros aparecem em cada andar do farol (3 níveis x 2 andares) e os faz nascer quando a
// jogadora entra numa sala de combate. Cada andar usa só 3 a 5 tipos, do mais fácil (1-1) ao mais difícil
// (3-2), com mais monstros, ondas extras, variações (venenoso, veloz, atordoante...) e atributos maiores
// a cada andar. O segundo andar de cada nível termina com um chefe: Muscle Boss (1-2), Fat (2-2) e
// Skull Face (3-2). O primeiro andar de cada nível termina numa sala de elite (todos com variação).
// Os monstros contam para a cota da sala: as sombras do EnemySpawner completam o resto.
[RequireComponent(typeof(StandaloneLighthouseMapSystem))]
public class MonsterDirector : MonoBehaviour
{
  // Tipos que não existem no catálogo: Ghost/Ghoul com o MonsterPhaseShift (somem e reaparecem).
  public const string PhantomGhost = "GhostFantasma";
  public const string PhantomGhoul = "GhoulFantasma";

  [System.Serializable]
  public class FloorPlan
  {
    public string label = "1-1";
    [Tooltip("Tipos de monstro deste andar (3 a 5), do catálogo, ou GhostFantasma / GhoulFantasma.")]
    public string[] types;
    [Tooltip("Peso de sorteio de cada tipo (mesma ordem; vazio = todos iguais).")]
    public float[] weights;
    [Tooltip("Monstros por onda numa sala normal (sorteado entre x e y; salões gigantes têm 1,5x).")]
    public Vector2Int perWave = new Vector2Int(2, 3);
    [Tooltip("Ondas por sala: a próxima vem quando sobra no máximo 1 monstro da onda anterior.")]
    public int waves = 1;
    [Range(0f, 1f)] public float variantChance;
    public MonsterVariantType[] variants;
    [Tooltip("Multiplicadores de vida, dano, velocidade e recarga dos ataques.")]
    public float health = 1f;
    public float damage = 1f;
    public float speed = 1f;
    public float cooldown = 1f;
    [Tooltip("Chefe na sala final (vazio = sala de elite).")]
    public string boss;
    public float bossHealth;
    public float bossDamage = 1f;
  }

  [Tooltip("Um plano por andar, na ordem dos andares do mapa (1-1, 1-2, 2-1, 2-2, 3-1, 3-2).")]
  [SerializeField] private FloorPlan[] floorPlans = DefaultPlans();
  [Tooltip("Distância mínima da jogadora para um monstro nascer.")]
  [SerializeField] private float minDistanceFromPlayer = 6f;
  [Tooltip("Segundos entre o aviso de fumaça e o monstro aparecer.")]
  [SerializeField] private float spawnTell = 0.6f;
  [Tooltip("Sombras extras permitidas na sala do chefe (o resto da cota é cortado).")]
  [SerializeField] private int shadowsInBossRoom = 3;
  [SerializeField] private bool showVariantLabels = true;

  private class RoomState
  {
    public StandaloneLighthouseMapSystem.MapRoomNode node;
    public int wavesLeft;
    public bool spawning;
    public readonly List<Monster> alive = new List<Monster>();
  }

  private StandaloneLighthouseMapSystem map;
  private MonsterCatalog catalog;
  private readonly Dictionary<StandaloneLighthouseMapSystem.MapRoomNode, RoomState> rooms =
    new Dictionary<StandaloneLighthouseMapSystem.MapRoomNode, RoomState>();
  private readonly List<MonsterVariant> labelled = new List<MonsterVariant>();
  private readonly List<Monster> phantoms = new List<Monster>();
  private GUIStyle labelStyle;

  public static MonsterDirector Instance { get; private set; }
  public IReadOnlyList<FloorPlan> Plans => floorPlans;
  public Monster CurrentBoss { get; private set; }
  public int CurrentFloor => map != null ? map.currentFloorIndex : 0;
  public FloorPlan CurrentPlan => PlanFor(CurrentFloor);

  public static FloorPlan[] DefaultPlans()
  {
    MonsterVariantType[] level1 = { MonsterVariantType.Veloz, MonsterVariantType.Venenoso };
    MonsterVariantType[] level2 =
    {
      MonsterVariantType.Veloz, MonsterVariantType.Venenoso, MonsterVariantType.Atordoante, MonsterVariantType.Gelido
    };
    MonsterVariantType[] level3 =
    {
      MonsterVariantType.Veloz, MonsterVariantType.Venenoso, MonsterVariantType.Atordoante, MonsterVariantType.Gelido,
      MonsterVariantType.Brutamontes, MonsterVariantType.Divisor
    };

    return new[]
    {
      new FloorPlan
      {
        label = "1-1", types = new[] { "BallShadow", "Regular", "Ghost" }, weights = new[] { 3f, 2f, 2f },
        perWave = new Vector2Int(2, 3), waves = 1, variantChance = 0f, variants = level1,
        health = 1f, damage = 0.5f, speed = 1f, cooldown = 1.2f
      },
      new FloorPlan
      {
        label = "1-2", types = new[] { "BallShadow", "TwistedBall", "Regular", "Ghost", "Muscle" }, weights = new[] { 2f, 2f, 2f, 2f, 1f },
        perWave = new Vector2Int(3, 4), waves = 1, variantChance = 0.12f, variants = level1,
        health = 1.2f, damage = 0.6f, speed = 1.05f, cooldown = 1.1f,
        boss = "MuscleBoss", bossHealth = 1100f, bossDamage = 0.6f
      },
      new FloorPlan
      {
        label = "2-1", types = new[] { "TwistedBall", "Ghoul", "Crescent", "ThreeEyed" }, weights = new[] { 2f, 3f, 2f, 2f },
        perWave = new Vector2Int(3, 4), waves = 2, variantChance = 0.2f, variants = level2,
        health = 1.5f, damage = 0.75f, speed = 1.1f, cooldown = 1f
      },
      new FloorPlan
      {
        label = "2-2", types = new[] { "Ghoul", "Crescent", "ThreeEyed", "Muscle", PhantomGhost }, weights = new[] { 2f, 2f, 2f, 1.5f, 2f },
        perWave = new Vector2Int(3, 5), waves = 2, variantChance = 0.28f, variants = level2,
        health = 1.8f, damage = 0.85f, speed = 1.15f, cooldown = 0.95f,
        boss = "Fat", bossHealth = 1600f, bossDamage = 0.75f
      },
      new FloorPlan
      {
        label = "3-1", types = new[] { "Horned", "Claw", "ThreeEyed", PhantomGhoul }, weights = new[] { 2f, 2f, 2f, 2f },
        perWave = new Vector2Int(4, 5), waves = 2, variantChance = 0.35f, variants = level3,
        health = 2.2f, damage = 0.95f, speed = 1.22f, cooldown = 0.88f
      },
      new FloorPlan
      {
        label = "3-2", types = new[] { "Horned", "Claw", "Crescent", "Muscle", PhantomGhoul }, weights = new[] { 2f, 2f, 1.5f, 1.5f, 2f },
        perWave = new Vector2Int(4, 6), waves = 3, variantChance = 0.45f, variants = level3,
        health = 2.6f, damage = 1.05f, speed = 1.3f, cooldown = 0.8f,
        boss = "SkullFace", bossHealth = 2200f, bossDamage = 0.85f
      }
    };
  }

  public FloorPlan PlanFor(int floorIndex)
  {
    if (floorPlans == null || floorPlans.Length == 0) floorPlans = DefaultPlans();
    return floorPlans[Mathf.Clamp(floorIndex, 0, floorPlans.Length - 1)];
  }

  public void Awake()
  {
    Instance = this;
    if (floorPlans == null || floorPlans.Length == 0) floorPlans = DefaultPlans();
  }

  public void Start()
  {
    map = GetComponent<StandaloneLighthouseMapSystem>();
    catalog = MonsterCatalog.Load();
    if (catalog == null)
    {
      Debug.LogError("[MonsterDirector] MonsterCatalog não encontrado (rode Roguelike/Build Monsters).");
      enabled = false;
      return;
    }

    if (map.onFloorGenerated == null) map.onFloorGenerated = new UnityEngine.Events.UnityEvent<int, int, string>();
    map.onFloorGenerated.AddListener((floor, level, title) => OnFloorGenerated());
  }

  public void OnDestroy()
  {
    if (Instance == this) Instance = null;
  }

  private void OnFloorGenerated()
  {
    StopAllCoroutines();
    rooms.Clear();
    labelled.Clear();
    phantoms.Clear();
    CurrentBoss = null;
  }

  public void Update()
  {
    if (map == null || map.playerTransform == null) return;
    if (!map.DungeonGrid.TryGetValue(map.currentPlayerRoomCoord, out StandaloneLighthouseMapSystem.MapRoomNode node)) return;

    RoomState state = GetState(node);
    if (state == null) return;

    state.alive.RemoveAll(m => m == null || m.Dead);
    if (!state.spawning && state.wavesLeft > 0 && state.alive.Count <= 1)
    {
      StartCoroutine(SpawnWave(state, false));
    }
  }

  // A sala foi vencida em outro lugar (modo cooperativo): sem mais ondas nem chefe nela.
  public void EndRoom(StandaloneLighthouseMapSystem.MapRoomNode node)
  {
    if (node == null) return;
    if (!rooms.TryGetValue(node, out RoomState state))
    {
      state = new RoomState { node = node };
      rooms[node] = state;
      if (node.mergedGiantPartner != null) rooms[node.mergedGiantPartner] = state;
    }
    state.wavesLeft = 0;
  }

  // O estado da sala (o mesmo para as duas metades de um salão gigante), criado na primeira visita.
  private RoomState GetState(StandaloneLighthouseMapSystem.MapRoomNode node)
  {
    if (rooms.TryGetValue(node, out RoomState state)) return state;
    if (node.mergedGiantPartner != null && rooms.TryGetValue(node.mergedGiantPartner, out state)) return state;

    bool combat = node.roomType == StandaloneLighthouseMapSystem.RoomType.Combat
                  || node.roomType == StandaloneLighthouseMapSystem.RoomType.GiantHall
                  || node.roomType == StandaloneLighthouseMapSystem.RoomType.BossExit;
    if (!combat || node.doorsUnlocked || node.isClearedAndLit && node.GetAliveMonsterCount() == 0)
    {
      return null;
    }

    FloorPlan plan = CurrentPlan;
    state = new RoomState { node = node, wavesLeft = Mathf.Max(1, plan.waves) };
    rooms[node] = state;
    if (node.mergedGiantPartner != null) rooms[node.mergedGiantPartner] = state;

    if (node.roomType == StandaloneLighthouseMapSystem.RoomType.BossExit && !string.IsNullOrEmpty(plan.boss))
    {
      StartCoroutine(SpawnBoss(state, plan));
    }
    else
    {
      StartCoroutine(SpawnWave(state, node.roomType == StandaloneLighthouseMapSystem.RoomType.BossExit));
    }
    return state;
  }

  private IEnumerator SpawnBoss(RoomState state, FloorPlan plan)
  {
    state.spawning = true;
    state.wavesLeft = 0;
    MonsterCatalog.Entry entry = catalog.Find(plan.boss);
    if (entry != null && TryFindSpawnPoint(state.node, entry.height, true, out Vector3 position))
    {
      for (int i = 0; i < 10; i++) EnemyBullet.SpawnSmokePuff(position + Vector3.up + Random.insideUnitSphere * 1.2f, Random.Range(0.6f, 1f));
      map.ShowBannerMessage($"☠ CHEFE: {entry.title}! Derrote-o para abrir a escadaria do farol!");
      yield return new WaitForSeconds(spawnTell * 1.5f);

      Monster boss = entry.Spawn(position, map.playerTransform, null, state.node.roomRoot.transform);
      if (boss != null)
      {
        boss.DamageScale = plan.bossDamage;
        boss.SpeedScale = Mathf.Lerp(1f, plan.speed, 0.5f);
        if (boss.Health != null && plan.bossHealth > 0f) boss.Health.SetMax(plan.bossHealth);
        Register(state, boss);
        CurrentBoss = boss;
      }
    }

    // a sala do chefe só deixa algumas sombras nascerem além dele
    StandaloneLighthouseMapSystem.MapRoomNode node = state.node;
    node.monsterQuota = Mathf.Min(node.monsterQuota, node.monstersSpawned + shadowsInBossRoom);
    if (node.mergedGiantPartner != null) node.mergedGiantPartner.monsterQuota = node.mergedGiantPartner.monstersSpawned;
    state.spawning = false;
  }

  private IEnumerator SpawnWave(RoomState state, bool elite)
  {
    state.spawning = true;
    state.wavesLeft--;
    FloorPlan plan = CurrentPlan;

    int count = Random.Range(plan.perWave.x, plan.perWave.y + 1);
    if (state.node.mergedGiantPartner != null) count = Mathf.RoundToInt(count * 1.5f);
    if (elite) count += 2;

    List<Vector3> spots = new List<Vector3>();
    List<string> picks = new List<string>();
    for (int i = 0; i < count; i++)
    {
      string type = PickType(plan);
      MonsterCatalog.Entry entry = catalog.Find(BaseName(type));
      if (entry == null) continue;
      StandaloneLighthouseMapSystem.MapRoomNode node = PickHalf(state.node);
      if (!TryFindSpawnPoint(node, entry.height, false, out Vector3 spot, spots)) continue;
      spots.Add(spot);
      picks.Add(type);
      EnemyBullet.SpawnSmokePuff(spot + Vector3.up * 0.5f, Random.Range(0.5f, 0.8f));
    }

    yield return new WaitForSeconds(spawnTell);

    for (int i = 0; i < picks.Count; i++)
    {
      // a sala pode ter sido liberada durante o aviso (última onda morta, poste consertado): sem reforços
      if (state.node.roomRoot == null || state.node.doorsUnlocked) break;
      Monster monster = SpawnMonster(picks[i], spots[i], plan, elite, state.node.roomRoot.transform);
      if (monster != null) Register(state, monster);
      yield return new WaitForSeconds(0.08f);
    }
    state.spawning = false;
  }

  // Faz nascer um monstro do plano (tipo do catálogo ou fantasma), já com atributos e variação.
  public Monster SpawnMonster(string type, Vector3 position, FloorPlan plan, bool forceVariant = false, Transform parent = null)
  {
    Monster monster = catalog != null ? catalog.Spawn(BaseName(type), position, map != null ? map.playerTransform : null, null, parent) : null;
    if (monster == null) return null;

    monster.SpeedScale = plan.speed;
    monster.DamageScale = plan.damage;
    monster.CooldownScale = plan.cooldown;
    if (monster.Health != null) monster.Health.SetMax(monster.Health.Max * plan.health);

    if (IsPhantom(type))
    {
      monster.gameObject.AddComponent<MonsterPhaseShift>();
      phantoms.Add(monster);
      monster.name = type;
    }

    bool variant = plan.variants != null && plan.variants.Length > 0 && (forceVariant || Random.value < plan.variantChance);
    if (variant)
    {
      MonsterVariantType variantType = plan.variants[Random.Range(0, plan.variants.Length)];
      MonsterVariant applied = MonsterVariant.Apply(monster, variantType, plan.damage);
      if (applied != null) labelled.Add(applied);
    }
    return monster;
  }

  private void Register(RoomState state, Monster monster)
  {
    StandaloneLighthouseMapSystem.MapRoomNode owner = OwnerHalf(state.node, monster.transform.position);
    map.RegisterRoomMonster(owner, monster.gameObject);
    state.alive.Add(monster);
    MonsterVariant variant = monster.GetComponent<MonsterVariant>();
    monster.Died += dead =>
    {
      map.NotifyMonsterKilled(dead.gameObject);
      if (variant != null && variant.Type == MonsterVariantType.Divisor) Split(state, dead);
    };
  }

  // O Divisor se parte em duas bolas de sombra (sem variação) que também contam para a sala.
  private void Split(RoomState state, Monster dead)
  {
    if (state.node.roomRoot == null) return;
    FloorPlan plan = CurrentPlan;
    for (int i = 0; i < 2; i++)
    {
      Vector3 offset = Quaternion.Euler(0f, i * 180f + Random.Range(-40f, 40f), 0f) * Vector3.right * 0.9f;
      Monster ball = SpawnMonster("BallShadow", dead.transform.position + offset, plan, false, state.node.roomRoot.transform);
      if (ball == null) continue;
      if (ball.Health != null) ball.Health.SetMax(ball.Health.Max * 0.6f);
      Register(state, ball);
    }
  }

  public static bool IsPhantom(string type) => type == PhantomGhost || type == PhantomGhoul;

  public static string BaseName(string type)
  {
    if (type == PhantomGhost) return "Ghost";
    if (type == PhantomGhoul) return "Ghoul";
    return type;
  }

  private static string PickType(FloorPlan plan)
  {
    float total = 0f;
    for (int i = 0; i < plan.types.Length; i++) total += Weight(plan, i);
    float pick = Random.value * total;
    for (int i = 0; i < plan.types.Length; i++)
    {
      pick -= Weight(plan, i);
      if (pick <= 0f) return plan.types[i];
    }
    return plan.types[plan.types.Length - 1];
  }

  private static float Weight(FloorPlan plan, int i) =>
    plan.weights != null && i < plan.weights.Length ? Mathf.Max(0f, plan.weights[i]) : 1f;

  private static StandaloneLighthouseMapSystem.MapRoomNode PickHalf(StandaloneLighthouseMapSystem.MapRoomNode node) =>
    node.mergedGiantPartner != null && Random.value < 0.5f ? node.mergedGiantPartner : node;

  private StandaloneLighthouseMapSystem.MapRoomNode OwnerHalf(StandaloneLighthouseMapSystem.MapRoomNode node, Vector3 position)
  {
    StandaloneLighthouseMapSystem.MapRoomNode partner = node.mergedGiantPartner;
    if (partner == null) return node;
    float a = (position - node.worldCenter).sqrMagnitude;
    float b = (position - partner.worldCenter).sqrMagnitude;
    return b < a ? partner : node;
  }

  // Um ponto livre no chão da sala, longe da jogadora, das portas, das paredes e dos outros monstros.
  private bool TryFindSpawnPoint(StandaloneLighthouseMapSystem.MapRoomNode node, float height, bool boss,
                                 out Vector3 position, List<Vector3> taken = null)
  {
    float radius = Mathf.Clamp(height * 0.3f, 0.35f, 1.4f);
    float halfX = map.roomWidthX * 0.5f - 2.2f - radius;
    float halfZ = map.roomLengthZ * 0.5f - 2.2f - radius;
    Vector3 player = map.playerTransform != null ? map.playerTransform.position : node.worldCenter;
    float minDistance = boss ? minDistanceFromPlayer + 3f : minDistanceFromPlayer;

    for (int attempt = 0; attempt < 40; attempt++)
    {
      float shrink = attempt < 25 ? 1f : 0.6f;
      Vector3 p = node.worldCenter + new Vector3(Random.Range(-halfX, halfX) * shrink, 0f, Random.Range(-halfZ, halfZ) * shrink);
      p.y = node.worldCenter.y;
      Vector3 flat = p - player;
      flat.y = 0f;
      if (flat.magnitude < minDistance && attempt < 34) continue;
      if (NearDoor(node, p)) continue;
      if (taken != null && taken.Exists(t => (t - p).sqrMagnitude < (radius * 2.4f) * (radius * 2.4f))) continue;
      if (Physics.CheckCapsule(p + Vector3.up * (radius + 0.1f), p + Vector3.up * Mathf.Max(radius + 0.1f, height - radius),
                               radius, ~0, QueryTriggerInteraction.Ignore))
      {
        continue;
      }
      position = p;
      return true;
    }

    position = node.worldCenter;
    return false;
  }

  private bool NearDoor(StandaloneLighthouseMapSystem.MapRoomNode node, Vector3 p)
  {
    float halfW = map.roomWidthX * 0.5f;
    float halfL = map.roomLengthZ * 0.5f;
    Vector3[] doors =
    {
      node.worldCenter + new Vector3(0f, 0f, halfL), node.worldCenter + new Vector3(0f, 0f, -halfL),
      node.worldCenter + new Vector3(halfW, 0f, 0f), node.worldCenter + new Vector3(-halfW, 0f, 0f)
    };
    foreach (Vector3 d in doors)
    {
      if (new Vector2(p.x - d.x, p.z - d.z).magnitude < 4.5f) return true;
    }
    return false;
  }

  // Rótulo de cada monstro com variação (e dos fantasmas), acima da cabeça.
  public void OnGUI()
  {
    if (!showVariantLabels) return;
    Camera cam = Camera.main;
    if (cam == null) return;

    if (labelStyle == null)
    {
      labelStyle = new GUIStyle(GUI.skin.label)
      {
        fontSize = 12,
        fontStyle = FontStyle.Bold,
        alignment = TextAnchor.MiddleCenter
      };
    }

    labelled.RemoveAll(v => v == null || v.Monster == null || v.Monster.Dead);
    phantoms.RemoveAll(m => m == null || m.Dead);

    foreach (MonsterVariant v in labelled)
    {
      if (v.Monster.Untargetable) continue;
      DrawLabel(cam, v.Monster, MonsterVariant.GetLabel(v.Type), MonsterVariant.GetLabelColor(v.Type));
    }
    foreach (Monster m in phantoms)
    {
      if (m.Untargetable || m.GetComponent<MonsterVariant>() != null) continue;
      DrawLabel(cam, m, "Fantasma", new Color(0.8f, 0.7f, 1f));
    }
  }

  private void DrawLabel(Camera cam, Monster monster, string text, Color color)
  {
    Vector3 head = monster.transform.position + Vector3.up * (monster.Size * monster.transform.localScale.y + 0.35f);
    Vector3 screen = cam.WorldToScreenPoint(head);
    if (screen.z <= 0f) return;
    Rect rect = new Rect(screen.x - 60f, Screen.height - screen.y - 10f, 120f, 20f);
    labelStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
    GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, labelStyle);
    labelStyle.normal.textColor = color;
    GUI.Label(rect, text, labelStyle);
  }
}
