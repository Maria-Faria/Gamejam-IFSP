using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// One attack of a monster. The Monster brain starts it when the target is between minRange and maxRange
    /// and the cooldown is over; Perform plays the animator state and does the attack, timed by the
    /// animation events of the clip (OnAnimationEvent).
    [RequireComponent(typeof(Monster))]
    public abstract class MonsterAttack : MonoBehaviour
    {
        [Tooltip("Animator state (= clip name) this attack plays.")]
        [SerializeField] protected string state;
        [SerializeField] protected float damage = 10f;
        [SerializeField] protected float minRange;
        [SerializeField] protected float maxRange = 6f;
        [SerializeField] protected float cooldown = 3f;
        [Tooltip("Relative chance of being picked when several attacks are ready.")]
        [SerializeField] float weight = 1f;

        protected Monster M;
        float _readyAt;
        readonly List<GameObject> _temporary = new List<GameObject>();

        public string State => state;
        public float Damage => damage;
        public float MinRange => minRange;
        public float MaxRange => maxRange;
        public float Weight => weight;
        public bool OnCooldown => Time.time < _readyAt;

        protected virtual void Awake()
        {
            M = GetComponent<Monster>();
            _readyAt = Time.time + Random.Range(0.2f, 0.6f) * cooldown;
        }

        public virtual bool CanStart(float distance) => !OnCooldown && distance >= minRange && distance <= maxRange;

        public void MarkUsed() => _readyAt = Time.time + cooldown * (M ? M.CooldownScale : 1f);

        public abstract IEnumerator Perform();

        public virtual void OnAnimationEvent(string evt) { }

        /// Called when the attack ends or is interrupted: removes effects it spawned.
        public virtual void Cleanup()
        {
            foreach (var go in _temporary)
                if (go) Destroy(go);
            _temporary.Clear();
        }

        protected T Track<T>(T obj) where T : Object
        {
            var go = obj as GameObject ?? (obj as Component)?.gameObject;
            if (go) _temporary.Add(go);
            return obj;
        }

        protected Vector3 TargetPoint => M.Target ? M.Target.position : transform.position + transform.forward * 3f * MonsterScale.Factor;

        protected float TargetHeight(Transform t)
        {
            var cc = t ? t.GetComponent<CharacterController>() : null;
            return cc ? cc.center.y : 0.5f * MonsterScale.Factor;
        }

        /// Damages every victim within `radius` of `point` once per call; returns how many were hit.
        protected int HitSphere(Vector3 point, float radius, float amount, HashSet<Health> already = null)
        {
            int n = 0;
            foreach (var c in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var h = Health.Victim(c, transform);
                if (h == null || (already != null && !already.Add(h)))
                    continue;
                h.TakeDamage(amount, point, M);
                n++;
            }
            return n;
        }

        /// While a state is playing, keep turning towards the target at `turnScale` of the monster's turn speed.
        protected IEnumerator PlayAndTrack(string stateName, float turnScale)
        {
            M.Play(stateName);
            float end = Time.time + M.ClipLength(stateName);
            while (Time.time < end)
            {
                if (turnScale > 0f) M.FaceTowards(TargetPoint, turnScale);
                yield return null;
            }
        }
    }
}
