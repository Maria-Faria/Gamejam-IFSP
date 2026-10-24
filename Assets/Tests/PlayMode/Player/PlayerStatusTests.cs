using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Testes de PlayMode da jogadora (status, animações e morte) na cena do jogo.
// Os scripts do jogo ficam no Assembly-CSharp, que um asmdef não pode referenciar,
// então os componentes são acessados por reflexão (pelo nome do tipo).
public class PlayerStatusTests
{
  private const string GameScene = "SampleScene";

  private GameObject player;
  private Component status;
  private Component health;
  private Component movement;
  private Component shooting;
  private Component animation;

  [UnitySetUp]
  public IEnumerator SetUp()
  {
    // Em batch mode os quadros são curtíssimos: passos menores que o minMoveDistance do CharacterController
    // seriam ignorados e a jogadora lenta quase não andaria. 60 fps como no jogo.
    Application.targetFrameRate = 60;
    yield return LoadGameScene();
  }

  // Descarrega a cena do jogo, para a jogadora e o mapa não sobrarem nos testes seguintes
  [UnityTearDown]
  public IEnumerator TearDown()
  {
    Application.targetFrameRate = -1;
    Time.timeScale = 1f;
    Type result = Type.GetType("GameResult, Assembly-CSharp");
    if (result != null)
    {
      foreach (UnityEngine.Object screen in UnityEngine.Object.FindObjectsByType(result)) UnityEngine.Object.Destroy(((Component)screen).gameObject);
    }
    Scene game = SceneManager.GetSceneByName(GameScene);
    if (!game.IsValid() || !game.isLoaded)
    {
      yield break;
    }
    Scene empty = SceneManager.CreateScene("DepoisDosTestesDaJogadora" + UnityEngine.Random.Range(0, 99999));
    SceneManager.SetActiveScene(empty);
    yield return SceneManager.UnloadSceneAsync(game);
  }

  [UnityTest]
  public IEnumerator Of_FindsStatusFromAnyPartOfPlayer()
  {
    Type type = GameType("PlayerStatus");
    MethodInfo of = type.GetMethod("Of", BindingFlags.Public | BindingFlags.Static);
    Transform model = player.transform.Find("Model");
    Assert.IsNotNull(model, "jogadora sem filho Model");

    Assert.AreSame(status, of.Invoke(null, new object[] { model }));
    Assert.AreSame(status, of.Invoke(null, new object[] { player.GetComponent<CharacterController>() }));
    Assert.IsNull(of.Invoke(null, new object[] { Camera.main }));
    yield return null;
  }

  [UnityTest]
  public IEnumerator Stun_BlocksMovementAndShootingForItsDuration()
  {
    float free = 0f;
    yield return MeasureMove(Vector2.right, 0.3f, d => free = d);
    yield return MeasureMove(Vector2.left, 0.3f, null);
    Assert.Greater(free, 1f, "deveria andar sem atordoamento");

    Call(status, "Stun", 0.8f);
    Assert.IsTrue(Get<bool>(status, "IsStunned"));
    Call(status, "Stun", 0.2f); // mais curto não reduz
    Assert.Greater(Get<float>(status, "StunRemaining"), 0.7f);

    float stunned = -1f;
    Set(shooting, "SimulatedFire", true);
    int bulletsBefore = CountObjects("LightBullet");
    yield return MeasureMove(Vector2.right, 0.4f, d => stunned = d);
    Assert.IsFalse(Get<bool>(shooting, "IsFiring"), "não pode atirar atordoada");
    Assert.AreEqual(bulletsBefore, CountObjects("LightBullet"), "nenhum tiro deveria sair atordoada");
    Assert.Less(stunned, 0.05f, "não pode andar atordoada");

    yield return new WaitForSeconds(0.5f);
    Assert.IsFalse(Get<bool>(status, "IsStunned"));

    float after = 0f;
    yield return MeasureMove(Vector2.right, 0.3f, d => after = d);
    Assert.IsTrue(Get<bool>(shooting, "IsFiring"), "deveria voltar a atirar");
    Set(shooting, "SimulatedFire", false);
    Assert.Greater(after, 1f, "deveria voltar a andar");
  }

