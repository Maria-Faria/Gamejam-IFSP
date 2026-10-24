using System.Collections.Generic;
using UnityEngine;

public enum WeaponType
{
  AimedTriple,
  EightWayBurst,
  Shotgun,
  SineWave,
  Exploding,
  RapidFire,
  // A arma com que toda personagem começa: um tiro mirado, sem gastar munição. As outras são compradas na
  // loja de cada andar (WeaponShop). Ficam no fim para não mudar os números das outras (co-op).
  Pistol,
  // Liberada pela relíquia Núcleo Teleguiado
  Homing
}

public class PlayerShooting : MonoBehaviour
{
  [Header("Geral")]
  [SerializeField] private Transform muzzle;
  [SerializeField] private AudioSource audioSource;
  [SerializeField] private WeaponType currentWeapon = WeaponType.Pistol;
  [Tooltip("Tempo mínimo, em segundos, entre duas trocas de arma.")]
  [SerializeField] private float weaponSwitchCooldown = 0.3f;
  [Tooltip("Projétil roxo usado por todos os padrões de tiro.")]
  [SerializeField] private GameObject bulletPrefab;
  [Tooltip("Som padrão, usado quando uma arma não tem som próprio.")]
  [SerializeField] private AudioClip shootSound;

  [Header("Pistola (tecla 1) - a arma inicial: um tiro mirado, munição e mana infinitas")]
  [SerializeField] private float pistolFireRate = 3.2f;
  [SerializeField] private float pistolDamage = 11f;
  [SerializeField] private AudioClip pistolSound;

  [Header("Empurrão nos monstros (m/s por acerto)")]
  [SerializeField] private float bulletKnockback = 3.5f;
  [SerializeField] private float shotgunKnockback = 5f;
  [SerializeField] private float explosionKnockback = 9f;

  [Header("Triplo Mirado (tecla 2) - mira no inimigo ou caixa mais alinhado com a frente")]
  [SerializeField] private float aimedTripleFireRate = 4f;
  [SerializeField] private float aimedTripleSpreadAngle = 12f;
  [SerializeField] private float aimRange = 20f;
  [Tooltip("Ângulo máximo, a partir da frente da personagem, para escolher um alvo.")]
  [SerializeField] private float aimMaxAngle = 60f;
  [SerializeField] private AudioClip aimedTripleSound;

  [Header("Rajada em 8 Direções (tecla 3)")]
  [SerializeField] private float eightWayFireRate = 2.5f;
  [SerializeField] private int eightWayBullets = 8;
  [SerializeField] private AudioClip eightWaySound;

  [Header("Escopeta (tecla 4)")]
  [Tooltip("Se vazio, usa o bulletPrefab.")]
  [SerializeField] private GameObject shotgunPelletPrefab;
  [SerializeField] private float shotgunFireRate = 1.2f;
  [SerializeField] private int shotgunPellets = 7;
  [SerializeField] private float shotgunSpreadAngle = 30f;
  [SerializeField] private AudioClip shotgunSound;

  [Header("Onda Senoidal (tecla 5)")]
  [SerializeField] private float sineWaveFireRate = 5f;
  [SerializeField] private float sineWaveAmplitude = 0.8f;
  [Tooltip("Oscilações por segundo.")]
  [SerializeField] private float sineWaveFrequency = 3f;
  [Tooltip("Toca em sequência enquanto o disparo estiver segurado.")]
  [SerializeField] private AudioClip sineWaveSound;

  [Header("Explosivo (tecla 6)")]
  [SerializeField] private float explodingFireRate = 1f;
  [SerializeField] private float explodingSpeed = 12f;
  [SerializeField] private float explodingRadius = 3f;
  [SerializeField] private float explodingDamage = 20f;
  [SerializeField] private int explodingFragments = 8;
  [SerializeField] private float explodingFragmentDamage = 5f;
  [SerializeField] private Color explodingColor = new Color(0.7f, 0.3f, 1f, 0.6f);
  [Tooltip("Toca no DISPARO. Se vazio, usa o shootSound.")]
  [SerializeField] private AudioClip explodingLaunchSound;
  [Tooltip("Toca no IMPACTO, quando o projétil explode.")]
  [SerializeField] private AudioClip impactSound;

