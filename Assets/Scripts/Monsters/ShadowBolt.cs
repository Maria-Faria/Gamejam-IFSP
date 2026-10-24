using UnityEngine;

namespace Roguelike
{
    /// Makes a monster's bolt look alive: its body keeps contracting and swelling like something breathing
    /// hard, and every time it contracts it exhales a gust of heavy smoke that hangs in the air behind it,
    /// while a trail of darkness drags after it. When the bolt hits, the smoke and the trail are let go to
    /// drift and fade instead of vanishing with it (Release).
    public class ShadowBolt : MonoBehaviour
    {
        [SerializeField] Transform body;
        [Tooltip("Breaths a second.")]
        [SerializeField] float pulseRate = 2.4f;
        [Tooltip("How far it swells and shrinks (fraction of its size).")]
        [SerializeField] float pulseAmount = 0.22f;
        [Tooltip("Heavy smoke puffed out on every contraction.")]
        [SerializeField] ParticleSystem exhale;
        [SerializeField] int exhaleCount = 7;

        Vector3 _rest;
        float _phase;
        float _last;

        public int Exhaled { get; private set; }

        void Awake()
        {
            if (body) _rest = body.localScale;
            _phase = Random.Range(0f, 2f * Mathf.PI);
            _last = 1f;
        }

        void Update()
        {
            if (!body)
                return;
            float s = Mathf.Sin(Time.time * pulseRate * 2f * Mathf.PI + _phase);
            // swells round, squeezes long and thin along the way it flies, like a gulp of air pushed out
            float k = pulseAmount * s;
            body.localScale = Vector3.Scale(_rest, new Vector3(1f + k, 1f + k, 1f - k * 0.6f));
            if (_last > 0f && s <= 0f && exhale)
            {
                exhale.Emit(exhaleCount);
                Exhaled++;
            }
            _last = s;
        }

        /// Lets go of the smoke and trails so they drift and fade where they are.
        public static void Release(GameObject bolt)
        {
            foreach (var ps in bolt.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                if (main.simulationSpace != ParticleSystemSimulationSpace.World || ps.particleCount == 0)
                    continue;
                ps.transform.SetParent(null, true);
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                Destroy(ps.gameObject, main.startLifetime.constantMax + 0.2f);
            }
            foreach (var tr in bolt.GetComponentsInChildren<TrailRenderer>())
            {
                tr.transform.SetParent(null, true);
                tr.emitting = false;
                Destroy(tr.gameObject, tr.time + 0.1f);
            }
        }
    }
}
