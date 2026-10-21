using System.Collections.Generic;
using UnityEngine;

public enum WeaponType
{
  LightPistol,
  Shotgun,
  Laser,
  MachineGun,
  RocketLauncher,
  Railgun
}

public class PlayerShooting : MonoBehaviour
{
  [Header("Geral")]
  [SerializeField] private Transform muzzle;
  [SerializeField] private AudioSource audioSource;
  [SerializeField] private WeaponType currentWeapon = WeaponType.LightPistol;

  [Header("Pistola de Luz (tecla 1)")]
  [SerializeField] private GameObject bulletPrefab;
  [SerializeField] private float fireRate = 6f;
  [SerializeField] private AudioClip shootSound;

  [Header("Escopeta (tecla 2)")]
  [Tooltip("Se vazio, usa o bulletPrefab da pistola.")]
  [SerializeField] private GameObject shotgunPelletPrefab;
  [SerializeField] private float shotgunFireRate = 1.2f;
  [SerializeField] private int shotgunPellets = 7;
  [SerializeField] private float shotgunSpreadAngle = 30f;
  [SerializeField] private AudioClip shotgunSound;

  [Header("Laser (tecla 3)")]
  [SerializeField] private float laserTickRate = 10f;
  [SerializeField] private float laserDamage = 2f;
  [SerializeField] private float laserRange = 30f;
  [SerializeField] private float laserWidth = 0.08f;
  [SerializeField] private Color laserColor = new Color(1f, 0.95f, 0.5f);
  [SerializeField] private LayerMask laserMask = ~0;
  [SerializeField] private AudioClip laserSound;

  [Header("Metralhadora (tecla 4)")]
  [SerializeField] private float machineGunFireRate = 14f;
  [SerializeField] private float machineGunSpreadAngle = 8f;
  [Tooltip("Se vazio, usa o shootSound da pistola.")]
  [SerializeField] private AudioClip machineGunSound;

  [Header("Lança-Foguetes (tecla 5)")]
  [Tooltip("Prefab com o componente Rocket. Se vazio, cria um foguete simples em tempo de execução.")]
  [SerializeField] private GameObject rocketPrefab;
  [SerializeField] private float rocketFireRate = 0.8f;
  [SerializeField] private float rocketSpeed = 14f;
  [SerializeField] private float rocketExplosionRadius = 3.5f;
  [SerializeField] private float rocketExplosionDamage = 25f;
  [SerializeField] private Color rocketColor = new Color(1f, 0.45f, 0.15f);
  [SerializeField] private AudioClip rocketSound;
  [SerializeField] private AudioClip explosionSound;

  [Header("Railgun (tecla 6) - segure para carregar, solte para disparar")]
  [SerializeField] private float railChargeTime = 1f;
  [SerializeField] private float railMinCharge = 0.25f;
  [SerializeField] private float railMinDamage = 10f;
  [SerializeField] private float railMaxDamage = 40f;
  [SerializeField] private float railCooldown = 0.5f;
  [SerializeField] private float railRange = 50f;
  [SerializeField] private float railWidth = 0.2f;
  [SerializeField] private Color railColor = new Color(0.4f, 0.9f, 1f);
  [Tooltip("Se vazio, usa o laserSound.")]
  [SerializeField] private AudioClip railSound;

  private const float RailFlashDuration = 0.2f;

  private float nextFireTime;
  private LineRenderer laserLine;
  private AudioSource laserAudio;
  private bool wasLaserActive;

  private LineRenderer railLine;
  private bool railCharging;
  private float railCharge;
  private float railFlashTimer;

  public WeaponType CurrentWeapon => currentWeapon;

  public void Awake()
  {
    laserLine = CreateBeamLine("LaserBeam", laserWidth, laserColor);
    laserAudio = CreateLaserAudio();
    railLine = CreateBeamLine("RailBeam", railWidth, railColor);
  }

  public void Update()
  {
    HandleWeaponSwitch();

    bool firing = Input.GetButton("Fire1");
    bool laserActive = firing && currentWeapon == WeaponType.Laser && muzzle != null;

    Vector3 laserEnd = Vector3.zero;
    RaycastHit laserHit = default;
    bool laserHitSomething = false;
    if (laserActive)
    {
      laserHitSomething = LaserRaycast(out laserHit);
      laserEnd = laserHitSomething ? laserHit.point : muzzle.position + muzzle.forward * laserRange;
      laserLine.SetPosition(0, muzzle.position);
      laserLine.SetPosition(1, laserEnd);
    }
    laserLine.enabled = laserActive;
    UpdateLaserAudio(laserActive);

    if (currentWeapon == WeaponType.Railgun)
    {
      UpdateRailgunCharge(firing);
    }
    UpdateRailVisual();

    if (firing && currentWeapon != WeaponType.Railgun && Time.time >= nextFireTime)
    {
      nextFireTime = Time.time + 1f / GetFireRate();

      switch (currentWeapon)
      {
        case WeaponType.LightPistol:
          Shoot();
          break;
        case WeaponType.Shotgun:
          ShootShotgun();
          break;
        case WeaponType.Laser:
          if (laserActive)
          {
            LaserTick(laserHitSomething, laserHit);
          }
          break;
        case WeaponType.MachineGun:
          ShootMachineGun();
          break;
        case WeaponType.RocketLauncher:
          ShootRocket();
          break;
      }
    }
  }

