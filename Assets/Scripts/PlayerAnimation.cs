using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
  [Header("Referências (se vazias, procura filhos chamados Model e Gun)")]
  [SerializeField] private Transform model;
  [SerializeField] private Transform gun;
  [SerializeField] private Animator animator;

  [Header("Parado")]
  [SerializeField] private float breathSpeed = 0.35f;
  [SerializeField] private float breathAmount = 0.02f;

  [Header("Andando / correndo")]
  [SerializeField] private float stepFrequency = 2f;
  [SerializeField] private float bobHeight = 0.06f;
  [SerializeField] private float swayAngle = 4f;
  [SerializeField] private float leanAngle = 5f;
  [SerializeField] private float blendSpeed = 8f;

  [Header("Coice ao atirar")]
  [SerializeField] private float recoilDistance = 0.15f;
  [SerializeField] private float recoilBodyAngle = 4f;
  [SerializeField] private float recoilRecovery = 12f;

  [Header("Dor ao levar dano")]
  [SerializeField] private float hurtAngle = 14f;
  [SerializeField] private float hurtRecovery = 7f;
  [Tooltip("Segundos do brilho branco no corpo logo após o golpe.")]
  [SerializeField] private float hurtFlashTime = 0.08f;
  [SerializeField] private Color hurtTint = new Color(1f, 0.35f, 0.35f);

  [Header("Atordoada")]
  [SerializeField] private float stunWobbleAngle = 9f;
  [SerializeField] private float stunWobbleSpeed = 2.2f;
  [SerializeField] private int stunStarCount = 3;
  [SerializeField] private Color stunStarColor = new Color(1f, 0.9f, 0.25f);

  [Header("Envenenada")]
  [SerializeField] private Color poisonTint = new Color(0.45f, 1f, 0.35f);
  [Tooltip("Segundos entre duas bolhas de veneno.")]
  [SerializeField] private float poisonBubbleInterval = 0.15f;

  [Header("Lenta")]
  [SerializeField] private Color slowTint = new Color(0.6f, 0.8f, 1.2f);
  [Tooltip("Quanto o corpo afunda e achata a cada passo pesado.")]
  [SerializeField] private float slowStepSquash = 0.07f;

  [Header("Empurrão")]
  [Tooltip("Graus de inclinação por unidade de velocidade de empurrão.")]
  [SerializeField] private float knockbackLeanPerSpeed = 2.5f;
  [SerializeField] private float knockbackMaxLean = 25f;

  [Header("Morte")]
  [Tooltip("Duração total da animação de morte. O reinício da fase espera ela terminar.")]
  [SerializeField] private float deathDuration = 1.9f;
  [SerializeField] private Color deathTint = new Color(0.25f, 0.22f, 0.32f);

  private static readonly int SpeedParam = Animator.StringToHash("Speed");
  private static readonly int IsRunningParam = Animator.StringToHash("IsRunning");
  private static readonly int ShootParam = Animator.StringToHash("Shoot");

  private PlayerMovement movement;
  private PlayerShooting shooting;
  private CharacterController controller;
  private PlayerHealth health;
  private PlayerStatus status;

  private Vector3 modelBasePosition;
  private Quaternion modelBaseRotation;
  private Vector3 modelBaseScale;
  private Vector3 gunBasePosition;
  private Quaternion gunBaseRotation;

  private float moveAmount;
  private float stepPhase;
  private float recoil;

  private bool hasSpeedParam;
  private bool hasRunningParam;
  private bool hasShootParam;

  private float hurt;
  private float hurtFlash;
  private float stunBlend;
  private float poisonBlend;
  private float slowBlend;
  private float headHeight = 2f;

  private bool deathStarted;
  private float deathElapsed;
  private bool deathMotesSpawned;

  private Material[] bodyMaterials = new Material[0];
  private Color[] bodyColors;
  private Color[] bodyEmissions;

  private Transform fxRoot;
  private Transform[] stars = new Transform[0];
  private readonly List<Bubble> bubbles = new List<Bubble>();
  private float bubbleTimer;
  private readonly List<Bubble> motes = new List<Bubble>();

  private class Bubble
  {
    public Transform transform;
    public float age;
    public float life;
    public float size;
    public Vector3 velocity;
  }

  // Verdadeiro enquanto a animação de morte ainda está tocando
  public bool IsPlayingDeath => deathStarted && deathElapsed < deathDuration;
  public float DeathDuration => deathDuration;

  public void Awake()
  {
    movement = GetComponent<PlayerMovement>();
    shooting = GetComponent<PlayerShooting>();
    controller = GetComponent<CharacterController>();
    health = GetComponent<PlayerHealth>();
    status = GetComponent<PlayerStatus>();

    if (model == null) model = transform.Find("Model");
    if (gun == null) gun = transform.Find("Gun");
    if (animator == null && model != null) animator = model.GetComponentInChildren<Animator>();

    if (model != null)
    {
      modelBasePosition = model.localPosition;
      modelBaseRotation = model.localRotation;
      modelBaseScale = model.localScale;
    }
    if (gun != null)
    {
      gunBasePosition = gun.localPosition;
      gunBaseRotation = gun.localRotation;
    }

    CacheAnimatorParameters();
    CacheBodyMaterials();
  }

  // Troca o modelo animado (PlayerCharacter põe a cientista escolhida no lugar do modelo da cena)
  public void UseModel(Transform newModel, Transform newGun, Animator newAnimator)
  {
    model = newModel;
    gun = newGun;
    animator = newAnimator;
    if (model != null)
    {
      modelBasePosition = model.localPosition;
      modelBaseRotation = model.localRotation;
      modelBaseScale = model.localScale;
    }
    if (gun != null)
    {
      gunBasePosition = gun.localPosition;
      gunBaseRotation = gun.localRotation;
    }
    hasSpeedParam = hasRunningParam = hasShootParam = false;
    CacheAnimatorParameters();
    foreach (Material material in bodyMaterials)
    {
      if (material != null) Destroy(material);
    }
    CacheBodyMaterials();
  }

  public void OnEnable()
  {
    if (shooting != null) shooting.Fired += OnFired;
    if (health != null)
    {
      health.Damaged += OnDamaged;
      health.Died += OnDied;
    }
  }

  public void OnDisable()
  {
    if (shooting != null) shooting.Fired -= OnFired;
    if (health != null)
    {
      health.Damaged -= OnDamaged;
      health.Died -= OnDied;
    }
  }

  public void OnDestroy()
  {
    foreach (Material material in bodyMaterials)
    {
      if (material != null) Destroy(material);
    }
  }

  public void LateUpdate()
  {
    float dt = Time.deltaTime;
    if (status == null) status = GetComponent<PlayerStatus>();

    if (health != null && health.IsDead)
    {
      if (!deathStarted) OnDied();
      AnimateDeath(dt);
      UpdateParticles(motes, dt);
      UpdateParticles(bubbles, dt);
      UpdateStars(0f, dt);
      return;
    }

    Vector3 velocity = controller != null ? controller.velocity : Vector3.zero;
    velocity.y = 0f;
    float speed = velocity.magnitude;
    float walkSpeed = movement != null ? movement.WalkSpeed : 5f;

    float targetMove = speed > 0.1f ? Mathf.Clamp(speed / walkSpeed, 0f, 2f) : 0f;
    moveAmount = Mathf.Lerp(moveAmount, targetMove, 1f - Mathf.Exp(-blendSpeed * dt));
    recoil = Mathf.Lerp(recoil, 0f, 1f - Mathf.Exp(-recoilRecovery * dt));
    hurt = Mathf.Lerp(hurt, 0f, 1f - Mathf.Exp(-hurtRecovery * dt));
    hurtFlash = Mathf.Max(0f, hurtFlash - dt);

    bool stunned = status != null && status.IsStunned;
    bool poisoned = status != null && status.IsPoisoned;
    bool slowed = status != null && status.IsSlowed;
    float statusBlend = 1f - Mathf.Exp(-10f * dt);
    stunBlend = Mathf.Lerp(stunBlend, stunned ? 1f : 0f, statusBlend);
    poisonBlend = Mathf.Lerp(poisonBlend, poisoned ? 1f : 0f, statusBlend);
    slowBlend = Mathf.Lerp(slowBlend, slowed ? 1f : 0f, statusBlend);

    float cadence = moveAmount <= 1f ? moveAmount : 1f + (moveAmount - 1f) * 0.5f;
    stepPhase += dt * stepFrequency * Mathf.PI * cadence;

    float walkBlend = Mathf.Clamp01(moveAmount);
    float bob = Mathf.Abs(Mathf.Sin(stepPhase)) * bobHeight * moveAmount;
    float sway = Mathf.Sin(stepPhase) * swayAngle * walkBlend;
    float lean = leanAngle * moveAmount - recoil * recoilBodyAngle;
    float breath = Mathf.Sin(Time.time * breathSpeed * 2f * Mathf.PI) * breathAmount * (1f - walkBlend);

    // Passos pesados quando lenta: afunda e achata quando o pé toca o chão
    float stepContact = (1f - Mathf.Abs(Mathf.Sin(stepPhase))) * walkBlend;
    float heavy = slowBlend * (0.35f + stepContact) * slowStepSquash;
    bob -= heavy * 0.6f;

    // Dor: joga o corpo para trás e treme um pouco
    lean -= hurt * hurtAngle;
    float shake = hurt * Mathf.Sin(Time.time * 70f) * 4f;

    // Atordoada: cambaleia em círculos
    float wobbleTime = Time.time * stunWobbleSpeed * 2f * Mathf.PI;
    float wobbleX = Mathf.Sin(wobbleTime) * stunWobbleAngle * stunBlend;
    float wobbleZ = Mathf.Cos(wobbleTime) * stunWobbleAngle * stunBlend;

    // Empurrão: o corpo inclina na direção em que está sendo empurrado
    Vector3 knockback = status != null ? transform.InverseTransformDirection(status.KnockbackVelocity) : Vector3.zero;
    float kbPitch = Mathf.Clamp(knockback.z * knockbackLeanPerSpeed, -knockbackMaxLean, knockbackMaxLean);
    float kbRoll = Mathf.Clamp(-knockback.x * knockbackLeanPerSpeed, -knockbackMaxLean, knockbackMaxLean);

    if (model != null)
    {
      Quaternion pose = Quaternion.Euler(lean + wobbleX + kbPitch, shake, sway + wobbleZ + kbRoll);
      model.localPosition = modelBasePosition + Vector3.up * bob;
      model.localRotation = pose * modelBaseRotation;
      Vector3 squash = new Vector3(1f - breath * 0.5f + hurt * 0.06f + heavy * 0.5f, 1f + breath - hurt * 0.08f - heavy, 1f - breath * 0.5f + hurt * 0.06f + heavy * 0.5f);
      model.localScale = Vector3.Scale(modelBaseScale, squash);
    }

    if (gun != null)
    {
      gun.localPosition = gunBasePosition + Vector3.up * bob + gun.localRotation * Vector3.back * recoil * recoilDistance;
    }

    if (animator != null)
    {
      if (hasSpeedParam) animator.SetFloat(SpeedParam, speed);
      if (hasRunningParam) animator.SetBool(IsRunningParam, movement != null && movement.IsRunning);
    }

    ApplyTint();
    UpdateStars(stunBlend, dt);
    UpdatePoisonBubbles(poisoned, dt);
    UpdateParticles(bubbles, dt);
  }

  private void OnFired(WeaponType weapon)
  {
    recoil = Mathf.Min(recoil + GetRecoilStrength(weapon), 1.5f);

    if (animator != null && hasShootParam)
    {
      animator.SetTrigger(ShootParam);
    }
  }

  private void OnDamaged(float damage)
  {
    // Dano pequeno (veneno) faz uma reação menor
    hurt = Mathf.Min(1.2f, hurt + Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(damage / 10f)));
    hurtFlash = hurtFlashTime;
  }

  private void OnDied()
  {
    if (deathStarted)
    {
      return;
    }

    deathStarted = true;
    deathElapsed = 0f;
    deathMotesSpawned = false;
  }

  // Cambaleia, cai de costas, quica e escurece enquanto a luz sai do corpo
  private void AnimateDeath(float dt)
  {
    deathElapsed += dt;
    float t = deathElapsed;
    hurt = Mathf.Lerp(hurt, 0f, 1f - Mathf.Exp(-hurtRecovery * dt));
    hurtFlash = Mathf.Max(0f, hurtFlash - dt);
    stunBlend = Mathf.Lerp(stunBlend, 0f, 1f - Mathf.Exp(-10f * dt));
    poisonBlend = Mathf.Lerp(poisonBlend, 0f, 1f - Mathf.Exp(-10f * dt));
    slowBlend = Mathf.Lerp(slowBlend, 0f, 1f - Mathf.Exp(-10f * dt));

    const float staggerEnd = 0.3f;
    const float fallEnd = 0.85f;
    const float bounceEnd = 1.15f;
    const float fallenAngle = -86f;

    float pitch;
    float rise = 0f;
    float squashY = 1f;
    if (t < staggerEnd)
    {
      float k = t / staggerEnd;
      pitch = Mathf.Sin(k * Mathf.PI) * -12f;
      rise = Mathf.Sin(k * Mathf.PI) * 0.08f;
    }
    else if (t < fallEnd)
    {
      float k = (t - staggerEnd) / (fallEnd - staggerEnd);
      pitch = Mathf.Lerp(0f, fallenAngle, k * k);
    }
    else if (t < bounceEnd)
    {
      float k = (t - fallEnd) / (bounceEnd - fallEnd);
      pitch = fallenAngle + Mathf.Sin(k * Mathf.PI) * 12f * (1f - k);
      squashY = 1f - Mathf.Sin(k * Mathf.PI) * 0.12f;
    }
    else
    {
      pitch = fallenAngle;
    }

    float fade = Mathf.Clamp01((t - fallEnd) / (deathDuration - fallEnd));
    float sink = Mathf.SmoothStep(0f, 1f, fade) * 0.12f;
    float spin = Mathf.Clamp01(t / fallEnd) * 25f;

    Quaternion fall = Quaternion.Euler(pitch, spin, 0f);
    if (model != null)
    {
      model.localPosition = modelBasePosition + Vector3.up * (rise - sink);
      model.localRotation = fall * modelBaseRotation;
      model.localScale = Vector3.Scale(modelBaseScale, new Vector3(1f, squashY, 1f));
    }

    if (gun != null)
    {
      gun.localPosition = fall * gunBasePosition + Vector3.up * (rise - sink);
      gun.localRotation = fall * gunBaseRotation;
    }

    if (!deathMotesSpawned && t >= fallEnd)
    {
      deathMotesSpawned = true;
      SpawnDeathMotes(fall);
    }

    ApplyTint(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - staggerEnd) / (deathDuration - staggerEnd))));
  }

  private void ApplyTint(float death = 0f)
  {
    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
    Color tint = Color.white;
    tint = Color.Lerp(tint, slowTint, 0.45f * slowBlend);
    tint = Color.Lerp(tint, poisonTint, (0.35f + 0.35f * pulse) * poisonBlend);
    tint = Color.Lerp(tint, hurtTint, Mathf.Clamp01(hurt) * 0.8f);
    tint = Color.Lerp(tint, deathTint, death);

    Color glow = Color.black;
    // A emissão usa a textura do modelo (ver CacheBodyMaterials), então o brilho mantém os detalhes
    glow += new Color(0.06f, 0.3f, 0.03f) * (0.4f + 0.6f * pulse) * poisonBlend;
    glow += new Color(0.12f, 0.1f, 0.01f) * stunBlend;
    glow += new Color(0.03f, 0.08f, 0.2f) * slowBlend;
    if (hurtFlash > 0f) glow += Color.white * 0.9f;

    for (int i = 0; i < bodyMaterials.Length; i++)
    {
      Material material = bodyMaterials[i];
      Color baseColor = bodyColors[i] * tint;
      baseColor.a = bodyColors[i].a;
      if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
      else material.color = baseColor;
      if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", bodyEmissions[i] * (1f - death) + glow);
    }
  }

  private void UpdateStars(float blend, float dt)
  {
    if (blend > 0.01f && stars.Length == 0)
    {
      CreateStars();
    }

    if (stars.Length == 0)
    {
      return;
    }

    bool visible = blend > 0.01f;
    Camera cam = Camera.main;
    float angle = Time.time * 300f;
    for (int i = 0; i < stars.Length; i++)
    {
      Transform star = stars[i];
      if (star.gameObject.activeSelf != visible) star.gameObject.SetActive(visible);
      if (!visible)
      {
        continue;
      }

      float a = (angle + 360f * i / stars.Length) * Mathf.Deg2Rad;
      float height = headHeight + 0.25f + Mathf.Sin(Time.time * 5f + i * 2f) * 0.06f;
      star.position = transform.position + new Vector3(Mathf.Cos(a) * 0.45f, height, Mathf.Sin(a) * 0.45f);
      Quaternion facing = cam != null ? Quaternion.LookRotation(cam.transform.forward, Vector3.up) : Quaternion.identity;
      star.rotation = facing * Quaternion.Euler(0f, 0f, Time.time * 360f + i * 40f);
      star.localScale = Vector3.one * 0.32f * blend;
    }
  }

  private void UpdatePoisonBubbles(bool poisoned, float dt)
  {
    if (!poisoned)
    {
      bubbleTimer = 0f;
      return;
    }

    bubbleTimer -= dt;
    while (bubbleTimer <= 0f)
    {
      bubbleTimer += poisonBubbleInterval;
      Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(0.3f, 0.6f);
      Vector3 position = transform.position + new Vector3(ring.x, Random.Range(0.3f, headHeight * 0.9f), ring.y);
      Bubble bubble = TakeParticle(bubbles, "BolhaVeneno", new Color(0.35f, 0.95f, 0.25f), new Color(0.1f, 0.6f, 0.05f));
      bubble.transform.position = position;
      bubble.age = 0f;
      bubble.life = Random.Range(0.6f, 0.9f);
      bubble.size = Random.Range(0.14f, 0.24f);
      bubble.velocity = new Vector3(ring.x * 0.3f, Random.Range(0.6f, 1f), ring.y * 0.3f);
    }
  }

  private void SpawnDeathMotes(Quaternion fall)
  {
    for (int i = 0; i < 14; i++)
    {
      Vector3 along = fall * (Vector3.up * Random.Range(0.1f, headHeight));
      Vector3 position = transform.position + transform.rotation * along + Random.insideUnitSphere * 0.25f;
      Bubble mote = TakeParticle(motes, "LuzSaindo", new Color(1f, 0.95f, 0.75f), new Color(1.5f, 1.3f, 0.8f));
      mote.transform.position = position;
      mote.age = -Random.Range(0f, 0.4f);
      mote.life = Random.Range(0.8f, 1.1f);
      mote.size = Random.Range(0.12f, 0.2f);
      mote.velocity = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(1.2f, 2f), Random.Range(-0.2f, 0.2f));
    }
  }

  // Bolhas crescem, sobem e estouram; partículas de luz sobem e somem
  private static void UpdateParticles(List<Bubble> particles, float dt)
  {
    foreach (Bubble particle in particles)
    {
      if (!particle.transform.gameObject.activeSelf)
      {
        continue;
      }

      particle.age += dt;
      if (particle.age < 0f)
      {
        particle.transform.localScale = Vector3.zero;
        continue;
      }

      float k = particle.age / particle.life;
      if (k >= 1f)
      {
        particle.transform.gameObject.SetActive(false);
        continue;
      }

      particle.transform.position += particle.velocity * dt;
      float grow = k < 0.85f ? Mathf.Lerp(0.4f, 1f, k / 0.85f) : Mathf.Lerp(1.3f, 0f, (k - 0.85f) / 0.15f);
      particle.transform.localScale = Vector3.one * particle.size * grow;
    }
  }

  private Bubble TakeParticle(List<Bubble> pool, string name, Color color, Color emission)
  {
    foreach (Bubble particle in pool)
    {
      if (!particle.transform.gameObject.activeSelf)
      {
        particle.transform.gameObject.SetActive(true);
        return particle;
      }
    }

    GameObject sphere = CreateFxPrimitive(PrimitiveType.Sphere, name, color, emission);
    Bubble created = new Bubble { transform = sphere.transform };
    pool.Add(created);
    return created;
  }

  private void CreateStars()
  {
    Mesh starMesh = CreateStarMesh();
    stars = new Transform[Mathf.Max(1, stunStarCount)];
    for (int i = 0; i < stars.Length; i++)
    {
      GameObject star = CreateFxPrimitive(PrimitiveType.Quad, "EstrelaTontura", stunStarColor, stunStarColor * 1.5f);
      star.GetComponent<MeshFilter>().sharedMesh = starMesh;
      stars[i] = star.transform;
    }
  }

  private GameObject CreateFxPrimitive(PrimitiveType type, string name, Color color, Color emission)
  {
    if (fxRoot == null)
    {
      fxRoot = new GameObject("StatusFX").transform;
      fxRoot.SetParent(transform, false);
    }

    GameObject fx = GameObject.CreatePrimitive(type);
    fx.name = name;
    DestroyImmediate(fx.GetComponent<Collider>());
    fx.transform.SetParent(fxRoot, false);

    Renderer fxRenderer = fx.GetComponent<Renderer>();
    fxRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    Material material = fxRenderer.material;
    material.color = color;
    material.EnableKeyword("_EMISSION");
    material.SetColor("_EmissionColor", emission);
    return fx;
  }

  // Estrela de 5 pontas, visível dos dois lados
  private static Mesh CreateStarMesh()
  {
    // Frente e verso têm vértices próprios para as normais não se anularem
    const int points = 5;
    const int ring = points * 2 + 1;
    Vector3[] vertices = new Vector3[ring * 2];
    Vector3[] normals = new Vector3[ring * 2];
    int[] triangles = new int[points * 2 * 6];
    for (int side = 0; side < 2; side++)
    {
      int offset = side * ring;
      vertices[offset] = Vector3.zero;
      for (int i = 0; i < points * 2; i++)
      {
        float radius = i % 2 == 0 ? 0.5f : 0.22f;
        float a = Mathf.PI / 2f + i * Mathf.PI / points;
        vertices[offset + i + 1] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
      }
      for (int i = 0; i < ring; i++)
      {
        normals[offset + i] = side == 0 ? Vector3.back : Vector3.forward;
      }
      for (int i = 0; i < points * 2; i++)
      {
        int a = offset + i + 1;
        int b = offset + (i + 1) % (points * 2) + 1;
        int t = (side * points * 2 + i) * 3;
        triangles[t] = offset;
        triangles[t + 1] = side == 0 ? b : a;
        triangles[t + 2] = side == 0 ? a : b;
      }
    }

    Mesh mesh = new Mesh { name = "Estrela" };
    mesh.vertices = vertices;
    mesh.normals = normals;
    mesh.triangles = triangles;
    mesh.RecalculateBounds();
    return mesh;
  }

  private void CacheBodyMaterials()
  {
    List<Renderer> renderers = new List<Renderer>();
    if (model != null) renderers.AddRange(model.GetComponentsInChildren<Renderer>());
    if (gun != null) renderers.AddRange(gun.GetComponentsInChildren<Renderer>());

    List<Material> materials = new List<Material>();
    bool hasBounds = false;
    Bounds bounds = new Bounds();
    foreach (Renderer bodyRenderer in renderers)
    {
      materials.AddRange(bodyRenderer.materials);
      if (model != null && bodyRenderer.transform.IsChildOf(model))
      {
        if (hasBounds) bounds.Encapsulate(bodyRenderer.bounds);
        else bounds = bodyRenderer.bounds;
        hasBounds = true;
      }
    }

    if (hasBounds)
    {
      headHeight = Mathf.Max(0.5f, bounds.max.y - transform.position.y);
    }

    bodyMaterials = materials.ToArray();
    bodyColors = new Color[bodyMaterials.Length];
    bodyEmissions = new Color[bodyMaterials.Length];
    for (int i = 0; i < bodyMaterials.Length; i++)
    {
      Material material = bodyMaterials[i];
      bodyColors[i] = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.HasProperty("_Color") ? material.color : Color.white;
      bodyEmissions[i] = material.HasProperty("_EmissionColor") && material.IsKeywordEnabled("_EMISSION") ? material.GetColor("_EmissionColor") : Color.black;
      // Emissão com a mesma textura da cor base: o brilho dos efeitos fica com os detalhes do modelo
      if (bodyEmissions[i].maxColorComponent <= 0f && material.HasProperty("_EmissionMap") && material.HasProperty("_BaseMap")
        && material.GetTexture("_EmissionMap") == null && material.GetTexture("_BaseMap") != null)
      {
        material.SetTexture("_EmissionMap", material.GetTexture("_BaseMap"));
      }
      material.EnableKeyword("_EMISSION");
    }
  }

  private static float GetRecoilStrength(WeaponType weapon)
  {
    switch (weapon)
    {
      case WeaponType.EightWayBurst: return 0.8f;
      case WeaponType.Shotgun: return 1.2f;
      case WeaponType.SineWave: return 0.4f;
      case WeaponType.Exploding: return 1.4f;
      case WeaponType.RapidFire: return 0.25f;
      default: return 0.5f;
    }
  }

  private void CacheAnimatorParameters()
  {
    if (animator == null || animator.runtimeAnimatorController == null)
    {
      return;
    }

    foreach (AnimatorControllerParameter parameter in animator.parameters)
    {
      if (parameter.nameHash == SpeedParam) hasSpeedParam = true;
      if (parameter.nameHash == IsRunningParam) hasRunningParam = true;
      if (parameter.nameHash == ShootParam) hasShootParam = true;
    }
  }
}
