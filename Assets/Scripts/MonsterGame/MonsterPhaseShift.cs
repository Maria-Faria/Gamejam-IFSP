using System.Collections;
using System.Collections.Generic;
using Roguelike;
using UnityEngine;

// Versão "fantasma" do Ghost e do Ghoul: de tempos em tempos some (vai ficando transparente), fica
// invisível, intocável e sem atacar enquanto troca de lugar — de preferência pelas costas da jogadora —
// e reaparece com um sopro de fumaça e um brilho de aviso antes de poder atacar de novo.
[RequireComponent(typeof(Monster))]
public class MonsterPhaseShift : MonoBehaviour
{
  private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

  [Tooltip("Segundos visível (sorteado) entre um sumiço e outro.")]
  [SerializeField] private Vector2 visibleSeconds = new Vector2(3.5f, 5.5f);
  [Tooltip("Segundos invisível (sorteado).")]
  [SerializeField] private Vector2 hiddenSeconds = new Vector2(1.2f, 2f);
  [SerializeField] private float fadeSeconds = 0.4f;
  [Tooltip("Segundos de aviso ao reaparecer, ainda sem poder atacar.")]
  [SerializeField] private float tellSeconds = 0.45f;
  [Tooltip("Distância da jogadora (m) onde ele reaparece (sorteada).")]
  [SerializeField] private Vector2 reappearDistance = new Vector2(3f, 5f);
  [Range(0f, 1f)] [SerializeField] private float hiddenOpacity = 0.03f;

  private Monster monster;
  private readonly List<Renderer> renderers = new List<Renderer>();
  private readonly List<float> baseOpacity = new List<float>();
  private readonly List<Collider> colliders = new List<Collider>();
  private GhostlyLook look;
  private float opacity = 1f;

  public bool IsHidden { get; private set; }
  // Visível de novo mas ainda no aviso: não ataca.
  public bool IsReappearing { get; private set; }
  public int Cycles { get; private set; }

  public void Awake()
  {
    monster = GetComponent<Monster>();
    look = GetComponent<GhostlyLook>();
    foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
    {
      Material material = r.sharedMaterial;
      if (material == null || !material.HasProperty(OpacityId)) continue;
      renderers.Add(r);
      baseOpacity.Add(material.GetFloat(OpacityId));
    }
    colliders.AddRange(GetComponentsInChildren<Collider>(true));
  }

  public void OnEnable()
  {
    StartCoroutine(Cycle());
  }

  public void OnDisable()
  {
    SetHidden(false);
    SetOpacity(1f);
  }

  // Para os testes: começa o próximo sumiço já.
  public void PhaseNow()
  {
    StopAllCoroutines();
    StartCoroutine(Cycle(0f));
  }

  private IEnumerator Cycle(float firstWait = -1f)
  {
    float wait = firstWait >= 0f ? firstWait : Random.Range(visibleSeconds.x, visibleSeconds.y) * 0.6f;
    while (true)
    {
      yield return new WaitForSeconds(wait);
      while (monster.Busy && !monster.Dead) yield return null;
      if (monster.Dead) yield break;

      // some
      SetHidden(true);
      EnemyBullet.SpawnSmokePuff(monster.Center, monster.Size * 0.45f);
      yield return Fade(1f, 0f);

      // troca de lugar invisível
      yield return new WaitForSeconds(Random.Range(hiddenSeconds.x, hiddenSeconds.y));
      if (monster.Dead) yield break;
      Relocate();

      // aviso: fumaça e brilho, depois volta a ser atingível
      IsReappearing = true;
      EnemyBullet.SpawnSmokePuff(monster.Center, monster.Size * 0.55f);
      if (look != null) look.SetGlow(2.5f);
      yield return Fade(0f, 1f);
      // já pode ser atingido, mas o cérebro segura o ataque até o fim do aviso
      MonsterBrain brain = monster.Brain;
      bool brainWasOn = brain != null && brain.enabled;
      if (brain != null) brain.enabled = false;
      SetHidden(false);
      IsReappearing = true;
      yield return new WaitForSeconds(tellSeconds);
      if (brain != null && brainWasOn) brain.enabled = true;
      if (look != null) look.SetGlow(1f);
      IsReappearing = false;
      Cycles++;

      wait = Random.Range(visibleSeconds.x, visibleSeconds.y);
    }
  }

  private IEnumerator Fade(float from, float to)
  {
    for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
    {
      SetOpacity(Mathf.Lerp(from, to, t / fadeSeconds));
      yield return null;
    }
    SetOpacity(to);
  }

  private void SetHidden(bool hidden)
  {
    IsHidden = hidden;
    if (monster == null) return;
    monster.Untargetable = hidden;
    if (monster.Health != null) monster.Health.Invulnerable = hidden;
    SetCollidersEnabled(!hidden);
    if (!hidden) IsReappearing = false;
  }

  private void SetCollidersEnabled(bool on)
  {
    foreach (Collider c in colliders)
    {
      if (c != null) c.enabled = on;
    }
  }

  private void SetOpacity(float visible)
  {
    opacity = visible;
    MaterialPropertyBlock block = new MaterialPropertyBlock();
    for (int i = 0; i < renderers.Count; i++)
    {
      Renderer r = renderers[i];
      if (r == null) continue;
      r.GetPropertyBlock(block);
      block.SetFloat(OpacityId, Mathf.Lerp(hiddenOpacity, baseOpacity[i], visible));
      r.SetPropertyBlock(block);
    }
  }

  public float Opacity => opacity;

  // Reaparece perto da jogadora (de preferência atrás dela), num lugar livre e com visão até ela.
  private void Relocate()
  {
    Transform target = monster.Target;
    if (target == null) return;

    Vector3 behind = -target.forward;
    behind.y = 0f;
    for (int i = 0; i < 16; i++)
    {
      float spread = i < 6 ? 70f : 180f;
      Vector3 dir = Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * (behind.sqrMagnitude > 0.01f ? behind.normalized : Vector3.forward);
      Vector3 spot = target.position + dir * Random.Range(reappearDistance.x, reappearDistance.y);
      spot.y = transform.position.y;
      Vector3 body = spot + Vector3.up * monster.Size * 0.5f;
      if (Physics.CheckSphere(body, monster.Size * 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
      if (Physics.Linecast(target.position + Vector3.up, body, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
          && !hit.collider.transform.IsChildOf(target)) continue;

      transform.position = spot;
      Vector3 face = target.position - spot;
      face.y = 0f;
      if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
      return;
    }
  }
}
