using System.Collections.Generic;
using UnityEngine;

public class ShadowEnemy : MonoBehaviour
{
  [SerializeField] private float health = 15f;
  [SerializeField] private float hitFlashDuration = 0.1f;
  [Tooltip("Chance (0 a 1) de soltar um power-up ao morrer.")]
  [Range(0f, 1f)] [SerializeField] private float powerUpDropChance = 0.08f;
  [SerializeField] private float moveSpeed = 3.2f;
  [SerializeField] private float fleeSpeed = 5f;
  [SerializeField] private float preferredDistance = 5f;
  [SerializeField] private float shootRange = 16f;
  [SerializeField] private float minFireInterval = 4.5f;
  [SerializeField] private float maxFireInterval = 5.5f;
  [SerializeField] private float bulletSpeed = 9f;
  [SerializeField] private float bulletDamage = 3f;
  [Tooltip("Nível de luz a partir do qual o inimigo foge e se recusa a entrar.")]
  [SerializeField] private float lightFearThreshold = 1f;
  [Tooltip("Quão rápido o inimigo muda de velocidade; valores menores deixam o movimento mais suave.")]
  [SerializeField] private float acceleration = 12f;
  [Tooltip("Distância à frente em que o inimigo procura paredes para desviar.")]
  [SerializeField] private float wallProbeDistance = 1.2f;

  private static readonly float[] SteerAngles = { 0f, 45f, -45f, 90f, -90f };

  private CharacterController controller;
  private Transform body;
  private Material[] bodyMaterials = new Material[0];
  private Color[] bodyColors;
  private Color[] bodyEmissions;
  private float flashTimer;
  private Transform target;
  private Collider targetCollider;
  private float bobPhase;
  private float orbitSign;
  private float nextFireTime;
  private Vector3 planarVelocity;
  private Vector3 wallNormal;
  private float lastWallHitTime = -1f;

  public void Init(Transform player, float fearThreshold)
  {
    target = player;
    targetCollider = player != null ? player.GetComponent<Collider>() : null;
    lightFearThreshold = fearThreshold;
  }

  public void Start()
  {
    controller = GetComponent<CharacterController>();
    body = transform.Find("Corpo");
    if (body != null)
    {
      List<Material> materials = new List<Material>();
      foreach (Renderer bodyRenderer in body.GetComponentsInChildren<Renderer>())
      {
        materials.AddRange(bodyRenderer.materials);
      }
      bodyMaterials = materials.ToArray();
    }

    bodyColors = new Color[bodyMaterials.Length];
    bodyEmissions = new Color[bodyMaterials.Length];
    for (int i = 0; i < bodyMaterials.Length; i++)
    {
      Material material = bodyMaterials[i];
      bodyColors[i] = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
      bodyEmissions[i] = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
      material.EnableKeyword("_EMISSION");
    }

    bobPhase = Random.value * Mathf.PI * 2f;
    orbitSign = Random.value < 0.5f ? -1f : 1f;
    nextFireTime = Time.time + Random.Range(minFireInterval, maxFireInterval);
  }

