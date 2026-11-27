using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // Owns materials on a collectible clone; scene templates and other models stay independent.
    public sealed class BossGemShine : MonoBehaviour
    {
        Material crystal;
        Material aura;
        Image edge;
        Material previousEdge;
        Renderer[] renderers;
        Material[][] originals;
        Material[] polishedMaterials;
        public bool IsReady => crystal != null && aura != null;

        public bool Initialize(Image edgeImage, Color color)
        {
            var crystalShader = GameArt.Load<Shader>("Art/Shaders/BossGemCrystal");
            var auraShader = GameArt.Load<Shader>("Art/Shaders/BossGemAura");
            if (crystalShader == null || auraShader == null || !crystalShader.isSupported || !auraShader.isSupported) return false;
            crystal = new Material(crystalShader) { name = "Polished collectible gem (runtime)" };
            aura = new Material(auraShader) { name = "Collectible aura and glints (runtime)" };
            float phase = Mathf.Repeat(transform.localPosition.x * 0.017f + transform.localPosition.y * 0.031f, 6.28f);
            crystal.SetColor("_EmissionColor", color);
            crystal.SetFloat("_Phase", phase);
            aura.SetColor("_Tint", color);
            aura.SetFloat("_Phase", phase);
            renderers = GetComponentsInChildren<Renderer>(true);
            originals = new Material[renderers.Length][];
            polishedMaterials = new Material[renderers.Length];
            for (int i = 0; i < Mathf.Min(renderers.Length, originals.Length); i++)
            {
                if (renderers[i] is ParticleSystemRenderer) continue;
                originals[i] = renderers[i].sharedMaterials;
                var material = new Material(crystal) { name = "Polished gem facets (runtime)" };
                var bounds = renderers[i].localBounds;
                material.SetVector("_GemCenter", bounds.center);
                material.SetVector("_GemExtents", bounds.extents);
                polishedMaterials[i] = material;
                var polished = new Material[originals[i].Length];
                for (int slot = 0; slot < polished.Length; slot++) polished[slot] = material;
                renderers[i].sharedMaterials = polished;
            }
            edge = edgeImage;
            if (edge != null)
            {
                previousEdge = edge.material;
                edge.material = aura;
            }
            return true;
        }

        // Reloads can preserve renderer references before the material backup is ready.
        void OnDestroy()
        {
            if (renderers != null && originals != null)
                for (int i = 0; i < Mathf.Min(renderers.Length, originals.Length); i++)
                    if (renderers[i] != null && originals[i] != null) renderers[i].sharedMaterials = originals[i];
            if (edge != null && edge.material == aura) edge.material = previousEdge;
            if (polishedMaterials != null)
                foreach (var material in polishedMaterials)
                    if (material != null) Destroy(material);
            if (crystal != null) Destroy(crystal);
            if (aura != null) Destroy(aura);
        }
    }
}
