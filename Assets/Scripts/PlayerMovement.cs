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
  private AudioSource footstepAudio;

  public float WalkSpeed => speed;
  public float RunSpeed => runSpeed;
  public bool IsRunning { get; private set; }
  public bool IsMoving { get; private set; }

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

    if (GetComponent<PlayerMana>() == null)
    {
      gameObject.AddComponent<PlayerMana>();
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
    float horizontal = Input.GetAxis("Horizontal");
    float vertical = Input.GetAxis("Vertical");

    Vector3 direction = new Vector3(horizontal, 0f, vertical);
    direction = Vector3.ClampMagnitude(direction, 1f);

    IsRunning = Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f;
    float currentSpeed = runSpeed * powerUps.MoveSpeedMultiplier;

    if (controller.isGrounded && verticalVelocity < 0f)
    {
      verticalVelocity = -2f;
    }
    verticalVelocity += gravity * Time.deltaTime;

    controller.Move((direction * currentSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);

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
