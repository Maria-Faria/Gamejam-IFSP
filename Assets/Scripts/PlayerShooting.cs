using GuardiasDaLuz.MapSystem;
using UnityEngine;

public enum WeaponType
{
  LightPistol,
  Shotgun,
  Laser
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

  private float nextFireTime;
  private LineRenderer laserLine;
  private AudioSource laserAudio;
  private bool wasLaserActive;

  public WeaponType CurrentWeapon => currentWeapon;

  public void Awake()
  {
    laserLine = CreateLaserLine();
    laserAudio = CreateLaserAudio();
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

    if (firing && Time.time >= nextFireTime)
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
    if (Input.GetKeyDown(KeyCode.Alpha1)) SetWeapon(WeaponType.LightPistol);
    if (Input.GetKeyDown(KeyCode.Alpha2)) SetWeapon(WeaponType.Shotgun);
    if (Input.GetKeyDown(KeyCode.Alpha3)) SetWeapon(WeaponType.Laser);

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
    if (!hitSomething)
    {
      return;
    }

    StandalonePlaceholderMonsterTestAI monster = hit.collider.GetComponentInParent<StandalonePlaceholderMonsterTestAI>();
    if (monster != null)
    {
      monster.TakeDamage(laserDamage);
      return;
    }

    StandaloneBreakableCrate crate = hit.collider.GetComponentInParent<StandaloneBreakableCrate>();
    if (crate != null)
    {
      crate.TakeHit();
      return;
    }

    StandaloneSecretCrackedWall wall = hit.collider.GetComponentInParent<StandaloneSecretCrackedWall>();
    if (wall != null)
    {
      wall.RegisterProjectileHit();
    }
  }

  private LineRenderer CreateLaserLine()
  {
    GameObject lineObject = new GameObject("LaserBeam");
    lineObject.transform.SetParent(transform, false);

    LineRenderer line = lineObject.AddComponent<LineRenderer>();
    line.useWorldSpace = true;
    line.positionCount = 2;
    line.startWidth = laserWidth;
    line.endWidth = laserWidth * 0.6f;
    line.material = new Material(Shader.Find("Sprites/Default"));
    line.startColor = laserColor;
    line.endColor = laserColor;
    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    line.receiveShadows = false;
    line.enabled = false;

    return line;
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
