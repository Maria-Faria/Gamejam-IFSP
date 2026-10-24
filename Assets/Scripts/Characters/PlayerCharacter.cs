using UnityEngine;
using UnityEngine.SceneManagement;

// Põe a cientista escolhida no menu (CharacterSelect) no lugar do modelo da jogadora: o modelo rigado com a
// pistola na mão direita, os atributos dela (CharacterStats) e a carteira de moedas (PlayerWallet). A cada
// quadro alimenta a animação: a corrida em 8 direções pela velocidade relativa a para onde ela olha, a
// pistola erguida enquanto atira e o coice a cada tiro. Os tiros saem do cano da pistola.
// É acrescentado sozinho à jogadora de toda cena (quem tem PlayerMovement e não é uma parceira do co-op).
[DefaultExecutionOrder(-50)]
public class PlayerCharacter : MonoBehaviour
{
  [Tooltip("Segundos que a pistola fica erguida depois do último tiro.")]
  [SerializeField] private float aimHold = 0.7f;

  private CharacterRig rig;
  private CharacterStats stats;
  private PlayerShooting shooting;
  private PlayerMovement movement;
  private CharacterController controller;
  private PlayerHealth health;
  private Transform shotOrigin;

  public CharacterRig Rig => rig;
  public int CharacterIndex => stats != null ? stats.CharacterIndex : 0;

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  private static void Install()
  {
    SceneManager.sceneLoaded -= OnSceneLoaded;
    SceneManager.sceneLoaded += OnSceneLoaded;
    AttachToPlayers();
  }

  private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToPlayers();

  public static void AttachToPlayers()
  {
    foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>())
    {
      if (player.GetComponent<PlayerCharacter>() == null && player.GetComponent<GuardiasDaLuz.CoopRemoteAvatar>() == null)
      {
        player.gameObject.AddComponent<PlayerCharacter>();
      }
    }
  }

  public void Awake()
  {
    stats = GetComponent<CharacterStats>();
    if (stats == null) stats = gameObject.AddComponent<CharacterStats>();
    if (GetComponent<PlayerWallet>() == null) gameObject.AddComponent<PlayerWallet>();
    shooting = GetComponent<PlayerShooting>();
    movement = GetComponent<PlayerMovement>();
    controller = GetComponent<CharacterController>();
    health = GetComponent<PlayerHealth>();
    Use(CharacterRoster.Selected);
  }

  // Troca para a personagem `index` (também usado pelos testes)
  public void Use(int index)
  {
    stats.Choose(index);
    CharacterRig prefab = CharacterRoster.LoadPrefab(index);
    if (prefab == null)
    {
      return;      // sem os modelos gerados (Roguelike/Build Characters): fica o modelo da cena
    }

    Transform old = transform.Find("Model");
    if (rig != null && old == rig.transform) old = null;
    if (rig != null) Destroy(rig.gameObject);
    if (old != null)
    {
      old.name = "Model_Antigo";
      old.gameObject.SetActive(false);
      Destroy(old.gameObject);
    }

    rig = Instantiate(prefab, transform, false);
    rig.name = "Model";
    rig.transform.localPosition = Vector3.zero;
    rig.transform.localRotation = Quaternion.identity;

    // a caixa que fazia as vezes de arma some; o cano (Muzzle) passa a seguir a pistola
    Transform oldGun = transform.Find("Gun");
    if (oldGun != null)
    {
      foreach (Renderer r in oldGun.GetComponentsInChildren<Renderer>()) r.enabled = false;
    }
    if (shooting != null && shooting.Muzzle != null)
    {
      shotOrigin = shooting.Muzzle;
      shotOrigin.SetParent(transform, true);
    }

    PlayerAnimation animation = GetComponent<PlayerAnimation>();
    if (animation != null) animation.UseModel(rig.transform, null, rig.Animator);
    UpdateShotOrigin();
  }

  public void OnEnable()
  {
    if (shooting == null) shooting = GetComponent<PlayerShooting>();
    if (shooting != null) shooting.Fired += OnFired;
  }

  public void OnDisable()
  {
    if (shooting != null) shooting.Fired -= OnFired;
  }

  private void OnFired(WeaponType weapon)
  {
    if (rig != null) rig.Kick(KickOf(weapon));
  }

  public static float KickOf(WeaponType weapon)
  {
    switch (weapon)
    {
      case WeaponType.Shotgun: return 1.2f;
      case WeaponType.Exploding: return 1.4f;
      case WeaponType.EightWayBurst: return 0.8f;
      case WeaponType.RapidFire: return 0.3f;
      case WeaponType.SineWave: return 0.45f;
      default: return 0.7f;
    }
  }

  public void Update()
  {
    if (rig == null) return;
    bool dead = health != null && health.IsDead;

    // velocidade no espaço dela, em fração da velocidade de corrida: escolhe a corrida certa das 8
    Vector3 velocity = controller != null ? controller.velocity : Vector3.zero;
    velocity.y = 0f;
    float runSpeed = movement != null ? Mathf.Max(1f, movement.CurrentSpeed) : 10f;
    Vector3 local = dead ? Vector3.zero : transform.InverseTransformDirection(velocity) / runSpeed;
    rig.SetLocomotion(local, Time.deltaTime);

    bool aiming = !dead && shooting != null && (shooting.IsFiring || Time.time - shooting.LastShotTime < aimHold);
    rig.SetAim(aiming);
  }

  public void LateUpdate()
  {
    UpdateShotOrigin();
  }

  // Os tiros saem do cano da pistola quando ela está erguida; sempre na direção em que ela olha
  private void UpdateShotOrigin()
  {
    if (shotOrigin == null || rig == null) return;
    Vector3 forward = transform.forward;
    forward.y = 0f;
    if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
    Vector3 chest = transform.position + Vector3.up * rig.Height * 0.48f + forward.normalized * 0.45f;
    Vector3 tip = rig.Muzzle != null ? rig.Muzzle.position : chest;
    Vector3 origin = Vector3.Lerp(chest, tip, rig.AimWeight);
    origin.y = Mathf.Max(origin.y, transform.position.y + rig.Height * 0.3f);
    shotOrigin.SetPositionAndRotation(origin, Quaternion.LookRotation(forward.normalized, Vector3.up));
  }
}
