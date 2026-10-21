using UnityEngine;

public class LightBullet : MonoBehaviour
{
  [SerializeField] private float speed = 20f;
  [SerializeField] private float lifetime = 3f;

  public void Start()
  {
    Destroy(gameObject, lifetime);
  }

  public void Update()
  {
    transform.position += transform.forward * speed * Time.deltaTime;
  }

  public void OnTriggerEnter(Collider other)
  {
    if (other.GetComponentInParent<PlayerMovement>() != null || other.GetComponent<LightBullet>() != null)
    {
      return;
    }

    Destroy(gameObject);
  }
}