  public void SetWeapon(WeaponType weapon)
  {
    currentWeapon = weapon;
    nextFireTime = Time.time;
    railCharging = false;
    railCharge = 0f;
  }

  private void HandleWeaponSwitch()
  {
    if (Input.GetKeyDown(KeyCode.Alpha1)) SetWeapon(WeaponType.LightPistol);
    if (Input.GetKeyDown(KeyCode.Alpha2)) SetWeapon(WeaponType.Shotgun);
    if (Input.GetKeyDown(KeyCode.Alpha3)) SetWeapon(WeaponType.Laser);
    if (Input.GetKeyDown(KeyCode.Alpha4)) SetWeapon(WeaponType.MachineGun);
    if (Input.GetKeyDown(KeyCode.Alpha5)) SetWeapon(WeaponType.RocketLauncher);
    if (Input.GetKeyDown(KeyCode.Alpha6)) SetWeapon(WeaponType.Railgun);

    float scroll = Input.GetAxis("Mouse ScrollWheel");
    if (scroll != 0f)
    {
      int count = System.Enum.GetValues(typeof(WeaponType)).Length;
      int step = scroll > 0f ? 1 : -1;
      SetWeapon((WeaponType)(((int)currentWeapon + step + count) % count));
    }
  }

  private float GetFireRate()
  {
    switch (currentWeapon)
    {
      case WeaponType.Shotgun: return shotgunFireRate;
      case WeaponType.Laser: return laserTickRate;
      case WeaponType.MachineGun: return machineGunFireRate;
      case WeaponType.RocketLauncher: return rocketFireRate;
      default: return fireRate;
    }
  }

