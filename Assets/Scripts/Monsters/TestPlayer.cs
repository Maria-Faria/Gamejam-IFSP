using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// Stand-in player for trying the monsters out: a cylinder that runs about (WASD / arrows, relative to the
    /// camera), dashes (Shift; untouchable while dashing) and hits back (Space or left click: a swipe that hurts
    /// monsters in front of it). G toggles god mode, R restarts the scene. It flashes red when hit and, killed,
    /// respawns where it started after a moment.
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public class TestPlayer : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] float speed = 4.5f;
        [SerializeField] float dashSpeed = 13f;
        [SerializeField] float dashTime = 0.18f;
        [SerializeField] float dashCooldown = 0.7f;
        [SerializeField] float attackRange = 2.2f;
        [SerializeField] float attackDamage = 20f;
        [SerializeField] float attackCooldown = 0.3f;
        [SerializeField] bool godMode;
        [SerializeField] Renderer body;
        [Tooltip("Burst shown in front of it on every swipe.")]
        [SerializeField] GameObject swingEffect;

        CharacterController _cc;
        Health _hp;
        Material _mat;
        Color _color;
        Vector3 _spawn, _dashDir;
        float _vy, _dashUntil, _nextDash, _nextAttack, _flashUntil, _respawnAt;

        bool Dashing => Time.time < _dashUntil;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _hp = GetComponent<Health>();
            _spawn = transform.position;
            if (body)
            {
                _mat = body.material;
                _color = _mat.GetColor(BaseColorId);
            }
            _hp.Damaged += (d, from) => _flashUntil = Time.time + 0.12f;
            _hp.Died += () => _respawnAt = Time.time + 2f;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }
            if (kb != null && kb.gKey.wasPressedThisFrame)
                godMode = !godMode;
            _hp.Invulnerable = godMode || Dashing;
            if (_mat) _mat.SetColor(BaseColorId, Time.time < _flashUntil ? new Color(1f, 0.15f, 0.1f) : _hp.IsDead ? _color * 0.3f : _color);
            if (_hp.IsDead)
            {
                if (Time.time >= _respawnAt) Respawn();
                return;
            }

            var input = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            }
            var cam = Camera.main ? Camera.main.transform : transform;
            var forward = Monster.Flat(cam.forward).normalized;
            var right = Monster.Flat(cam.right).normalized;
            var move = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);

            if (kb != null && kb.leftShiftKey.wasPressedThisFrame && Time.time >= _nextDash)
            {
                _dashDir = move.sqrMagnitude > 0.01f ? move.normalized : transform.forward;
                _dashUntil = Time.time + dashTime;
                _nextDash = Time.time + dashCooldown;
            }
            var velocity = Dashing ? _dashDir * dashSpeed : move * speed;
            _vy = _cc.isGrounded ? -1f : _vy - 20f * Time.deltaTime;
            _cc.Move((velocity + Vector3.up * _vy) * Time.deltaTime);
            if (velocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(velocity), 900f * Time.deltaTime);

            bool attack = (kb != null && kb.spaceKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (attack && Time.time >= _nextAttack)
                Attack();
        }

        void Attack()
        {
            _nextAttack = Time.time + attackCooldown;
            var centre = transform.position + Vector3.up * 0.55f;
            var hit = new HashSet<Monster>();
            foreach (var c in Physics.OverlapSphere(centre, attackRange, ~0, QueryTriggerInteraction.Ignore))
            {
                var m = c.GetComponentInParent<Monster>();
                if (m == null || m.Dead || !hit.Add(m))
                    continue;
                var to = Monster.Flat(c.ClosestPoint(centre) - transform.position);
                if (to.magnitude > 0.6f && Vector3.Angle(transform.forward, to) > 75f)
                    continue;                      // behind us
                m.GetComponent<Health>()?.TakeDamage(attackDamage, transform.position);
            }
            if (swingEffect)
            {
                var fx = Instantiate(swingEffect, centre + transform.forward * 1f, Quaternion.identity);
                fx.transform.localScale = Vector3.one * 0.6f;
                Destroy(fx, 2f);
            }
        }

        void Respawn()
        {
            _cc.enabled = false;
            transform.position = _spawn;
            _cc.enabled = true;
            _hp.SetMax(_hp.Max);
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(12, Screen.height - 50, 900, 22),
                      "WASD move   Shift dash   Space / click hit   G god mode   R restart   Tab one monster at a time   1-9 nearest attacks", style);
            GUI.Label(new Rect(12, Screen.height - 28, 900, 22), $"HP {_hp.Current:0}/{_hp.Max:0}{(godMode ? "   GOD MODE" : "")}", style);
            if (_hp.IsDead)
            {
                var big = new GUIStyle(style) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
                big.normal.textColor = new Color(1f, 0.4f, 0.4f);
                GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 60), "DEAD - respawning", big);
            }
        }
    }
}
