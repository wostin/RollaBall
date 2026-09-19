// CrossSectionRendererFeature.cs
// Renderer Feature para URP 17+ (Unity 6) con Render Graph.
// Renderiza objetos de forma secuencial para evitar conflictos de Stencil.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;

public class CrossSectionRendererFeature : ScriptableRendererFeature
{
    class CrossSectionPass : ScriptableRenderPass
    {
        class PassData
        {
            internal List<Renderer> renderers = new List<Renderer>();
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData  = frameData.Get<UniversalResourceData>();

            // Usamos la lista pre-filtrada y cacheada
            var entries = CrossSectionController.RegisteredEntries;
            if (entries == null || entries.Count == 0) return;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                "CrossSection_SequentialCap", out var passData))
            {
                // Registramos los renderers para el Render Graph
                for (int i = 0; i < entries.Count; i++)
                {
                    var renderer = entries[i].renderer;
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                        passData.renderers.Add(renderer);
                }

                if (passData.renderers.Count == 0) return;

                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    // Volvemos a usar la lista estática directamente para evitar copias
                    var staticEntries = CrossSectionController.RegisteredEntries;
                    
                    for (int i = 0; i < staticEntries.Count; i++)
                    {
                        var entry = staticEntries[i];
                        var renderer = entry.renderer;

                        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                        if (!renderer.isVisible) continue;

                        for (int j = 0; j < entry.materials.Length; j++)
                        {
                            var mat = entry.materials[j];
                            int submesh = entry.submeshIndices[j];

                            // Dibujamos secuencialmente (Passes: 1 Inc, 2 Dec, 3 Cap, 4 Cleanup)
                            ctx.cmd.DrawRenderer(renderer, mat, submesh, 1);
                            ctx.cmd.DrawRenderer(renderer, mat, submesh, 2);
                            ctx.cmd.DrawRenderer(renderer, mat, submesh, 3);
                            ctx.cmd.DrawRenderer(renderer, mat, submesh, 4);
                        }
                    }
                });
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }
    }

    CrossSectionPass m_Pass;

    public override void Create()
    {
        m_Pass = new CrossSectionPass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_Pass);
    }
}
