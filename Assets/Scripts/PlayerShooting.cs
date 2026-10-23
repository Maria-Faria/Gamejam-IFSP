using System.Collections.Generic;
using UnityEngine;

public enum WeaponType
{
  AimedTriple,
  EightWayBurst,
  Shotgun,
  SineWave,
  Exploding,
  RapidFire
}

public class PlayerShooting : MonoBehaviour
{
  [Header("Geral")]
  [SerializeField] private Transform muzzle;
  [SerializeField] private AudioSource audioSource;
  [SerializeField] private WeaponType currentWeapon = WeaponType.AimedTriple;
  [Tooltip("Tempo mínimo, em segundos, entre duas trocas de arma.")]
  [SerializeField] private float weaponSwitchCooldown = 0.3f;
  [Tooltip("Projétil roxo usado por todos os padrões de tiro.")]
  [SerializeField] private GameObject bulletPrefab;
  [Tooltip("Som padrão, usado quando uma arma não tem som próprio.")]
  [SerializeField] private AudioClip shootSound;

  [Header("Triplo Mirado (tecla 1) - mira no inimigo ou caixa mais alinhado com a frente")]
  [SerializeField] private float aimedTripleFireRate = 4f;
  [SerializeField] private float aimedTripleSpreadAngle = 12f;
  [SerializeField] private float aimRange = 20f;
  [Tooltip("Ângulo máximo, a partir da frente da personagem, para escolher um alvo.")]
  [SerializeField] private float aimMaxAngle = 60f;
  [SerializeField] private AudioClip aimedTripleSound;

  [Header("Rajada em 8 Direções (tecla 2)")]
  [SerializeField] private float eightWayFireRate = 2.5f;
  [SerializeField] private int eightWayBullets = 8;
  [SerializeField] private AudioClip eightWaySound;

  [Header("Escopeta (tecla 3)")]
  [Tooltip("Se vazio, usa o bulletPrefab.")]
  [SerializeField] private GameObject shotgunPelletPrefab;
  [SerializeField] private float shotgunFireRate = 1.2f;
  [SerializeField] private int shotgunPellets = 7;
  [SerializeField] private float shotgunSpreadAngle = 30f;
  [SerializeField] private AudioClip shotgunSound;

  [Header("Onda Senoidal (tecla 4)")]
  [SerializeField] private float sineWaveFireRate = 5f;
  [SerializeField] private float sineWaveAmplitude = 0.8f;
  [Tooltip("Oscilações por segundo.")]
  [SerializeField] private float sineWaveFrequency = 3f;
  [Tooltip("Toca em sequência enquanto o disparo estiver segurado.")]
  [SerializeField] private AudioClip sineWaveSound;

  [Header("Explosivo (tecla 5)")]
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

  [Header("Tiro Rápido (tecla 6)")]
  [SerializeField] private float rapidFireRate = 16f;
  [SerializeField] private float rapidFireSpreadAngle = 6f;
  [SerializeField] private float rapidFireDamage = 5f;
  [Tooltip("Toca em sequência enquanto o disparo estiver segurado.")]
  [SerializeField] private AudioClip rapidFireSound;

  [Header("Custo de mana por disparo")]
  [SerializeField] private float aimedTripleManaCost = 4f;
  [SerializeField] private float eightWayManaCost = 10f;
  [SerializeField] private float shotgunManaCost = 12f;
  [SerializeField] private float sineWaveManaCost = 4f;
  [SerializeField] private float explodingManaCost = 15f;
  [SerializeField] private float rapidFireManaCost = 2f;

  [Header("Munição por arma (disparos até esvaziar)")]
  [SerializeField] private int aimedTripleAmmo = 20;
  [SerializeField] private int eightWayAmmo = 10;
  [SerializeField] private int shotgunAmmo = 6;
  [SerializeField] private int sineWaveAmmo = 25;
  [SerializeField] private int explodingAmmo = 5;
  [SerializeField] private int rapidFireAmmo = 60;
  [Tooltip("Segundos para uma arma vazia recarregar por completo.")]
  [SerializeField] private float ammoRefillSeconds = 8f;
  [Tooltip("Segundos sem disparar uma arma até ela começar a recarregar.")]
  [SerializeField] private float ammoRechargeDelay = 1f;

