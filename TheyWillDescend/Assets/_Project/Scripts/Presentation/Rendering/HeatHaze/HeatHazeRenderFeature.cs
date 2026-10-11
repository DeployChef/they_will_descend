using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace TheyWillDescend.Presentation.Rendering
{
    public sealed class HeatHazeRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] private LayerMask _heatHazeLayerMask;
        private HeatHazePass _pass;

        public override void Create()
        {
            _pass = new HeatHazePass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents,
                requiresIntermediateTexture = true
            };
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraType type = renderingData.cameraData.cameraType;
            if ((type != CameraType.Game && type != CameraType.SceneView)
                || renderingData.cameraData.renderType != CameraRenderType.Base
                || _heatHazeLayerMask.value == 0)
                return;

            _pass.LayerMask = _heatHazeLayerMask.value;
            renderer.EnqueuePass(_pass);
        }

        private sealed class HeatHazePass : ScriptableRenderPass
        {
            private static readonly int SceneColorId =
                Shader.PropertyToID("_HeatSceneColor");

            private static readonly List<ShaderTagId> ShaderTags =
                new List<ShaderTagId>
                {
                    new ShaderTagId("UniversalForward"),
                    new ShaderTagId("UniversalForwardOnly"),
                    new ShaderTagId("SRPDefaultUnlit")
                };

            public int LayerMask { get; set; }

            private sealed class DrawData
            {
                public RendererListHandle RendererList;
            }

            public override void RecordRenderGraph(
                RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var source = resources.activeColorTexture;

                if (resources.isActiveTargetBackBuffer || !source.IsValid()
                    || !resources.activeDepthTexture.IsValid())
                    return;

                var descriptor = renderGraph.GetTextureDesc(source);
                descriptor.name = "HeatHaze Scene Color";
                descriptor.depthBufferBits = DepthBits.None;
                descriptor.msaaSamples = MSAASamples.None;
                descriptor.bindTextureMS = false;
                descriptor.clearBuffer = false;

                var sceneColor = renderGraph.CreateTexture(descriptor);

                using (var builder = renderGraph.AddBlitPass(
                    source, sceneColor, Vector2.one, Vector2.zero,
                    passName: "HeatHaze: Copy Scene",
                    returnBuilder: true))
                {
                    builder.SetGlobalTextureAfterPass(sceneColor, SceneColorId);
                }

                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();

                var drawing = RenderingUtils.CreateDrawingSettings(
                    ShaderTags, renderingData, cameraData, lightData,
                    SortingCriteria.CommonTransparent);

                var filtering = new FilteringSettings(
                    RenderQueueRange.transparent, LayerMask);

                var rendererList = renderGraph.CreateRendererList(
                    new RendererListParams(
                        renderingData.cullResults, drawing, filtering));

                using (var builder = renderGraph.AddRasterRenderPass<DrawData>(
                    "HeatHaze: Draw Particles", out var data))
                {
                    data.RendererList = rendererList;

                    builder.UseGlobalTexture(SceneColorId, AccessFlags.Read);
                    builder.UseRendererList(rendererList);
                    builder.SetRenderAttachment(
                        resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(
                        resources.activeDepthTexture, AccessFlags.Read);

                    builder.SetRenderFunc(
                        static (DrawData passData, RasterGraphContext context) =>
                            context.cmd.DrawRendererList(passData.RendererList));
                }
            }
        }
    }
}