  [UnityTest]
  public IEnumerator Poison_DealsDpsTimesSecondsThroughPlayerHealth()
  {
    float before = Get<float>(health, "Health");
    int hits = 0;
    EventInfo damaged = health.GetType().GetEvent("Damaged");
    Action<float> onDamaged = _ => hits++;
    damaged.AddEventHandler(health, onDamaged);

    Call(status, "Poison", 4f, 2f);
    Call(status, "Poison", 1f, 1f); // mais fraco: mantém 4 dps e 2 s
    Assert.IsTrue(Get<bool>(status, "IsPoisoned"));
    Assert.AreEqual(4f, Get<float>(status, "PoisonDamagePerSecond"), 0.001f);

    yield return new WaitForSeconds(2.3f);
    damaged.RemoveEventHandler(health, onDamaged);

    float dealt = before - Get<float>(health, "Health");
    Debug.Log($"[PlayerStatusTests] veneno causou {dealt:0.00} de dano em {hits} golpes (esperado 8)");
    Assert.IsFalse(Get<bool>(status, "IsPoisoned"));
    Assert.AreEqual(8f, dealt, 0.25f);
    Assert.GreaterOrEqual(hits, 2);
  }

  [UnityTest]
  public IEnumerator Slow_ReducesSpeedAndKeepsTheStrongest()
  {
    float normal = 0f;
    yield return MeasureMove(Vector2.right, 0.3f, d => normal = d);
    yield return MeasureMove(Vector2.left, 0.3f, null);

    Call(status, "Slow", 0.5f, 3f);
    Call(status, "Slow", 0.8f, 1f); // mais fraco: continua 0.5
    Assert.IsTrue(Get<bool>(status, "IsSlowed"));
    Assert.AreEqual(0.5f, Get<float>(status, "SpeedMultiplier"), 0.001f);

    float slowed = 0f;
    yield return MeasureMove(Vector2.right, 0.3f, d => slowed = d);
    yield return MeasureMove(Vector2.left, 0.3f, null);

    Debug.Log($"[PlayerStatusTests] distância normal {normal:0.00}, lenta {slowed:0.00} (razão {slowed / normal:0.00})");
    // A velocidade aplicada é exata; o deslocamento real pode ser menor se a jogadora esbarrar em algo
    Assert.AreEqual(0.5f, Get<float>(movement, "CurrentSpeed") / Get<float>(movement, "RunSpeed"), 0.001f);
    Assert.Greater(normal, 2f, "deveria andar livre antes da lentidão");
    Assert.Less(slowed / normal, 0.7f);
    Assert.Greater(slowed, 0.1f);

    yield return new WaitForSeconds(3f);
    Assert.IsFalse(Get<bool>(status, "IsSlowed"));
    Assert.AreEqual(1f, Get<float>(status, "SpeedMultiplier"), 0.001f);
  }

  [UnityTest]
  public IEnumerator Knockback_PushesThePlayerAndFades()
  {
    Vector3 start = Flat(player.transform.position);
    Call(status, "Knockback", new Vector3(0f, 0f, 10f));
    yield return new WaitForSeconds(0.6f);
    float pushed = Vector3.Distance(start, Flat(player.transform.position));
    Debug.Log($"[PlayerStatusTests] empurrão moveu {pushed:0.00}");
    Assert.Greater(pushed, 0.5f);
    Assert.IsFalse(Get<bool>(status, "IsKnockedBack"));
  }

  [UnityTest]
  public IEnumerator StatusApplied_FiresForEveryStatus()
  {
    var received = new System.Collections.Generic.List<string>();
    EventInfo applied = status.GetType().GetEvent("StatusApplied");
    Action<string> handler = received.Add;
    applied.AddEventHandler(status, handler);

    Call(status, "Stun", 0.1f);
    Call(status, "Poison", 1f, 0.1f);
    Call(status, "Slow", 0.5f, 0.1f);
    Call(status, "Knockback", Vector3.right);
    applied.RemoveEventHandler(status, handler);

    CollectionAssert.AreEqual(new[] { "stun", "poison", "slow", "knockback" }, received);
    yield return null;
  }