  private static readonly string[] WeaponNames = { "Triplo", "8 Direções", "Escopeta", "Senoidal", "Explosivo", "Rápido" };

  private float nextFireTime;
  private float nextSwitchTime;
  private AudioSource heldAudio;
  private PlayerMana mana;
  private PlayerPowerUps powerUps;
  private PlayerHealth health;
  private PlayerStatus status;
  private float[] ammo;
  private float[] lastShotTimes;

  public WeaponType CurrentWeapon => currentWeapon;
  public bool IsFiring { get; private set; }
  // Segura o disparo sem teclado (usado pelos testes automáticos)
  public bool SimulatedFire { get; set; }

  public event System.Action<WeaponType> Fired;

  public void Awake()
  {
    heldAudio = CreateHeldAudio();

    int count = System.Enum.GetValues(typeof(WeaponType)).Length;
    ammo = new float[count];
    lastShotTimes = new float[count];
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

    if (mana == null) mana = GetComponent<PlayerMana>();
    if (powerUps == null) powerUps = GetComponent<PlayerPowerUps>();

    bool firing = !inputBlocked && (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0) || SimulatedFire) && bulletPrefab != null && muzzle != null;
    if (firing && ammo[(int)currentWeapon] < 1f)
    {
      SwitchToNextLoadedWeapon();
    }

    float cost = GetManaCost();
    int weaponIndex = (int)currentWeapon;
    bool canShoot = ammo[weaponIndex] >= 1f && (mana == null || mana.Mana >= cost);
    IsFiring = firing && canShoot;
    UpdateHeldAudio(IsFiring ? GetHeldClip() : null);

    if (!IsFiring || Time.time < nextFireTime || (mana != null && !mana.TrySpend(cost)))
    {
      return;
    }

    ammo[weaponIndex] -= 1f;
    lastShotTimes[weaponIndex] = Time.time;

