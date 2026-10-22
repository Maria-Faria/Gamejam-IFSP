using System.Collections.Generic;
using GuardiasDaLuz.MapSystem;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
  [SerializeField] private float spawnInterval = 5f;
  [SerializeField] private int enemiesPerWave = 5;
  [Tooltip("Quantidade sorteada de inimigos que aparecem assim que a jogadora entra numa sala (multiplicada pelo número de postes).")]
  [SerializeField] private int minInitialEnemies = 2;
  [SerializeField] private int maxInitialEnemies = 5;
  [Tooltip("Segundos entre novas tentativas quando não há ponto escuro livre para um inimigo nascer.")]
  [SerializeField] private float retryInterval = 0.5f;
  [Tooltip("Limite de inimigos vivos ao mesmo tempo, para as ondas não se acumularem sem fim.")]
  [SerializeField] private int maxAliveEnemies = 40;
  [SerializeField] private float minDistanceFromPlayer = 7f;
  [Tooltip("Nível de luz a partir do qual os inimigos fogem. Eles só nascem em pontos mais escuros que isso.")]
  [SerializeField] private float lightFearThreshold = 1f;
  [SerializeField] private float spawnHeight = 1f;
  [SerializeField] private Color bodyColor = new Color(0.08f, 0.04f, 0.12f);
  [SerializeField] private Color eyeColor = new Color(1f, 0.15f, 0.2f);
  [Tooltip("Altura que cada modelo de monstro ocupa depois de normalizado.")]
  [SerializeField] private float monsterHeight = 1.6f;
  [Tooltip("Largura máxima de um monstro, para modelos achatados não ficarem enormes.")]
  [SerializeField] private float monsterMaxWidth = 1.8f;
  [Tooltip("Giro aplicado aos modelos caso a frente do FBX não aponte para +Z.")]
  [SerializeField] private float monsterYawOffset = 0f;

  private static readonly PrimitiveType[] Shapes =
  {
    PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Cylinder, PrimitiveType.Cube
  };

  private static readonly Vector3[] ShapeScales =
  {
    Vector3.one * 0.85f, Vector3.one, new Vector3(0.8f, 0.5f, 0.8f), new Vector3(0.45f, 1.3f, 0.45f)
  };

  private readonly List<ShadowEnemy> aliveEnemies = new List<ShadowEnemy>();
  private StandaloneLighthouseMapSystem map;
  private float nextWaveTime;
  private StandaloneLighthouseMapSystem.MapRoomNode currentRoom;
  private int pendingSpawns;
  private float nextRetryTime;

  public static void Paint(Renderer renderer, Color color, Color emission)
  {
    if (renderer == null)
    {
      return;
    }

    Material material = renderer.material;
    material.color = color;
    if (emission.maxColorComponent > 0f)
    {
      material.EnableKeyword("_EMISSION");
      material.SetColor("_EmissionColor", emission);
    }
  }

  public void Start()
  {
    map = GetComponent<StandaloneLighthouseMapSystem>();
    if (map == null)
    {
      map = StandaloneLighthouseMapSystem.Instance;
    }
    if (map != null && map.onFloorGenerated != null)
    {
      map.onFloorGenerated.AddListener((floor, level, title) => ClearEnemies());
    }

    nextWaveTime = Time.time + spawnInterval;
  }

  public void Update()
  {
    aliveEnemies.RemoveAll(enemy => enemy == null);

    if (map == null || map.playerTransform == null
      || !map.DungeonGrid.TryGetValue(map.currentPlayerRoomCoord, out StandaloneLighthouseMapSystem.MapRoomNode room))
    {
      return;
    }

    if (room != currentRoom && (currentRoom == null || room != currentRoom.mergedGiantPartner))
    {
      currentRoom = room;
      DespawnEnemiesOutside(room);
      pendingSpawns = Random.Range(minInitialEnemies, maxInitialEnemies + 1) * Mathf.Max(1, map.GetRoomPostCount(room));
      nextRetryTime = Time.time;
      nextWaveTime = Time.time + spawnInterval;
    }
    else
    {
      currentRoom = room;
    }

    if (!map.RoomAttractsMonsters(room))
    {
      pendingSpawns = 0;
      return;
    }

    if (Time.time >= nextWaveTime)
    {
      nextWaveTime = Time.time + spawnInterval;
      pendingSpawns += enemiesPerWave * Mathf.Max(1, map.GetRoomPostCount(room));
    }

    if (pendingSpawns > 0 && Time.time >= nextRetryTime)
    {
      nextRetryTime = Time.time + retryInterval;
      pendingSpawns -= SpawnWave(room, pendingSpawns);
      if (RemainingQuota(room) <= 0)
      {
        pendingSpawns = 0;
      }
    }
  }

  private int SpawnWave(StandaloneLighthouseMapSystem.MapRoomNode room, int amount)
  {
    Transform player = map.playerTransform;
    Collider playerCollider = player.GetComponent<Collider>();
    float height = playerCollider != null ? playerCollider.bounds.center.y : player.position.y + spawnHeight;
    int spawned = 0;
    for (int i = 0; i < amount && aliveEnemies.Count < maxAliveEnemies; i++)
    {
      StandaloneLighthouseMapSystem.MapRoomNode node = PickNodeWithQuota(room);
      if (node == null)
      {
        break;
      }
      if (TryFindSpawnPoint(node, player.position, height, out Vector3 position))
      {
        ShadowEnemy enemy = CreateEnemy(position, player);
        aliveEnemies.Add(enemy);
        map.RegisterRoomMonster(node, enemy.gameObject);
        spawned++;
      }
    }
    return spawned;
  }

  private static int RemainingQuota(StandaloneLighthouseMapSystem.MapRoomNode room)
  {
    int remaining = room.monsterQuota - room.monstersSpawned;
    if (room.mergedGiantPartner != null)
    {
      remaining += room.mergedGiantPartner.monsterQuota - room.mergedGiantPartner.monstersSpawned;
    }
    return remaining;
  }

  private static StandaloneLighthouseMapSystem.MapRoomNode PickNodeWithQuota(StandaloneLighthouseMapSystem.MapRoomNode room)
  {
    StandaloneLighthouseMapSystem.MapRoomNode partner = room.mergedGiantPartner;
    bool roomHasQuota = room.monsterQuota > room.monstersSpawned;
    bool partnerHasQuota = partner != null && partner.monsterQuota > partner.monstersSpawned;
    if (roomHasQuota && partnerHasQuota)
    {
      return Random.value < 0.5f ? room : partner;
    }
    return roomHasQuota ? room : partnerHasQuota ? partner : null;
  }

  private void DespawnEnemiesOutside(StandaloneLighthouseMapSystem.MapRoomNode room)
  {
    for (int i = aliveEnemies.Count - 1; i >= 0; i--)
    {
      Vector3 position = aliveEnemies[i].transform.position;
      if (IsInsideRoom(room, position) || (room.mergedGiantPartner != null && IsInsideRoom(room.mergedGiantPartner, position)))
      {
        continue;
      }

      Destroy(aliveEnemies[i].gameObject);
      aliveEnemies.RemoveAt(i);
    }
  }

  private bool IsInsideRoom(StandaloneLighthouseMapSystem.MapRoomNode room, Vector3 position)
  {
    return Mathf.Abs(position.x - room.worldCenter.x) <= map.roomWidthX * 0.5f
      && Mathf.Abs(position.z - room.worldCenter.z) <= map.roomLengthZ * 0.5f;
  }

  private bool TryFindSpawnPoint(StandaloneLighthouseMapSystem.MapRoomNode room, Vector3 playerPosition, float height, out Vector3 position)
  {
    float halfX = map.roomWidthX * 0.5f - 2f;
    float halfZ = map.roomLengthZ * 0.5f - 2f;

    for (int attempt = 0; attempt < 30; attempt++)
    {
      position = new Vector3(
        room.worldCenter.x + Random.Range(-halfX, halfX),
        height,
        room.worldCenter.z + Random.Range(-halfZ, halfZ));

      Vector3 offset = position - playerPosition;
      offset.y = 0f;
      if (offset.magnitude < minDistanceFromPlayer)
      {
        continue;
      }
      if (LightField.Sample(position, out _) > lightFearThreshold)
      {
        continue;
      }
      if (Physics.CheckSphere(position, 0.6f, ~0, QueryTriggerInteraction.Ignore))
      {
        continue;
      }

      return true;
    }

    position = Vector3.zero;
    return false;
  }

  private ShadowEnemy CreateEnemy(Vector3 position, Transform player)
  {
    GameObject root = new GameObject("Inimigo_Sombra");
    root.transform.SetParent(transform);
    root.transform.position = position;

    CharacterController controller = root.AddComponent<CharacterController>();
    controller.radius = 0.5f;
    controller.height = 1.6f;
    controller.center = Vector3.zero;

    GameObject[] models = map != null ? map.monsterModels : null;
    GameObject model = models != null && models.Length > 0 ? models[Random.Range(0, models.Length)] : null;
    if (model != null)
    {
      CreateModelBody(model, root.transform);
    }
    else
    {
      CreatePrimitiveBody(root.transform);
    }

    for (int i = 0; i < 6; i++)
    {
      EnemyBullet.SpawnSmokePuff(position + Random.insideUnitSphere * 0.6f, Random.Range(0.4f, 0.7f));
    }

    ShadowEnemy enemy = root.AddComponent<ShadowEnemy>();
    enemy.Init(player, lightFearThreshold);
    return enemy;
  }

  private void CreateModelBody(GameObject model, Transform root)
  {
    GameObject body = new GameObject("Corpo");
    body.transform.SetParent(root, false);

    Bounds bounds = RoomDecorKit.GetLocalBounds(model);
    float scale = monsterHeight / Mathf.Max(bounds.size.y, 0.01f);
    float width = Mathf.Max(bounds.size.x, bounds.size.z) * scale;
    if (width > monsterMaxWidth)
    {
      scale *= monsterMaxWidth / width;
    }

    GameObject wrapper = new GameObject($"Modelo_{model.name}");
    wrapper.transform.SetParent(body.transform, false);
    wrapper.transform.localRotation = Quaternion.Euler(0f, monsterYawOffset, 0f);
    wrapper.transform.localScale = Vector3.one * scale;

    GameObject instance = Instantiate(model, wrapper.transform, false);
    instance.transform.localPosition = model.transform.localPosition - bounds.center;
    foreach (Camera camera in instance.GetComponentsInChildren<Camera>(true)) Destroy(camera.gameObject);
    foreach (Light light in instance.GetComponentsInChildren<Light>(true)) Destroy(light.gameObject);
    foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) Destroy(collider);
  }

  private void CreatePrimitiveBody(Transform root)
  {
    int shape = Random.Range(0, Shapes.Length);
    GameObject body = GameObject.CreatePrimitive(Shapes[shape]);
    body.name = "Corpo";
    Destroy(body.GetComponent<Collider>());
    body.transform.SetParent(root, false);
    body.transform.localScale = ShapeScales[shape];
    Paint(body.GetComponent<Renderer>(), bodyColor, Color.black);

    foreach (float side in new[] { -0.18f, 0.18f })
    {
      GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      eye.name = "Olho";
      Destroy(eye.GetComponent<Collider>());
      eye.transform.SetParent(root, false);
      eye.transform.localPosition = new Vector3(side, 0.15f, 0.6f);
      eye.transform.localScale = Vector3.one * 0.14f;
      Paint(eye.GetComponent<Renderer>(), eyeColor, eyeColor * 3f);
    }
  }

  private void ClearEnemies()
  {
    foreach (ShadowEnemy enemy in aliveEnemies)
    {
      if (enemy != null)
      {
        Destroy(enemy.gameObject);
      }
    }
    aliveEnemies.Clear();
    nextWaveTime = Time.time + spawnInterval;
    currentRoom = null;
    pendingSpawns = 0;
  }
}
