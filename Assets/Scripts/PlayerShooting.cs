using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
  [SerializeField] private GameObject bulletPrefab;
  [SerializeField] private Transform muzzle;
  [SerializeField] private float fireRate = 6f;
  [SerializeField] private AudioSource audioSource;
  [SerializeField] private AudioClip shootSound;

  private float nextFireTime;

  public void Update()
  {
    if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
    {
      nextFireTime = Time.time + 1f / fireRate;
      Shoot();
    }
  }

  private void Shoot()
  {
    if (bulletPrefab == null || muzzle == null)
    {
      return;
    }

    Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);

    if (audioSource != null && shootSound != null)
    {
      audioSource.PlayOneShot(shootSound);
    }
  }
}
