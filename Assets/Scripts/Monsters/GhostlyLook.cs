using UnityEngine;

namespace Roguelike
{
    /// Gives each monster instance its own phase for the breathing/misshaping of the Roguelike/Ghostly and
    /// Roguelike/GhostSmoke materials, so several of the same monster never pulse in sync. Bosses also turn
    /// their glow up through it when they enrage.
    public class GhostlyLook : MonoBehaviour
    {
        static readonly int PhaseId = Shader.PropertyToID("_Phase");
        static readonly int GlowId = Shader.PropertyToID("_GlowColor");
        static readonly int RimId = Shader.PropertyToID("_RimColor");

        [SerializeField] float phase = -1f;

        SkinnedMeshRenderer[] _renderers;

        void Awake()
        {
            if (phase < 0f)
                phase = Random.Range(0f, 100f);
            _renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            var block = new MaterialPropertyBlock();
            foreach (var r in _renderers)
            {
                r.GetPropertyBlock(block);
                block.SetFloat(PhaseId, phase);
                r.SetPropertyBlock(block);
            }
        }

        /// Multiplies the eye glow and rim light of the body material (1 = as authored).
        public void SetGlow(float boost)
        {
            var block = new MaterialPropertyBlock();
            foreach (var r in _renderers)
            {
                var mat = r.sharedMaterial;
                if (mat == null || !mat.HasProperty(GlowId))
                    continue;
                r.GetPropertyBlock(block, 0);
                block.SetFloat(PhaseId, phase);
                block.SetColor(GlowId, mat.GetColor(GlowId) * boost);
                if (mat.HasProperty(RimId)) block.SetColor(RimId, mat.GetColor(RimId) * Mathf.Lerp(1f, boost, 0.6f));
                r.SetPropertyBlock(block, 0);
            }
        }
    }
}
