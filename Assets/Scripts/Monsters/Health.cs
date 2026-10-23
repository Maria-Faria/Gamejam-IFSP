using System;
using UnityEngine;

namespace Roguelike
{
    /// Hit points for anything monsters can hurt (the player) or that can be hurt (the monsters themselves).
    /// The members are virtual so a target can keep its hit points elsewhere: in the game the player's
    /// MonsterPlayerHealth subclass forwards everything to the game's PlayerHealth.
    public class Health : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;
        [Tooltip("Takes hits (events fire) without losing health, for test scenes.")]
        [SerializeField] bool invulnerable;

        float _current;

        public virtual float Max => maxHealth;
        public virtual float Current => _current;
        public virtual bool IsDead => Current <= 0f;
        public float LastHitTime { get; protected set; } = -1f;

        /// (damage, where it came from)
        public event Action<float, Vector3> Damaged;
        /// Any victim hit by a monster's attack or projectile: (victim, damage dealt, the monster). The game
        /// hangs on-hit effects on it (poison, stun...).
        public static event Action<Health, float, Monster> HitByMonster;
        public event Action Died;

        protected virtual void Awake() => _current = maxHealth;

        public void SetMax(float value, bool refill = true)
        {
            maxHealth = value;
            if (refill) _current = value;
        }

        public bool Invulnerable
        {
            get => invulnerable;
            set => invulnerable = value;
        }

        public virtual void TakeDamage(float amount, Vector3 from)
        {
            if (IsDead || amount <= 0f)
                return;
            if (!invulnerable)
                _current = Mathf.Max(0f, _current - amount);
            LastHitTime = Time.time;
            Damaged?.Invoke(amount, from);
            if (IsDead)
                Died?.Invoke();
        }

        /// A hit from a monster: scaled by its DamageScale (unless the amount already is, as a projectile's
        /// is when it is fired), then reported through HitByMonster.
        public void TakeDamage(float amount, Vector3 from, Monster source, bool applyScale = true)
        {
            if (IsDead || amount <= 0f)
                return;
            if (source && applyScale) amount *= source.DamageScale;
            TakeDamage(amount, from);
            HitByMonster?.Invoke(this, amount, source);
        }

        protected void RaiseDamaged(float amount, Vector3 from)
        {
            LastHitTime = Time.time;
            Damaged?.Invoke(amount, from);
        }

        protected void RaiseDied() => Died?.Invoke();

        /// The Health a collider belongs to, unless it is part of `self` or another monster (no friendly fire).
        public static Health Victim(Collider c, Transform self)
        {
            if (c == null || c.isTrigger || (self && c.transform.IsChildOf(self)))
                return null;
            var h = c.GetComponentInParent<Health>();
            if (h == null || h.IsDead || h.GetComponent<Monster>())
                return null;
            return h;
        }
    }
}