  [Header("Tiro Rápido (tecla 7)")]
  [SerializeField] private float rapidFireRate = 16f;
  [SerializeField] private float rapidFireSpreadAngle = 6f;
  [SerializeField] private float rapidFireDamage = 5f;
  [Tooltip("Toca em sequência enquanto o disparo estiver segurado.")]
  [SerializeField] private AudioClip rapidFireSound;

  [Header("Teleguiado (tecla 7) - liberado pela relíquia Núcleo Teleguiado")]
  [SerializeField] private float homingFireRate = 3f;
  [SerializeField] private float homingSpeed = 15f;
  [SerializeField] private float homingDamage = 14f;
  [Tooltip("Graus por segundo que o tiro consegue virar atrás do alvo.")]
  [SerializeField] private float homingTurnRate = 260f;
  [SerializeField] private float homingSpreadAngle = 16f;
  [SerializeField] private AudioClip homingSound;

  [Header("Lente Prismática (relíquia)")]
  [Tooltip("Ângulo do feixe gêmeo em relação ao disparo normal.")]
  [SerializeField] private float twinShotAngle = 9f;
  [Tooltip("Dano do feixe gêmeo em relação ao normal.")]
  [SerializeField] private float twinShotDamage = 0.7f;

  [Header("Munição por arma (disparos até esvaziar)")]
  [SerializeField] private int aimedTripleAmmo = 20;
  [SerializeField] private int eightWayAmmo = 10;
  [SerializeField] private int shotgunAmmo = 6;
  [SerializeField] private int sineWaveAmmo = 25;
  [SerializeField] private int explodingAmmo = 5;
  [SerializeField] private int rapidFireAmmo = 60;
  [SerializeField] private int homingAmmo = 16;
  [Tooltip("Segundos para uma arma vazia recarregar por completo.")]
  [SerializeField] private float ammoRefillSeconds = 8f;
  [Tooltip("Segundos sem disparar uma arma até ela começar a recarregar.")]
  [SerializeField] private float ammoRechargeDelay = 1f;

  private static readonly string[] WeaponNames = { "Triplo", "8 Direções", "Escopeta", "Senoidal", "Explosivo", "Rápido", "Pistola", "Teleguiado" };
  // Ordem das teclas 1 a 8 e da lista na tela: a pistola primeiro, depois as armas especiais
  public static readonly WeaponType[] SlotOrder =
  {
    WeaponType.Pistol, WeaponType.AimedTriple, WeaponType.EightWayBurst, WeaponType.Shotgun,
    WeaponType.SineWave, WeaponType.Exploding, WeaponType.RapidFire, WeaponType.Homing
  };

  private float nextFireTime;
  private float nextSwitchTime;
  private AudioSource heldAudio;
  private PlayerPowerUps powerUps;
  private PlayerHealth health;
  private PlayerStatus status;
  private float[] ammo;
  private float[] lastShotTimes;
  private bool[] owned;
  // Enquanto a Lente Prismática repete o disparo: gira os tiros e não toca som nem avisa de novo
  private bool echoing;

  public WeaponType CurrentWeapon => currentWeapon;
  public static string GetName(WeaponType weapon) => WeaponNames[(int)weapon];
  public bool Owns(WeaponType weapon) => owned != null && owned[(int)weapon];
  // Fim do último tiro, para a personagem manter a pistola erguida um pouco depois de atirar
  public float LastShotTime { get; private set; } = float.NegativeInfinity;
  public bool IsFiring { get; private set; }
  // Segura o disparo sem teclado (usado pelos testes automáticos)
  public bool SimulatedFire { get; set; }

  public event System.Action<WeaponType> Fired;
  // Projétil e cano da arma, para o modo cooperativo repetir os tiros da parceira.
  public GameObject BulletPrefab => bulletPrefab;
  public Transform Muzzle => muzzle;

  public void Awake()
  {
    heldAudio = CreateHeldAudio();

    int count = System.Enum.GetValues(typeof(WeaponType)).Length;
    ammo = new float[count];
    lastShotTimes = new float[count];
    owned = new bool[count];
    owned[(int)WeaponType.Pistol] = true;
    currentWeapon = WeaponType.Pistol;
    for (int i = 0; i < count; i++)
    {
      ammo[i] = GetMaxAmmo((WeaponType)i);
      lastShotTimes[i] = float.NegativeInfinity;
    }
  }

