using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
  [Header("Movimento")]
  public float walkSpeed = 3f;
  public float runSpeed = 6f;
  public float gravity = -20f;

  [Header("Tiro")]
  public GameObject bulletPrefab;
  public Transform firePoint;
  public float bulletSpeed = 30f;
  public float fireRate = 0.25f;

  CharacterController controller;
  Vector3 velocity;
  float nextFireTime;

  public void Awake()
  {
    controller = GetComponent<CharacterController>();
  }
}
