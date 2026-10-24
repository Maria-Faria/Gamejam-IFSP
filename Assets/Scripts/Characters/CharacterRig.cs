using UnityEngine;

// O modelo rigado de uma cientista (prefab gerado por Roguelike/Build Characters): o Animator com a corrida
// em 8 direções (blend tree MoveX/MoveZ) e a camada "Aim" (braço direito erguido com a pistola), a pistola
// presa na mão direita e o cano de onde saem os tiros. O coice do tiro é aplicado por cima da animação:
// o braço sobe um pouco e a pistola recua na mão.
public class CharacterRig : MonoBehaviour
{
  private static readonly int MoveXParam = Animator.StringToHash("MoveX");
  private static readonly int MoveZParam = Animator.StringToHash("MoveZ");

  [SerializeField] private Animator animator;
  [SerializeField] private Transform rightArm;
  [SerializeField] private Transform gunSocket;
  [SerializeField] private Transform muzzle;
  [Tooltip("Altura da personagem já na escala do jogo (m).")]
  [SerializeField] private float height = 1.8f;

  [Header("Coice")]
  [SerializeField] private float recoilArmAngle = 22f;
  [SerializeField] private float recoilGunAngle = 28f;
  [SerializeField] private float recoilGunDistance = 0.06f;
  [SerializeField] private float recoilRecovery = 14f;

  private Vector3 socketPosition;
  private Quaternion socketRotation;
  private float recoil;
  private float aimWeight;
  private float aimTarget;
  private Vector2 move;

  public Animator Animator => animator;
  public Transform GunSocket => gunSocket;
  public Transform Muzzle => muzzle;
  public float Height => height;
  public float Recoil => recoil;
  public float AimWeight => aimWeight;
  public Vector2 Move => move;

  public void Awake()
  {
    if (gunSocket != null)
    {
      socketPosition = gunSocket.localPosition;
      socketRotation = gunSocket.localRotation;
    }
  }

  // Velocidade no espaço da personagem (x = direita, z = frente), em fração da velocidade de corrida
  public void SetLocomotion(Vector3 localVelocity, float dt)
  {
    Vector2 target = new Vector2(localVelocity.x, localVelocity.z);
    if (target.magnitude > 1f) target.Normalize();
    move = Vector2.Lerp(move, target, 1f - Mathf.Exp(-12f * dt));
    if (animator == null) return;
    animator.SetFloat(MoveXParam, move.x);
    animator.SetFloat(MoveZParam, move.y);
    // passos mais rápidos quanto mais rápido ela anda (ex.: power-up de velocidade)
    animator.speed = Mathf.Clamp(0.85f + 0.35f * localVelocity.magnitude, 1f, 1.6f);
  }

  // 1 = braço erguido com a pistola para atirar, 0 = pistola baixa
  public void SetAim(bool aiming)
  {
    aimTarget = aiming ? 1f : 0f;
  }

  public void Kick(float strength)
  {
    recoil = Mathf.Min(1.5f, recoil + strength);
    aimWeight = Mathf.Max(aimWeight, 0.8f);
  }

  public void LateUpdate()
  {
    float dt = Time.deltaTime;
    aimWeight = Mathf.MoveTowards(aimWeight, aimTarget, dt * (aimTarget > aimWeight ? 9f : 3f));
    recoil = Mathf.Lerp(recoil, 0f, 1f - Mathf.Exp(-recoilRecovery * dt));
    if (animator != null && animator.layerCount > 1) animator.SetLayerWeight(1, aimWeight);

    // coice por cima da pose: o braço sobe, a pistola empina e recua na mão
    if (rightArm != null && recoil > 0.001f)
    {
      rightArm.rotation = Quaternion.AngleAxis(-recoilArmAngle * recoil * aimWeight, transform.right) * rightArm.rotation;
    }
    if (gunSocket != null)
    {
      gunSocket.localPosition = socketPosition;
      gunSocket.localRotation = socketRotation;
      if (recoil > 0.001f)
      {
        gunSocket.rotation = Quaternion.AngleAxis(-recoilGunAngle * recoil, gunSocket.right) * gunSocket.rotation;
        gunSocket.position -= gunSocket.forward * recoilGunDistance * recoil;
      }
    }
  }
}
