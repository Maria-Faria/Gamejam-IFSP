using UnityEngine;

public class CameraFollow : MonoBehaviour
{
  [SerializeField] private Transform target;
  [SerializeField] private Vector3 offset = new Vector3(0f, 8f, -8f);
  [SerializeField] private float smoothTime = 0.15f;

  private Vector3 velocity;

  public void Start()
  {
    if (target == null)
    {
      PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
      if (player != null)
      {
        target = player.transform;
      }
    }
  }

  public void LateUpdate()
  {
    if (target == null)
    {
      return;
    }

    Vector3 desiredPosition = target.position + offset;
    transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
    transform.LookAt(target.position);
  }
}