  public void Update()
  {
    if (body != null)
    {
      float t = Time.time * 2.2f + bobPhase;
      body.localPosition = Vector3.up * Mathf.Sin(t) * 0.12f;
      body.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f) * 6f, Mathf.Sin(t * 0.5f) * 10f, Mathf.Cos(t * 0.9f) * 6f);
    }

    if (flashTimer > 0f)
    {
      flashTimer -= Time.deltaTime;
      if (flashTimer <= 0f)
      {
        SetFlash(false);
      }
    }

    if (target == null)
    {
      return;
    }

    Move();
    TryShoot();
  }

  public void TakeDamage(float damage)
  {
    if (health <= 0f)
    {
      return;
    }

    health -= damage;
    if (health <= 0f)
    {
      PowerUpPickup.TrySpawn(transform.position, powerUpDropChance);
      Destroy(gameObject);
      return;
    }

    flashTimer = hitFlashDuration;
    SetFlash(true);
  }

  private void SetFlash(bool on)
  {
    for (int i = 0; i < bodyMaterials.Length; i++)
    {
      Material material = bodyMaterials[i];
      if (material.HasProperty("_BaseColor"))
      {
        material.SetColor("_BaseColor", on ? Color.white : bodyColors[i]);
      }
      else
      {
        material.color = on ? Color.white : bodyColors[i];
      }
      material.SetColor("_EmissionColor", on ? Color.white * 2f : bodyEmissions[i]);
    }
  }

  private void OnDestroy()
  {
    foreach (Material material in bodyMaterials)
    {
      Destroy(material);
    }
  }

  private void Move()
  {
    Vector3 position = transform.position;
    Vector3 toPlayer = target.position - position;
    toPlayer.y = 0f;
    float distance = toPlayer.magnitude;
    Vector3 toPlayerDir = distance > 0.01f ? toPlayer / distance : transform.forward;

    if (distance > 0.01f)
    {
      transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayerDir), 8f * Time.deltaTime);
    }

    Vector3 desiredVelocity;
    if (LightField.Sample(position, out Vector3 away) > lightFearThreshold)
    {
      Vector3 fleeDirection = FindOpenDirection(position, away, false);
      desiredVelocity = (fleeDirection != Vector3.zero ? fleeDirection : away) * fleeSpeed;
    }
    else
    {
      Vector3 desired = distance > preferredDistance
        ? toPlayerDir
        : Vector3.Cross(Vector3.up, toPlayerDir) * orbitSign;
      Vector3 direction = FindOpenDirection(position, desired, true);
      if (direction == Vector3.zero && distance <= preferredDistance)
      {
        orbitSign = -orbitSign;
      }
      desiredVelocity = direction * moveSpeed;
    }

    if (Time.time - lastWallHitTime < 0.2f && Vector3.Dot(desiredVelocity, wallNormal) < 0f)
    {
      desiredVelocity = Vector3.ProjectOnPlane(desiredVelocity, wallNormal);
    }

    planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, acceleration * Time.deltaTime);
    Vector3 velocity = planarVelocity;
    float targetHeight = targetCollider != null ? targetCollider.bounds.center.y : target.position.y + 1f;
    velocity.y = Mathf.Clamp((targetHeight - position.y) * 6f, -6f, 6f);

    controller.Move(velocity * Time.deltaTime);
  }

  private Vector3 FindOpenDirection(Vector3 position, Vector3 desired, bool avoidLight)
  {
    foreach (float angle in SteerAngles)
    {
      Vector3 direction = Quaternion.Euler(0f, angle, 0f) * desired;
      if (avoidLight && LightField.Sample(position + direction * 1.5f, out _) > lightFearThreshold)
      {
        continue;
      }
      if (IsBlocked(position, direction))
      {
        continue;
      }
      return direction;
    }

    return Vector3.zero;
  }

  private bool IsBlocked(Vector3 position, Vector3 direction)
  {
    float radius = controller.radius * 0.9f;
    return Physics.SphereCast(position, radius, direction, out RaycastHit hit, wallProbeDistance, ~0, QueryTriggerInteraction.Ignore)
      && hit.collider != controller
      && (target == null || !hit.transform.IsChildOf(target))
      && hit.collider.GetComponentInParent<ShadowEnemy>() == null;
  }

  private void OnControllerColliderHit(ControllerColliderHit hit)
  {
    Vector3 normal = hit.normal;
    normal.y = 0f;
    if (normal.sqrMagnitude < 0.01f)
    {
      return;
    }

    wallNormal = normal.normalized;
    lastWallHitTime = Time.time;
  }

  private void TryShoot()
  {
    if (Time.time < nextFireTime)
    {
      return;
    }
    nextFireTime = Time.time + Random.Range(minFireInterval, maxFireInterval);

    Vector3 aim = targetCollider != null ? targetCollider.bounds.center : target.position + Vector3.up;
    Vector3 origin = transform.position;
    Vector3 toAim = aim - origin;
    float distance = toAim.magnitude;
    if (distance > shootRange)
    {
      return;
    }

    Vector3 direction = toAim / distance;
    if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore)
      && !hit.transform.IsChildOf(target))
    {
      return;
    }

    EnemyBullet.Spawn(origin + direction * 0.7f, direction, bulletSpeed, bulletDamage);
  }
}