  [UnityTest]
  public IEnumerator Death_PlaysAnimationThenExistingRestartFlowRuns()
  {
    Call(status, "Stun", 5f);
    Call(status, "Poison", 2f, 5f);
    Transform model = player.transform.Find("Model");
    Quaternion standing = model.localRotation;
    float killedAt = Time.time;
    Call(health, "TakeDamage", 9999f);

    Assert.IsTrue(Get<bool>(health, "IsDead"));
    Assert.IsFalse(Get<bool>(status, "IsStunned"), "status limpos na morte");
    Assert.IsFalse(Get<bool>(status, "IsPoisoned"));
    Assert.IsTrue(Get<bool>(movement, "IsInputBlocked"), "sem comandos depois de morrer");

    yield return null;
    Assert.IsTrue(Get<bool>(animation, "IsPlayingDeath"));

    yield return new WaitForSeconds(1.2f);
    float fallen = Quaternion.Angle(standing, model.localRotation);
    Debug.Log($"[PlayerStatusTests] corpo girou {fallen:0.0} graus na queda");
    Assert.Greater(fallen, 60f, "a jogadora deveria ter caído");

    // fora do co-op a morte mostra a tela de derrota (GameResult, que pausa o jogo); "jogar de novo" reinicia
    Component oldHealth = health;
    float duration = Get<float>(animation, "DeathDuration");
    Type result = Type.GetType("GameResult, Assembly-CSharp", true);
    Func<bool> showing = () => (bool)result.GetProperty("IsShowing").GetValue(null);
    float shownAt = -1f;
    float timeout = Time.realtimeSinceStartup + 6f;
    while (Time.realtimeSinceStartup < timeout)
    {
      if (showing() && Time.timeScale == 0f)
      {
        shownAt = Time.time;
        break;
      }
      Assert.IsTrue(Get<bool>(oldHealth, "IsDead"));
      yield return null;
    }

    Debug.Log($"[PlayerStatusTests] tela de derrota {shownAt - killedAt:0.00}s depois da morte (animação {duration:0.00}s)");
    Assert.Greater(shownAt, 0f, "a tela de derrota deveria aparecer");
    Assert.GreaterOrEqual(shownAt - killedAt, duration, "derrota antes do fim da animação");

    UnityEngine.Object screen = UnityEngine.Object.FindAnyObjectByType(result);
    result.GetMethod("PlayAgain", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null);
    timeout = Time.realtimeSinceStartup + 6f;
    Component newHealth = null;
    while (Time.realtimeSinceStartup < timeout)
    {
      newHealth = FindComponent("PlayerHealth");
      if (newHealth != null && newHealth != oldHealth) break;
      yield return null;
    }
    Assert.IsTrue(newHealth != null && newHealth != oldHealth, "jogar de novo reinicia a cena");
    yield return null;
    Assert.AreEqual(1f, Time.timeScale, "o jogo volta a correr");
    Assert.IsFalse(Get<bool>(FindComponent("PlayerHealth"), "IsDead"));
  }

  // Gera imagens das animações quando a variável PLAYER_FX_SCREENSHOTS aponta para uma pasta
  [UnityTest]
  public IEnumerator Screenshots_OfStatusAnimations()
  {
    string folder = Environment.GetEnvironmentVariable("PLAYER_FX_SCREENSHOTS");
    if (string.IsNullOrEmpty(folder))
    {
      Assert.Ignore("PLAYER_FX_SCREENSHOTS não definida");
    }
    Directory.CreateDirectory(folder);

    yield return Capture(folder, "00_normal");

    Call(status, "Stun", 1.5f);
    yield return new WaitForSeconds(0.4f);
    yield return Capture(folder, "01_stunned");
    yield return new WaitForSeconds(1.3f);

    Call(status, "Poison", 0.5f, 2f);
    yield return new WaitForSeconds(0.5f);
    yield return Capture(folder, "02_poisoned");
    yield return new WaitForSeconds(1.7f);

    Call(status, "Slow", 0.4f, 2f);
    yield return MeasureMove(Vector2.right, 0.35f, null);
    yield return Capture(folder, "03_slowed");
    Set(movement, "SimulatedInput", Vector2.zero);
    yield return new WaitForSeconds(0.2f);

    Call(health, "TakeDamage", 5f);
    yield return null;
    yield return Capture(folder, "04_hurt");

    Call(status, "Knockback", player.transform.right * 12f);
    yield return new WaitForSeconds(0.05f);
    yield return Capture(folder, "05_knockback");
    yield return new WaitForSeconds(0.6f);

    Call(health, "TakeDamage", 9999f);
    yield return new WaitForSeconds(0.55f);
    yield return Capture(folder, "06_death_falling");
    yield return new WaitForSeconds(0.55f);
    yield return Capture(folder, "07_death_fallen");
    yield return new WaitForSeconds(0.6f);
    yield return Capture(folder, "08_death_end");
  }