  public void Update()
  {
    if (health == null) health = GetComponent<PlayerHealth>();
    if (status == null) status = GetComponent<PlayerStatus>();
    // Morta ou atordoada: não atira nem troca de arma
    bool inputBlocked = (health != null && health.IsDead) || (status != null && status.IsStunned);

    if (!inputBlocked) HandleWeaponSwitch();
    RechargeAmmo();

    if (powerUps == null) powerUps = GetComponent<PlayerPowerUps>();

    bool firing = !inputBlocked && (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0) || SimulatedFire) && bulletPrefab != null && muzzle != null;
    if (firing && currentWeapon != WeaponType.Pistol && ammo[(int)currentWeapon] < 1f)
    {
      SwitchToNextLoadedWeapon();
    }

    int weaponIndex = (int)currentWeapon;
    bool canShoot = currentWeapon == WeaponType.Pistol || ammo[weaponIndex] >= 1f;
    IsFiring = firing && canShoot;
    UpdateHeldAudio(IsFiring ? GetHeldClip() : null);

    if (!IsFiring || Time.time < nextFireTime)
    {
      return;
    }

    if (currentWeapon != WeaponType.Pistol) ammo[weaponIndex] -= 1f;
    lastShotTimes[weaponIndex] = Time.time;
    LastShotTime = Time.time;

    float fireRateMultiplier = (powerUps != null ? powerUps.FireRateMultiplier : 1f) * CharacterStats.FireRateOf(this);
    nextFireTime = Time.time + 1f / (GetFireRate() * fireRateMultiplier);

    FireCurrentWeapon();
    if (powerUps != null && powerUps.TwinShot)
    {
      echoing = true;
      FireCurrentWeapon();
      echoing = false;
    }

