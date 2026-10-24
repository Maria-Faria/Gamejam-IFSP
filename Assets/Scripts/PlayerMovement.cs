using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
  [Tooltip("Velocidade de referência da animação: correr a runSpeed fica com a cadência de corrida.")]
  [SerializeField] private float speed = 5f;
  [SerializeField] private float runSpeed = 10f;
  [SerializeField] private float rotationSpeed = 720f;
  [SerializeField] private float gravity = -20f;

  [Header("Som de passos (toca em loop enquanto corre)")]
  [SerializeField] private AudioClip runSound;

  private float verticalVelocity;

  private CharacterController controller;
  private PlayerPowerUps powerUps;
  private PlayerHealth health;
  private PlayerStatus status;
  private AudioSource footstepAudio;

  public float WalkSpeed => speed;
  public float RunSpeed => runSpeed;
  public bool IsRunning { get; private set; }
  public bool IsMoving { get; private set; }
  // Velocidade atual considerando power-ups e lentidão
  public float CurrentSpeed => runSpeed * (powerUps != null ? powerUps.MoveSpeedMultiplier : 1f) * (status != null ? status.SpeedMultiplier : 1f);
  // Entrada extra somada ao teclado (usada pelos testes automáticos)
  public Vector2 SimulatedInput { get; set; }
  public bool IsInputBlocked => (health != null && health.IsDead) || (status != null && status.IsStunned);

  public void Start()
  {
    controller = GetComponent<CharacterController>();

    if (GetComponent<PlayerAnimation>() == null)
    {
      gameObject.AddComponent<PlayerAnimation>();
    }

    health = GetComponent<PlayerHealth>();
    if (health == null)
    {
      health = gameObject.AddComponent<PlayerHealth>();
    }

    status = GetComponent<PlayerStatus>();
    if (status == null)
    {
      status = gameObject.AddComponent<PlayerStatus>();
    }

    powerUps = GetComponent<PlayerPowerUps>();
    if (powerUps == null)
    {
      powerUps = gameObject.AddComponent<PlayerPowerUps>();
    }

    footstepAudio = CreateFootstepAudio();
  }

  public void Update()
  {
    float horizontal = Mathf.Clamp(Input.GetAxis("Horizontal") + SimulatedInput.x, -1f, 1f);
    float vertical = Mathf.Clamp(Input.GetAxis("Vertical") + SimulatedInput.y, -1f, 1f);

    Vector3 direction = new Vector3(horizontal, 0f, vertical);
    direction = Vector3.ClampMagnitude(direction, 1f);

    IsRunning = Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f || SimulatedInput != Vector2.zero;

    // Morta ou atordoada: ignora os comandos de movimento (gravidade e empurrão continuam)
    if (IsInputBlocked)
    {
      direction = Vector3.zero;
      IsRunning = false;
    }

    float statusMultiplier = status != null ? status.SpeedMultiplier : 1f;
    float currentSpeed = runSpeed * powerUps.MoveSpeedMultiplier * statusMultiplier;
    Vector3 knockback = status != null ? status.KnockbackVelocity : Vector3.zero;

    if (controller.isGrounded && verticalVelocity < 0f)
    {
      verticalVelocity = -2f;
    }
    verticalVelocity += gravity * Time.deltaTime;

    controller.Move((direction * currentSpeed + knockback + Vector3.up * verticalVelocity) * Time.deltaTime);

    if(direction.sqrMagnitude > 0.01f)
    {
      Quaternion targetRotation = Quaternion.LookRotation(direction);
      transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    UpdateFootsteps();
  }

  private AudioSource CreateFootstepAudio()
  {
    GameObject audioObject = new GameObject("FootstepAudio");
    audioObject.transform.SetParent(transform, false);

    AudioSource source = audioObject.AddComponent<AudioSource>();
    source.playOnAwake = false;
    source.loop = true;
    AudioSource playerAudio = GetComponent<AudioSource>();
    if (playerAudio != null)
    {
      source.outputAudioMixerGroup = playerAudio.outputAudioMixerGroup;
      source.spatialBlend = playerAudio.spatialBlend;
      source.volume = playerAudio.volume;
    }

    return source;
  }

  private void UpdateFootsteps()
  {
    Vector3 velocity = controller.velocity;
    velocity.y = 0f;
    IsMoving = velocity.sqrMagnitude > 0.25f && !health.IsDead;
    AudioClip clip = IsMoving ? runSound : null;

    if (clip == null)
    {
      if (footstepAudio.isPlaying) footstepAudio.Stop();
      return;
    }

    if (footstepAudio.clip != clip || !footstepAudio.isPlaying)
    {
      footstepAudio.clip = clip;
      footstepAudio.Play();
    }
  }
}