    float fireRateMultiplier = powerUps != null ? powerUps.FireRateMultiplier : 1f;
    nextFireTime = Time.time + 1f / (GetFireRate() * fireRateMultiplier);

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
    }

    if (ammo[weaponIndex] < 1f)
    {
      SwitchToNextLoadedWeapon();
    }
  }

  private void SwitchToNextLoadedWeapon()
  {
    int count = ammo.Length;
    for (int step = 1; step < count; step++)
    {
      int index = ((int)currentWeapon + step) % count;
      if (ammo[index] >= 1f)
      {
        SetWeapon((WeaponType)index);
        return;
      }
    }
  }

  public void SetWeapon(WeaponType weapon)
  {
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
    if (Input.GetKeyDown(KeyCode.Alpha1)) requested = WeaponType.AimedTriple;
    if (Input.GetKeyDown(KeyCode.Alpha2)) requested = WeaponType.EightWayBurst;
    if (Input.GetKeyDown(KeyCode.Alpha3)) requested = WeaponType.Shotgun;
    if (Input.GetKeyDown(KeyCode.Alpha4)) requested = WeaponType.SineWave;
    if (Input.GetKeyDown(KeyCode.Alpha5)) requested = WeaponType.Exploding;
    if (Input.GetKeyDown(KeyCode.Alpha6)) requested = WeaponType.RapidFire;

    float scroll = Input.GetAxis("Mouse ScrollWheel");
    if (scroll != 0f)
    {
      int count = System.Enum.GetValues(typeof(WeaponType)).Length;
      int step = scroll > 0f ? 1 : -1;
      requested = (WeaponType)(((int)currentWeapon + step + count) % count);
    }

    if (requested != currentWeapon)
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
      default: return aimedTripleAmmo;
    }
  }

  public void OnGUI()
  {
    const float width = 200f;
    const float rowHeight = 22f;
    float x = Screen.width - width - 20f;
    float y = Screen.height - 20f - rowHeight * ammo.Length;

    for (int i = 0; i < ammo.Length; i++)
    {
      Rect row = new Rect(x, y + i * rowHeight, width, rowHeight - 3f);
      bool selected = i == (int)currentWeapon;
      float max = GetMaxAmmo((WeaponType)i);
      bool empty = ammo[i] < 1f;

      GUI.color = selected ? new Color(1f, 1f, 1f, 0.9f) : new Color(0f, 0f, 0f, 0.6f);
      GUI.DrawTexture(row, Texture2D.whiteTexture);
      GUI.color = new Color(0.08f, 0.05f, 0.12f, 0.9f);
      GUI.DrawTexture(new Rect(row.x + 2f, row.y + 2f, row.width - 4f, row.height - 4f), Texture2D.whiteTexture);
      GUI.color = empty ? new Color(0.5f, 0.2f, 0.25f) : new Color(0.6f, 0.3f, 1f);
      GUI.DrawTexture(new Rect(row.x + 2f, row.y + 2f, (row.width - 4f) * ammo[i] / max, row.height - 4f), Texture2D.whiteTexture);
      GUI.color = Color.white;
      string count = empty ? "VAZIA" : $"{Mathf.FloorToInt(ammo[i])}/{max}";
      GUI.Label(new Rect(row.x + 6f, row.y + 1f, row.width, row.height), $"{i + 1} {WeaponNames[i]}  {count}");
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
      default: return aimedTripleFireRate;
    }
  }

  private float GetManaCost()
  {
    switch (currentWeapon)
    {
      case WeaponType.EightWayBurst: return eightWayManaCost;
      case WeaponType.Shotgun: return shotgunManaCost;
      case WeaponType.SineWave: return sineWaveManaCost;
      case WeaponType.Exploding: return explodingManaCost;
      case WeaponType.RapidFire: return rapidFireManaCost;
      default: return aimedTripleManaCost;
    }
  }

  private void ShootAimedTriple()
  {
    Quaternion aim = GetAimRotation();
    for (int i = -1; i <= 1; i++)
    {
      Spawn(bulletPrefab, aim * Quaternion.Euler(0f, i * aimedTripleSpreadAngle, 0f));
    }

    PlaySound(OrDefault(aimedTripleSound));
    Fired?.Invoke(currentWeapon);
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

  private static IEnumerable<MonoBehaviour> FindAimTargets()
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
      if (!monster.Dead)
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
    Fired?.Invoke(currentWeapon);
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
      Spawn(pellet, muzzle.rotation * Quaternion.Euler(pitch, yaw, 0f));
    }

    PlaySound(OrDefault(shotgunSound));
    Fired?.Invoke(currentWeapon);
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

    Fired?.Invoke(currentWeapon);
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
    }

    PlaySound(OrDefault(explodingLaunchSound));
    Fired?.Invoke(currentWeapon);
  }

  private void ShootRapidFire()
  {
    float halfSpread = rapidFireSpreadAngle / 2f;
    LightBullet bullet = Spawn(bulletPrefab, muzzle.rotation * Quaternion.Euler(0f, Random.Range(-halfSpread, halfSpread), 0f));
    if (bullet != null)
    {
      bullet.SetDamage(rapidFireDamage);
    }

    Fired?.Invoke(currentWeapon);
  }

  private LightBullet Spawn(GameObject prefab, Quaternion rotation)
  {
    LightBullet bullet = Instantiate(prefab, muzzle.position, rotation).GetComponent<LightBullet>();
    if (bullet != null && powerUps != null)
    {
      bullet.SetBounces(powerUps.RicochetBounces);
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
    if (audioSource != null && clip != null)
    {
      audioSource.PlayOneShot(clip);
    }
  }
}
