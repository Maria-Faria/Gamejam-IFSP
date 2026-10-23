using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// The shadow balls' attack: coil back, then shoot forward at the target ("Hit" starts the dash), hurting
    /// it on contact.
    public class LungeAttack : MonsterAttack
    {
        [SerializeField] float dashDistance = 2.5f;
        [SerializeField] float dashTime = 0.25f;
        [Tooltip("Contact radius (x monster height).")]
        [SerializeField] float radius = 0.6f;

        float _dashStart = -1f;
        Vector3 _dir;

        public override IEnumerator Perform()
        {
            _dashStart = -1f;
            M.Play(state);
            float end = Time.time + M.ClipLength(state);
            var hit = new HashSet<Health>();
            while (Time.time < end)
            {
                if (_dashStart < 0f)
                    M.FaceTowards(TargetPoint, 1.5f);
                else if (Time.time - _dashStart < dashTime)
                {
                    var step = _dir * dashDistance / dashTime * Time.deltaTime;
                    var origin = transform.position + Vector3.up * M.Size * 0.5f;
                    bool blocked = false;
                    foreach (var h in Physics.SphereCastAll(origin, radius * M.Size * 0.5f, _dir, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
                        if (!M.IsSelf(h.collider) && !Health.Victim(h.collider, transform) && h.normal.y < 0.6f && h.distance > 0f)
                            blocked = true;
                    if (!blocked) transform.position += step;
                    HitSphere(origin, radius * M.Size, damage, hit);
                }
                yield return null;
            }
        }

        public override void OnAnimationEvent(string evt)
        {
            if (evt != "Hit")
                return;
            _dashStart = Time.time;
            _dir = transform.forward;
        }
    }
}