  private IEnumerator Capture(string folder, string name)
  {
    yield return null;
    Camera cam = new GameObject("TestShotCamera").AddComponent<Camera>();
    Camera main = Camera.main;
    if (main != null)
    {
      cam.CopyFrom(main);
    }
    Vector3 target = player.transform.position + Vector3.up * 1f;
    cam.transform.position = target + new Vector3(0f, 3.2f, -4.2f);
    cam.transform.LookAt(target);
    cam.fieldOfView = 50f;

    RenderTexture rt = new RenderTexture(640, 480, 24);
    cam.targetTexture = rt;
    cam.Render();
    RenderTexture.active = rt;
    Texture2D image = new Texture2D(640, 480, TextureFormat.RGB24, false);
    image.ReadPixels(new Rect(0, 0, 640, 480), 0, 0);
    image.Apply();
    RenderTexture.active = null;
    cam.targetTexture = null;
    File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
    UnityEngine.Object.Destroy(image);
    UnityEngine.Object.Destroy(rt);
    UnityEngine.Object.Destroy(cam.gameObject);
  }

  private IEnumerator LoadGameScene()
  {
    SceneManager.LoadScene(GameScene);
    yield return null;
    yield return new WaitForSeconds(0.5f);
    DisableEnemies();
    yield return new WaitForSeconds(0.3f);

    movement = FindComponent("PlayerMovement");
    Assert.IsNotNull(movement, "PlayerMovement não encontrado na cena");
    player = movement.gameObject;
    health = player.GetComponent(GameType("PlayerHealth"));
    shooting = player.GetComponent(GameType("PlayerShooting"));
    status = player.GetComponent(GameType("PlayerStatus"));
    animation = player.GetComponent(GameType("PlayerAnimation"));
    Assert.IsNotNull(status, "PlayerStatus deveria ser adicionado à jogadora");
    Assert.IsNotNull(animation);
  }

  // Inimigos atirando atrapalhariam as medições de vida e movimento
  private static void DisableEnemies()
  {
    string[] enemyTypes = { "EnemySpawner", "ShadowEnemy", "EnemyBullet", "StandalonePlaceholderMonsterTestAI", "MonsterDirector" };
    foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>())
    {
      if (Array.IndexOf(enemyTypes, behaviour.GetType().Name) < 0)
      {
        continue;
      }

      // o spawner e o diretor de monstros ficam no objeto do mapa: só desligados
      if (behaviour.GetType().Name == "EnemySpawner" || behaviour.GetType().Name == "MonsterDirector") behaviour.enabled = false;
      else UnityEngine.Object.Destroy(behaviour.gameObject);
    }
  }

  private IEnumerator MeasureMove(Vector2 input, float seconds, Action<float> result)
  {
    Vector3 start = Flat(player.transform.position);
    Set(movement, "SimulatedInput", input);
    float begin = Time.time;
    float end = begin + seconds;
    while (Time.time < end)
    {
      yield return null;
    }
    Set(movement, "SimulatedInput", Vector2.zero);
    // Normaliza pelo tempo que realmente passou, para a duração dos quadros não influenciar
    float elapsed = Mathf.Max(0.0001f, Time.time - begin);
    result?.Invoke(Vector3.Distance(start, Flat(player.transform.position)) * seconds / elapsed);
  }

  private static Vector3 Flat(Vector3 v)
  {
    v.y = 0f;
    return v;
  }

  private static int CountObjects(string typeName)
  {
    return UnityEngine.Object.FindObjectsByType(GameType(typeName), FindObjectsInactive.Exclude).Length;
  }

  private static Component FindComponent(string typeName)
  {
    return UnityEngine.Object.FindFirstObjectByType(GameType(typeName)) as Component;
  }

  private static Type GameType(string name)
  {
    Type type = Type.GetType(name + ", Assembly-CSharp");
    Assert.IsNotNull(type, $"tipo {name} não encontrado");
    return type;
  }

  private static void Call(Component target, string method, params object[] args)
  {
    Type[] types = Array.ConvertAll(args, a => a.GetType());
    MethodInfo info = target.GetType().GetMethod(method, types);
    Assert.IsNotNull(info, $"{target.GetType().Name}.{method} não encontrado");
    info.Invoke(target, args);
  }

  private static T Get<T>(Component target, string property)
  {
    PropertyInfo info = target.GetType().GetProperty(property);
    Assert.IsNotNull(info, $"{target.GetType().Name}.{property} não encontrado");
    return (T)info.GetValue(target);
  }

  private static void Set(Component target, string property, object value)
  {
    target.GetType().GetProperty(property).SetValue(target, value);
  }
}
