using UnityEngine;

public class LightBullet : MonoBehaviour
{
  [SerializeField] private float speed = 20f;
  [SerializeField] private float lifetime = 3f;
  [SerializeField] private float damage = 10f;
  [SerializeField] private float hitRadius = 0.3f;

  public void Start()
  {
    Destroy(gameObject, lifetime);
  }

  public void Update()
  {
    float step = speed * Time.deltaTime;
    RaycastHit[] hits = Physics.SphereCastAll(transform.position, hitRadius, transform.forward, step, ~0, QueryTriggerInteraction.Ignore);
    foreach (RaycastHit hit in hits)
    {
      if (WeaponDamage.TryDamage(hit.collider, damage))
      {
        Destroy(gameObject);
        return;
      }
    }

    transform.position += transform.forward * step;
  }

  public void OnTriggerEnter(Collider other)
  {
    if (other.GetComponentInParent<PlayerMovement>() != null || other.GetComponent<LightBullet>() != null)
    {
      return;
    }

    WeaponDamage.TryDamage(other, damage);
    Destroy(gameObject);
  }
}