    if (ammo[weaponIndex] < 1f)
    {
      SwitchToNextLoadedWeapon();
    }
  }

  private void FireCurrentWeapon()
  {
    switch (currentWeapon)
    {
      case WeaponType.AimedTriple:
        ShootAimedTriple();
        break;
      case WeaponType.EightWayBurst:
        ShootEightWay();
        break;
      case WeaponType.Shotgun:
        ShootShotgun();
        break;
      case WeaponType.SineWave:
        ShootSineWave();
        break;
      case WeaponType.Exploding:
        ShootExploding();
        break;
      case WeaponType.RapidFire:
        ShootRapidFire();
        break;
      case WeaponType.Pistol:
        ShootPistol();
        break;
      case WeaponType.Homing:
        ShootHoming();
        break;
    }
  }

  public bool IsUnlocked(WeaponType weapon) => Owns(weapon);

  // Relíquia (Núcleo Teleguiado): a arma fica disponível e cheia, sem passar pela loja
  public void UnlockWeapon(WeaponType weapon)
  {
    owned[(int)weapon] = true;
    ammo[(int)weapon] = GetMaxAmmo(weapon);
  }

  // Arma vazia: passa para a próxima arma comprada com munição (a pistola nunca acaba)
  private void SwitchToNextLoadedWeapon()
  {
    int slot = System.Array.IndexOf(SlotOrder, currentWeapon);
    for (int step = 1; step <= SlotOrder.Length; step++)
    {
      WeaponType next = SlotOrder[(slot + step) % SlotOrder.Length];
      if (Owns(next) && (next == WeaponType.Pistol || ammo[(int)next] >= 1f))
      {
        SetWeapon(next);
        return;
      }
    }
  }

  // Compra na loja: a arma fica disponível, cheia, e já na mão
  public void Unlock(WeaponType weapon)
  {
    owned[(int)weapon] = true;
    ammo[(int)weapon] = GetMaxAmmo(weapon);
    SetWeapon(weapon);
  }

  public void SetWeapon(WeaponType weapon)
  {
    if (!Owns(weapon))
    {
      return;
    }
    currentWeapon = weapon;
    nextFireTime = Time.time;
  }

  private void HandleWeaponSwitch()
  {
    if (Time.time < nextSwitchTime)
    {
      return;
    }

    WeaponType requested = currentWeapon;
    for (int i = 0; i < SlotOrder.Length; i++)
    {
      if (Input.GetKeyDown(KeyCode.Alpha1 + i) && Owns(SlotOrder[i])) requested = SlotOrder[i];
    }

    // a roda do mouse passa só pelas armas compradas
    float scroll = Input.GetAxis("Mouse ScrollWheel");
    if (scroll != 0f)
    {
      int step = scroll > 0f ? 1 : -1;
      int slot = System.Array.IndexOf(SlotOrder, currentWeapon);
      for (int i = 1; i <= SlotOrder.Length; i++)
      {
        WeaponType next = SlotOrder[(slot + step * i + SlotOrder.Length * 8) % SlotOrder.Length];
        if (Owns(next))
        {
          requested = next;
          break;
        }
      }
    }

    if (requested != currentWeapon && Owns(requested))
    {
      SetWeapon(requested);
      nextSwitchTime = Time.time + weaponSwitchCooldown;
    }
  }

  private void RechargeAmmo()
  {
    float refillSeconds = Mathf.Max(0.01f, ammoRefillSeconds);
    for (int i = 0; i < ammo.Length; i++)
    {
      if (Time.time - lastShotTimes[i] < ammoRechargeDelay)
      {
        continue;
      }

      float max = GetMaxAmmo((WeaponType)i);
      ammo[i] = Mathf.Min(max, ammo[i] + max / refillSeconds * Time.deltaTime);
    }
  }

  private int GetMaxAmmo(WeaponType weapon)
  {
    switch (weapon)
    {
      case WeaponType.EightWayBurst: return eightWayAmmo;
      case WeaponType.Shotgun: return shotgunAmmo;
      case WeaponType.SineWave: return sineWaveAmmo;
      case WeaponType.Exploding: return explodingAmmo;
      case WeaponType.RapidFire: return rapidFireAmmo;
      case WeaponType.Pistol: return 1;
      case WeaponType.Homing: return homingAmmo;
      default: return aimedTripleAmmo;
    }
  }

  public void OnGUI()
  {
    HudScale.Begin();
    DrawHud();
    HudScale.End();
  }

  private void DrawHud()
  {
    const float width = 200f;
    const float rowHeight = 22f;
    float x = HudScale.Width - width - 20f;
    int rows = 0;
    foreach (WeaponType weapon in SlotOrder)
    {
      if (Owns(weapon)) rows++;
    }
    float y = HudScale.Height - 20f - rowHeight * rows;

    int shown = 0;
    for (int slot = 0; slot < SlotOrder.Length; slot++)
    {
      int i = (int)SlotOrder[slot];
      if (!owned[i]) continue;
      Rect row = new Rect(x, y + shown++ * rowHeight, width, rowHeight - 3f);
      bool selected = i == (int)currentWeapon;
      bool pistol = SlotOrder[slot] == WeaponType.Pistol;
      float max = GetMaxAmmo((WeaponType)i);
      bool empty = !pistol && ammo[i] < 1f;

      GUI.color = selected ? new Color(1f, 1f, 1f, 0.9f) : new Color(0f, 0f, 0f, 0.6f);
      GUI.DrawTexture(row, Texture2D.whiteTexture);
      GUI.color = new Color(0.08f, 0.05f, 0.12f, 0.9f);
      GUI.DrawTexture(new Rect(row.x + 2f, row.y + 2f, row.width - 4f, row.height - 4f), Texture2D.whiteTexture);
      GUI.color = empty ? new Color(0.5f, 0.2f, 0.25f) : new Color(0.6f, 0.3f, 1f);
      GUI.DrawTexture(new Rect(row.x + 2f, row.y + 2f, (row.width - 4f) * (pistol ? 1f : ammo[i] / max), row.height - 4f), Texture2D.whiteTexture);
      GUI.color = Color.white;
      string count = pistol ? "∞" : empty ? "VAZIA" : $"{Mathf.FloorToInt(ammo[i])}/{max}";
      GUI.Label(new Rect(row.x + 6f, row.y + 1f, row.width, row.height), $"{slot + 1} {WeaponNames[i]}  {count}");
    }
  }

  private float GetFireRate()
  {
    switch (currentWeapon)
    {
      case WeaponType.EightWayBurst: return eightWayFireRate;
      case WeaponType.Shotgun: return shotgunFireRate;
      case WeaponType.SineWave: return sineWaveFireRate;
      case WeaponType.Exploding: return explodingFireRate;
      case WeaponType.RapidFire: return rapidFireRate;
      case WeaponType.Pistol: return pistolFireRate;
      case WeaponType.Homing: return homingFireRate;
      default: return aimedTripleFireRate;
    }
  }

  private void ShootPistol()
  {
    LightBullet bullet = Spawn(bulletPrefab, GetAimRotation());
    if (bullet != null)
    {
      bullet.SetDamage(pistolDamage);
    }

    PlaySound(OrDefault(pistolSound));
    if (!echoing) Fired?.Invoke(currentWeapon);
  }

  // O inimigo vivo mais perto dentro do alcance, em qualquer direção: para onde a personagem se vira ao
  // atirar (assim ela pode correr para um lado atirando para outro)
  public bool TryGetAimTarget(out Vector3 direction)
  {
    direction = Vector3.zero;
    if (muzzle == null)
    {
      return false;
    }

    float best = aimRange;
    foreach (MonoBehaviour target in FindAimTargets())
    {
      if (target is GuardiasDaLuz.MapSystem.StandaloneBreakableCrate)
      {
        continue;
      }
      Vector3 toTarget = Flatten(target.transform.position - transform.position);
      float distance = toTarget.magnitude;
      if (distance < 0.01f || distance > best)
      {
        continue;
      }
      best = distance;
      direction = toTarget / distance;
    }

    return direction != Vector3.zero;
  }

  private void ShootAimedTriple()
  {
    Quaternion aim = GetAimRotation();
    for (int i = -1; i <= 1; i++)
    {
      Spawn(bulletPrefab, aim * Quaternion.Euler(0f, i * aimedTripleSpreadAngle, 0f));
    }

    PlaySound(OrDefault(aimedTripleSound));
    if (!echoing) Fired?.Invoke(currentWeapon);
  }

  private Quaternion GetAimRotation()
  {
    Vector3 origin = muzzle.position;
    Vector3 forward = Flatten(transform.forward);
    Vector3 bestDirection = forward;
    float bestAngle = aimMaxAngle;

    foreach (MonoBehaviour target in FindAimTargets())
    {
      Vector3 toTarget = Flatten(target.transform.position - origin);
      float distance = toTarget.magnitude;
      float angle = Vector3.Angle(forward, toTarget);
      if (distance < 0.01f || distance > aimRange || angle > bestAngle)
      {
        continue;
      }

      bestAngle = angle;
      bestDirection = toTarget;
    }

    return Quaternion.LookRotation(bestDirection.normalized, Vector3.up);
  }

  public static IEnumerable<MonoBehaviour> FindAimTargets()
  {
    foreach (var crate in FindObjectsByType<GuardiasDaLuz.MapSystem.StandaloneBreakableCrate>())
    {
      yield return crate;
    }

    foreach (ShadowEnemy enemy in FindObjectsByType<ShadowEnemy>())
    {
      yield return enemy;
    }

    foreach (var monster in FindObjectsByType<GuardiasDaLuz.MapSystem.StandalonePlaceholderMonsterTestAI>())
    {
      yield return monster;
    }

    foreach (Roguelike.Monster monster in FindObjectsByType<Roguelike.Monster>())
    {
      if (!monster.Dead && !monster.Untargetable)
      {
        yield return monster;
      }
    }
  }

  private static Vector3 Flatten(Vector3 vector)
  {
    vector.y = 0f;
    return vector;
  }

  private void ShootEightWay()
  {
    int count = Mathf.Max(1, eightWayBullets);
    Quaternion facing = Quaternion.LookRotation(Flatten(transform.forward).normalized, Vector3.up);
    for (int i = 0; i < count; i++)
    {
      Spawn(bulletPrefab, facing * Quaternion.Euler(0f, 360f * i / count, 0f));
    }

    PlaySound(OrDefault(eightWaySound));
    if (!echoing) Fired?.Invoke(currentWeapon);
  }

  private void ShootShotgun()
  {
    GameObject pellet = shotgunPelletPrefab != null ? shotgunPelletPrefab : bulletPrefab;
    if (shotgunPellets <= 0)
    {
      return;
    }

    for (int i = 0; i < shotgunPellets; i++)
    {
      float t = shotgunPellets == 1 ? 0.5f : (float)i / (shotgunPellets - 1);
      float yaw = Mathf.Lerp(-shotgunSpreadAngle / 2f, shotgunSpreadAngle / 2f, t) + Random.Range(-2f, 2f);
      float pitch = Random.Range(-2f, 2f);
      LightBullet bullet = Spawn(pellet, muzzle.rotation * Quaternion.Euler(pitch, yaw, 0f));
      if (bullet != null) bullet.SetKnockback(shotgunKnockback);
    }

    PlaySound(OrDefault(shotgunSound));
    if (!echoing) Fired?.Invoke(currentWeapon);
  }

  private void ShootSineWave()
  {
    foreach (float sign in new[] { 1f, -1f })
    {
      LightBullet bullet = Spawn(bulletPrefab, muzzle.rotation);
      if (bullet != null)
      {
        bullet.ConfigureWave(sineWaveAmplitude * sign, sineWaveFrequency);
      }
    }

    if (!echoing) Fired?.Invoke(currentWeapon);
  }

  private void ShootExploding()
  {
    LightBullet bullet = Spawn(bulletPrefab, muzzle.rotation);
    if (bullet != null)
    {
      bullet.transform.localScale *= 1.6f;
      bullet.SetSpeed(explodingSpeed);
      bullet.ConfigureExplosion(explodingRadius, explodingDamage, explodingColor, impactSound,
        bulletPrefab, explodingFragments, explodingFragmentDamage);
      bullet.SetKnockback(explosionKnockback);
    }

    PlaySound(OrDefault(explodingLaunchSound));
    if (!echoing) Fired?.Invoke(currentWeapon);
  }

  private void ShootRapidFire()
  {
    float halfSpread = rapidFireSpreadAngle / 2f;
    LightBullet bullet = Spawn(bulletPrefab, muzzle.rotation * Quaternion.Euler(0f, Random.Range(-halfSpread, halfSpread), 0f));
    if (bullet != null)
    {
      bullet.SetDamage(rapidFireDamage);
    }

    if (!echoing) Fired?.Invoke(currentWeapon);
  }

  // Dois tiros teleguiados abertos em V, que depois curvam atrás dos monstros
  private void ShootHoming()
  {
    foreach (float side in new[] { -1f, 1f })
    {
      LightBullet bullet = Spawn(bulletPrefab, muzzle.rotation * Quaternion.Euler(0f, side * homingSpreadAngle, 0f));
      if (bullet != null)
      {
        bullet.SetSpeed(homingSpeed);
        bullet.SetDamage(homingDamage);
        bullet.SetLifetime(3.5f);
        bullet.ConfigureHoming(homingTurnRate);
      }
    }

    PlaySound(OrDefault(homingSound));
    if (!echoing) Fired?.Invoke(currentWeapon);
  }

  private LightBullet Spawn(GameObject prefab, Quaternion rotation)
  {
    if (echoing) rotation = Quaternion.Euler(0f, twinShotAngle, 0f) * rotation;
    LightBullet bullet = Instantiate(prefab, muzzle.position, rotation).GetComponent<LightBullet>();
    if (bullet != null)
    {
      if (powerUps != null) bullet.SetBounces(powerUps.RicochetBounces);
      // o dano da personagem (e dos cristais), das relíquias e do feixe gêmeo vale para tudo que o tiro
      // fizer, inclusive a explosão
      float relics = powerUps != null ? powerUps.DamageMultiplier : 1f;
      bullet.SetDamageMultiplier(CharacterStats.DamageOf(this) * relics * (echoing ? twinShotDamage : 1f));
      bullet.SetKnockback(bulletKnockback);
    }

    return bullet;
  }

  private AudioClip GetHeldClip()
  {
    switch (currentWeapon)
    {
      case WeaponType.SineWave: return OrDefault(sineWaveSound);
      case WeaponType.RapidFire: return OrDefault(rapidFireSound);
      default: return null;
    }
  }

  private AudioSource CreateHeldAudio()
  {
    GameObject audioObject = new GameObject("HeldShotAudio");
    audioObject.transform.SetParent(transform, false);

    AudioSource source = audioObject.AddComponent<AudioSource>();
    source.playOnAwake = false;
    source.loop = false;
    if (audioSource != null)
    {
      source.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
      source.spatialBlend = audioSource.spatialBlend;
      source.volume = audioSource.volume;
    }

    return source;
  }

  private void UpdateHeldAudio(AudioClip clip)
  {
    if (clip == null)
    {
      if (heldAudio.isPlaying) heldAudio.Stop();
      return;
    }

    if (heldAudio.clip != clip || !heldAudio.isPlaying)
    {
      heldAudio.clip = clip;
      heldAudio.Play();
    }
  }

  private AudioClip OrDefault(AudioClip clip)
  {
    return clip != null ? clip : shootSound;
  }

  private void PlaySound(AudioClip clip)
  {
    if (!echoing && audioSource != null && clip != null)
    {
      audioSource.PlayOneShot(clip);
    }
  }
}
