using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// A boss fight: once the boss notices the target it runs its pattern — a scripted round of moves and
    /// attacks (Round), repeated — until it dies. Below enrageAt of its health it enrages once (a short
    /// invulnerable roar, glowing brighter) and from then on plays its phase-2 round: faster, with combos and
    /// shorter openings. Shows a health bar at the bottom of the screen during the fight.
    public abstract class BossBrain : MonsterBrain
    {
        static readonly List<BossBrain> Fighting = new List<BossBrain>();

        [SerializeField] string title = "Boss";
        [Tooltip("Health fraction where it enrages into phase 2.")]
        [Range(0f, 1f)] [SerializeField] float enrageAt = 0.5f;
        [Tooltip("Burst of smoke when it enrages.")]
        [SerializeField] GameObject enrageEffect;
        [SerializeField] bool showHealthBar = true;

        protected Health HP;
        GhostlyLook _look;
        Coroutine _fight;

        public int Phase { get; private set; } = 1;
        public bool InFight => _fight != null;
        public string Title => title;
        /// What the pattern is doing right now (the attack's state name, "Hold", "Approach", ...).
        public string Step { get; private set; } = "";

        protected float Dist => M.TargetDistance;

        protected override void Awake()
        {
            base.Awake();
            HP = GetComponent<Health>();
            _look = GetComponent<GhostlyLook>();
            if (HP) HP.Died += Stop;
        }

        public override void Tick()
        {
            if (_fight == null && M.Engaged)
                _fight = StartCoroutine(Fight());
        }

        void OnDisable() => Stop();

        void Stop()
        {
            StopAllCoroutines();
            _fight = null;
            Fighting.Remove(this);
        }

        IEnumerator Fight()
        {
            Fighting.Add(this);
            Step = "Intro";
            yield return Intro();
            while (true)
            {
                yield return CheckEnrage();
                if (M.HasTarget)
                    yield return Round();
                yield return null;
            }
        }

        IEnumerator CheckEnrage()
        {
            if (Phase != 1 || HP == null || HP.Current > HP.Max * enrageAt)
                yield break;
            Phase = 2;
            Step = "Enrage";
            yield return Enrage();
        }

        /// One pass of the pattern; check Phase to vary it.
        protected abstract IEnumerator Round();

        protected virtual IEnumerator Intro()
        {
            yield return Hold(0.8f);
        }

        protected virtual IEnumerator Enrage()
        {
            bool was = HP && HP.Invulnerable;
            if (HP) HP.Invulnerable = true;
            if (enrageEffect)
            {
                var fx = Instantiate(enrageEffect, M.Center, Quaternion.identity);
                fx.transform.localScale = Vector3.one * M.Size * 1.5f;
                Destroy(fx, 3f);
            }
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                if (_look) _look.SetGlow(Mathf.Lerp(1f, 1.8f, t / 1.2f));
                M.FaceTarget(2f);
                yield return null;
            }
            if (HP) HP.Invulnerable = was;
        }

        // ------------------------------------------------------------------------------- pattern steps

        /// Runs the attack and waits for it to play out. Dropping below enrageAt enrages it first.
        protected IEnumerator Use(MonsterAttack attack)
        {
            if (attack == null)
                yield break;
            while (M.Busy)
                yield return null;
            yield return CheckEnrage();
            Step = attack.State;
            if (!M.Run(attack))
                yield break;
            while (M.Busy)
                yield return null;
        }

        /// Stands its ground facing the target.
        protected IEnumerator Hold(float seconds)
        {
            Step = "Hold";
            for (float end = Time.time + seconds; Time.time < end;)
            {
                M.FaceTarget();
                yield return null;
            }
        }

        /// Walks at the target until within `within` m, or `timeout` runs out.
        protected IEnumerator Approach(float within, float timeout, float speed = 1f)
        {
            Step = "Approach";
            for (float end = Time.time + timeout; Time.time < end && Dist > within;)
            {
                M.Move(ToTarget, speed);
                yield return null;
            }
        }

        /// Holds a distance band from the target, circling it, for `seconds` or until `until` says stop.
        protected IEnumerator Hover(float min, float max, float seconds, float speed = 1f, System.Func<bool> until = null)
        {
            Step = "Hover";
            for (float end = Time.time + seconds; Time.time < end && (until == null || !until());)
            {
                KeepRange(min, max, speed, speed * 0.8f);
                yield return null;
            }
        }

        // ------------------------------------------------------------------------------- health bar

        void OnGUI()
        {
            HudScale.Begin();
            DrawHud();
            HudScale.End();
        }

        private void DrawHud()
        {
            if (!showHealthBar || !InFight || HP == null || HP.IsDead)
                return;
            int slot = Fighting.IndexOf(this);
            float w = Mathf.Min(560f, HudScale.Width * 0.6f), h = 14f;
            float x = (HudScale.Width - w) * 0.5f, y = HudScale.Height - 104f - slot * 46f;
            var label = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter };
            label.normal.textColor = Phase > 1 ? new Color(1f, 0.6f, 1f) : Color.white;
            GUI.Label(new Rect(x, y - 24f, w, 22f), Phase > 1 ? $"{title}  — enraged" : title, label);
            var bar = new Rect(x, y, w, h);
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.05f, 0.03f, 0.08f, 0.85f), 0f, 3f);
            var fill = new Rect(x + 2f, y + 2f, (w - 4f) * Mathf.Clamp01(HP.Current / HP.Max), h - 4f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                            Phase > 1 ? new Color(0.85f, 0.25f, 0.75f) : new Color(0.55f, 0.35f, 0.95f), 0f, 2f);
            GUI.DrawTexture(new Rect(x + w * enrageAt - 1f, y, 2f, h), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                            new Color(1f, 1f, 1f, 0.6f), 0f, 0f);
        }
    }
}