  private void Shoot()
  {
    if (bulletPrefab == null || muzzle == null)
    {
      return;
    }

    Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);
    PlaySound(shootSound);
  }

  private void ShootShotgun()
  {
    GameObject pellet = shotgunPelletPrefab != null ? shotgunPelletPrefab : bulletPrefab;
    if (pellet == null || muzzle == null || shotgunPellets <= 0)
    {
      return;
    }

    for (int i = 0; i < shotgunPellets; i++)
    {
      // Espalha os projéteis em leque, com um pouco de aleatoriedade
      float t = shotgunPellets == 1 ? 0.5f : (float)i / (shotgunPellets - 1);
      float yaw = Mathf.Lerp(-shotgunSpreadAngle / 2f, shotgunSpreadAngle / 2f, t) + Random.Range(-2f, 2f);
      float pitch = Random.Range(-2f, 2f);
      Instantiate(pellet, muzzle.position, muzzle.rotation * Quaternion.Euler(pitch, yaw, 0f));
    }

    PlaySound(shotgunSound != null ? shotgunSound : shootSound);
  }

  private void ShootMachineGun()
  {
    if (bulletPrefab == null || muzzle == null)
    {
      return;
    }

    // Tiro rápido e impreciso
    float halfSpread = machineGunSpreadAngle / 2f;
    Quaternion spread = Quaternion.Euler(Random.Range(-halfSpread, halfSpread) * 0.5f, Random.Range(-halfSpread, halfSpread), 0f);
    Instantiate(bulletPrefab, muzzle.position, muzzle.rotation * spread);

    PlaySound(machineGunSound != null ? machineGunSound : shootSound);
  }

  private void ShootRocket()
  {
    if (muzzle == null)
    {
      return;
    }

    GameObject rocketObject = rocketPrefab != null
      ? Instantiate(rocketPrefab, muzzle.position, muzzle.rotation)
      : CreateRuntimeRocket();

    Rocket rocket = rocketObject.GetComponent<Rocket>();
    if (rocket == null)
    {
      rocket = rocketObject.AddComponent<Rocket>();
    }
    rocket.Configure(rocketSpeed, rocketExplosionRadius, rocketExplosionDamage, explosionSound);

    PlaySound(rocketSound != null ? rocketSound : shootSound);
  }

  private GameObject CreateRuntimeRocket()
  {
    GameObject rocketObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    rocketObject.name = "Rocket";
    rocketObject.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
    rocketObject.transform.localScale = Vector3.one * 0.35f;

    rocketObject.GetComponent<Collider>().isTrigger = true;
    Rigidbody body = rocketObject.AddComponent<Rigidbody>();
    body.isKinematic = true;
    body.useGravity = false;

    rocketObject.GetComponent<Renderer>().material.color = rocketColor;
    return rocketObject;
  }

  private void UpdateRailgunCharge(bool firing)
  {
    if (firing && muzzle != null && Time.time >= nextFireTime)
    {
      railCharging = true;
      railCharge = Mathf.Min(railCharge + Time.deltaTime, railChargeTime);
    }
    else if (!firing && railCharging)
    {
      railCharging = false;
      if (railCharge >= railMinCharge)
      {
        FireRailgun(railCharge / railChargeTime);
        nextFireTime = Time.time + railCooldown;
      }
      railCharge = 0f;
    }
  }

  private void FireRailgun(float charge)
  {
    float damage = Mathf.Lerp(railMinDamage, railMaxDamage, charge);
    Vector3 start = muzzle.position;
    Vector3 end = start + muzzle.forward * railRange;

    RaycastHit[] hits = Physics.RaycastAll(start, muzzle.forward, railRange, laserMask, QueryTriggerInteraction.Ignore);
    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

    // Atravessa inimigos, caixas e paredes secretas; para no primeiro obstáculo comum
    HashSet<MonoBehaviour> damaged = new HashSet<MonoBehaviour>();
    foreach (RaycastHit hit in hits)
    {
      if (hit.collider.GetComponentInParent<PlayerMovement>() != null)
      {
        continue;
      }

      MonoBehaviour target = WeaponDamage.FindTarget(hit.collider);
      if (target == null)
      {
        end = hit.point;
        break;
      }

      if (damaged.Add(target))
      {
        WeaponDamage.Damage(target, damage);
      }
    }

    railLine.SetPosition(0, start);
    railLine.SetPosition(1, end);
    railFlashTimer = RailFlashDuration;

    PlaySound(railSound != null ? railSound : laserSound);
  }

  private void UpdateRailVisual()
  {
    if (railCharging && muzzle != null)
    {
      // Mira fininha que fica mais forte conforme carrega
      float t = railCharge / railChargeTime;
      railLine.SetPosition(0, muzzle.position);
      railLine.SetPosition(1, muzzle.position + muzzle.forward * railRange);
      SetBeamStyle(railLine, railWidth * Mathf.Lerp(0.05f, 0.3f, t), railColor, Mathf.Lerp(0.1f, 0.5f, t));
      railLine.enabled = true;
    }
    else if (railFlashTimer > 0f)
    {
      railFlashTimer -= Time.deltaTime;
      float t = Mathf.Clamp01(railFlashTimer / RailFlashDuration);
      SetBeamStyle(railLine, railWidth * t, railColor, t);
      railLine.enabled = true;
    }
    else
    {
      railLine.enabled = false;
    }
  }

  private bool LaserRaycast(out RaycastHit closest)
  {
    closest = default;
    bool found = false;
    RaycastHit[] hits = Physics.RaycastAll(muzzle.position, muzzle.forward, laserRange, laserMask, QueryTriggerInteraction.Ignore);

    foreach (RaycastHit hit in hits)
    {
      if (hit.collider.GetComponentInParent<PlayerMovement>() != null)
      {
        continue;
      }

      if (!found || hit.distance < closest.distance)
      {
        closest = hit;
        found = true;
      }
    }

    return found;
  }

  private void LaserTick(bool hitSomething, RaycastHit hit)
  {
    if (hitSomething)
    {
      WeaponDamage.TryDamage(hit.collider, laserDamage);
    }
  }

  private LineRenderer CreateBeamLine(string name, float width, Color color)
  {
    GameObject lineObject = new GameObject(name);
    lineObject.transform.SetParent(transform, false);

    LineRenderer line = lineObject.AddComponent<LineRenderer>();
    line.useWorldSpace = true;
    line.positionCount = 2;
    line.material = new Material(Shader.Find("Sprites/Default"));
    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    line.receiveShadows = false;
    line.enabled = false;
    SetBeamStyle(line, width, color, color.a);

    return line;
  }

  private static void SetBeamStyle(LineRenderer line, float width, Color color, float alpha)
  {
    Color tinted = new Color(color.r, color.g, color.b, alpha);
    line.startWidth = width;
    line.endWidth = width * 0.6f;
    line.startColor = tinted;
    line.endColor = tinted;
  }

  private AudioSource CreateLaserAudio()
  {
    // Fonte própria para o laser, para poder cortar o som ao soltar o botão
    AudioSource source = laserLine.gameObject.AddComponent<AudioSource>();
    source.playOnAwake = false;
    source.loop = false;
    source.clip = laserSound;
    if (audioSource != null)
    {
      source.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
      source.spatialBlend = audioSource.spatialBlend;
      source.volume = audioSource.volume;
    }

    return source;
  }

  private void UpdateLaserAudio(bool laserActive)
  {
    // Toca uma vez só quando o laser começa a disparar
    if (laserActive && !wasLaserActive && laserSound != null)
    {
      laserAudio.clip = laserSound;
      laserAudio.Play();
    }
    else if (!laserActive && laserAudio.isPlaying)
    {
      laserAudio.Stop();
    }

    wasLaserActive = laserActive;
  }

  private void PlaySound(AudioClip clip)
  {
    if (audioSource != null && clip != null)
    {
      audioSource.PlayOneShot(clip);
    }
  }
}
