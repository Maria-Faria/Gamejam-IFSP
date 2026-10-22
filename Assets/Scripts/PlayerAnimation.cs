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

  private static readonly int SpeedParam = Animator.StringToHash("Speed");
  private static readonly int IsRunningParam = Animator.StringToHash("IsRunning");
  private static readonly int ShootParam = Animator.StringToHash("Shoot");

  private PlayerMovement movement;
  private PlayerShooting shooting;
  private CharacterController controller;

  private Vector3 modelBasePosition;
  private Quaternion modelBaseRotation;
  private Vector3 modelBaseScale;
  private Vector3 gunBasePosition;

  private float moveAmount;
  private float stepPhase;
  private float recoil;

  private bool hasSpeedParam;
  private bool hasRunningParam;
  private bool hasShootParam;

  public void Awake()
  {
    movement = GetComponent<PlayerMovement>();
    shooting = GetComponent<PlayerShooting>();
    controller = GetComponent<CharacterController>();

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
    }

    CacheAnimatorParameters();
  }

  public void OnEnable()
  {
    if (shooting != null) shooting.Fired += OnFired;
  }

  public void OnDisable()
  {
    if (shooting != null) shooting.Fired -= OnFired;
  }

  public void LateUpdate()
  {
    float dt = Time.deltaTime;

    Vector3 velocity = controller != null ? controller.velocity : Vector3.zero;
    velocity.y = 0f;
    float speed = velocity.magnitude;
    float walkSpeed = movement != null ? movement.WalkSpeed : 5f;

    float targetMove = speed > 0.1f ? Mathf.Clamp(speed / walkSpeed, 0f, 2f) : 0f;
    moveAmount = Mathf.Lerp(moveAmount, targetMove, 1f - Mathf.Exp(-blendSpeed * dt));
    recoil = Mathf.Lerp(recoil, 0f, 1f - Mathf.Exp(-recoilRecovery * dt));

    float cadence = moveAmount <= 1f ? moveAmount : 1f + (moveAmount - 1f) * 0.5f;
    stepPhase += dt * stepFrequency * Mathf.PI * cadence;

    float walkBlend = Mathf.Clamp01(moveAmount);
    float bob = Mathf.Abs(Mathf.Sin(stepPhase)) * bobHeight * moveAmount;
    float sway = Mathf.Sin(stepPhase) * swayAngle * walkBlend;
    float lean = leanAngle * moveAmount - recoil * recoilBodyAngle;
    float breath = Mathf.Sin(Time.time * breathSpeed * 2f * Mathf.PI) * breathAmount * (1f - walkBlend);

    if (model != null)
    {
      model.localPosition = modelBasePosition + Vector3.up * bob;
      model.localRotation = Quaternion.Euler(lean, 0f, sway) * modelBaseRotation;
      model.localScale = Vector3.Scale(modelBaseScale, new Vector3(1f - breath * 0.5f, 1f + breath, 1f - breath * 0.5f));
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
  }

  private void OnFired(WeaponType weapon)
  {
    recoil = Mathf.Min(recoil + GetRecoilStrength(weapon), 1.5f);

    if (animator != null && hasShootParam)
    {
      animator.SetTrigger(ShootParam);
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
