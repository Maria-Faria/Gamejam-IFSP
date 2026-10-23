using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// The skull face's charged laser: gathers a growing orb between its claws ("Charge"), then fires a beam
    /// from the face ("Fire") that sweeps slowly after the target, burning whatever it touches, until
    /// "FireEnd".
    public class LaserAttack : MonsterAttack
    {
        [SerializeField] string origin = "Eye";
        [SerializeField] string[] chargeHands = { "Muzzle.L", "Muzzle.R" };
        [Tooltip("Orb that grows between the hands while charging.")]
        [SerializeField] GameObject chargeOrb;
        [Tooltip("Beam: a LineRenderer (positions 0 and 1 are set every frame).")]
        [SerializeField] LineRenderer beam;
        [SerializeField] float length = 18f;
        [SerializeField] float beamRadius = 0.25f;
        [Tooltip("Degrees per second the beam sweeps after the target while firing.")]
        [SerializeField] float sweepSpeed = 22f;
        [SerializeField] float tickInterval = 0.15f;

        GameObject _orb;
        LineRenderer _beam;
        float _chargeStart = -1f, _nextTick;
        Vector3 _beamDir;

        public bool Firing => _beam != null;

        public float SweepSpeed
        {
            get => sweepSpeed;
            set => sweepSpeed = value;
        }

        public override IEnumerator Perform()
        {
            _chargeStart = -1f;
            M.Play(state);
            float end = Time.time + M.ClipLength(state);
            while (Time.time < end)
            {
                if (_beam == null)
                    M.FaceTowards(TargetPoint, 0.5f);
                UpdateOrb();
                UpdateBeam();
                yield return null;
            }
        }

        public override void OnAnimationEvent(string evt)
        {
            switch (evt)
            {
                case "Charge":
                    _chargeStart = Time.time;
                    if (chargeOrb) _orb = Track(Instantiate(chargeOrb, HandsMid(), Quaternion.identity));
                    break;
                case "Fire":
                    if (_orb) Destroy(_orb);
                    var from = Origin();
                    _beamDir = ((TargetPoint + Vector3.up * TargetHeight(M.Target)) - from).normalized;
                    if (beam) _beam = Track(Instantiate(beam));
                    _nextTick = 0f;
                    break;
                case "FireEnd":
                    if (_beam) Destroy(_beam.gameObject);
                    _beam = null;
                    break;
            }
        }

        Vector3 Origin()
        {
            var b = M.Bone(origin);
            return b ? b.position : M.Center + transform.forward * M.Size * 0.3f;
        }

        Vector3 HandsMid()
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var h in chargeHands)
            {
                var b = M.Bone(h);
                if (b) { sum += b.position; n++; }
            }
            return n > 0 ? sum / n : M.Center + transform.forward * M.Size * 0.4f;
        }

        void UpdateOrb()
        {
            if (_orb == null || _chargeStart < 0f)
                return;
            float k = Mathf.Clamp01((Time.time - _chargeStart) / 1.2f);
            _orb.transform.position = HandsMid();
            _orb.transform.localScale = Vector3.one * M.Size * Mathf.Lerp(0.03f, 0.22f, k) * (1f + 0.08f * Mathf.Sin(Time.time * 40f));
        }

        void UpdateBeam()
        {
            if (_beam == null)
                return;
            var from = Origin();
            var want = ((TargetPoint + Vector3.up * TargetHeight(M.Target)) - from).normalized;
            _beamDir = Vector3.RotateTowards(_beamDir, want, sweepSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
            // the body follows the beam
            M.FaceTowards(from + _beamDir, 0.6f);
            float len = length;
            foreach (var h in Physics.RaycastAll(from, _beamDir, length, ~0, QueryTriggerInteraction.Ignore))
                if (!M.IsSelf(h.collider) && !h.collider.GetComponentInParent<Monster>() && !h.collider.GetComponentInParent<Health>())
                    len = Mathf.Min(len, h.distance);
            _beam.positionCount = 2;
            _beam.SetPosition(0, from);
            _beam.SetPosition(1, from + _beamDir * len);
            float w = beamRadius * 2f * (1f + 0.15f * Mathf.Sin(Time.time * 50f));
            _beam.startWidth = w * 0.7f;
            _beam.endWidth = w;
            if (Time.time >= _nextTick)
            {
                _nextTick = Time.time + tickInterval;
                var hit = new HashSet<Health>();
                foreach (var h in Physics.SphereCastAll(from, beamRadius, _beamDir, len, ~0, QueryTriggerInteraction.Ignore))
                {
                    var victim = Health.Victim(h.collider, transform);
                    if (victim && hit.Add(victim))
                        victim.TakeDamage(damage, from);
                }
            }
        }

        public override void Cleanup()
        {
            base.Cleanup();
            _beam = null;
            _orb = null;
        }
    }
}
