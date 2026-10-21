using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
  [SerializeField] private float speed = 5f;
  [SerializeField] private float runSpeed = 10f;
  [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
  [SerializeField] private float rotationSpeed = 720f;
  [SerializeField] private float gravity = -20f;

  private float verticalVelocity;

  private CharacterController controller;

  public float WalkSpeed => speed;
  public float RunSpeed => runSpeed;
  public bool IsRunning { get; private set; }

  public void Start()
  {
    controller = GetComponent<CharacterController>();

    // Garante que o personagem seja animado mesmo sem adicionar o componente na cena
    if (GetComponent<PlayerAnimation>() == null)
    {
      gameObject.AddComponent<PlayerAnimation>();
    }
  }

  public void Update()
  {
    float horizontal = Input.GetAxis("Horizontal");
    float vertical = Input.GetAxis("Vertical");

    Vector3 direction = new Vector3(horizontal, 0f, vertical);
    direction = Vector3.ClampMagnitude(direction, 1f);

    IsRunning = Input.GetKey(runKey) && direction.sqrMagnitude > 0.01f;
    float currentSpeed = Input.GetKey(runKey) ? runSpeed : speed;

    controller.Move(direction * currentSpeed * Time.deltaTime);

    if(direction.sqrMagnitude > 0.01f)
    {
      Quaternion targetRotation = Quaternion.LookRotation(direction);
      transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
  }
}
