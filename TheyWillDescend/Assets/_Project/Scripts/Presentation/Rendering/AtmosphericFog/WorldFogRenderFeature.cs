using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TheyWillDescend.Presentation.Rendering
{
    /// <summary>Scene-owned settings supplied by TimeLightController; never serialized into the renderer asset.</summary>
    internal struct WorldFogSettings
    {
        public bool Enabled;
        public Color Color;
        public Vector4 Distance; // Linear start/end, exponential density, FogMode.
        public Vector4 Mask; // World centre X/Z, clear radius, transition width.
        public Vector4 Strength; // Inside, outside, maximum opacity, reserved.
    }

    /// <summary>One depth-driven, alpha-blended fog pass. No colour copy, noise, or raymarching.</summary>
    public sealed class WorldFogRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] private Material fogMaterial;
        private WorldFogSettings _settings;
        private WorldFogPass _pass;

        internal bool IsReady => isActive && fogMaterial && fogMaterial.shader.isSupported;

        internal void SetFog(WorldFogSettings settings) => _settings = settings;
        internal void ClearFog() => _settings = default;

        public override void Create()
        {
            _pass = new WorldFogPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
                requiresIntermediateTexture = true
            };
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraType type = renderingData.cameraData.cameraType;
            if (!IsReady || !_settings.Enabled || _pass == null
                || (type != CameraType.Game && type != CameraType.SceneView)
                || renderingData.cameraData.renderType != CameraRenderType.Base)
                return;

            _pass.Material = fogMaterial;
            _pass.Settings = _settings;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            ClearFog();
            _pass = null;
        }

        private sealed class WorldFogPass : ScriptableRenderPass
        {
            private static readonly int DepthId = Shader.PropertyToID("_WorldFogDepth");
            private static readonly int ColorId = Shader.PropertyToID("_WorldFogColor");
            private static readonly int DistanceId = Shader.PropertyToID("_WorldFogDistance");
            private static readonly int MaskId = Shader.PropertyToID("_WorldFogMask");
            private static readonly int StrengthId = Shader.PropertyToID("_WorldFogStrength");
            private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();

            public Material Material;
            public WorldFogSettings Settings;

            private sealed class PassData
            {
                public TextureHandle Depth;
                public Material Material;
                public MaterialPropertyBlock Properties;
                public WorldFogSettings Settings;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !resources.activeColorTexture.IsValid()
                    || !resources.cameraDepthTexture.IsValid())
                    return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("World Masked Fog", out var data))
                {
                    data.Depth = resources.cameraDepthTexture;
                    data.Material = Material;
                    data.Properties = _properties;
                    data.Settings = Settings;
                    builder.UseTexture(data.Depth, AccessFlags.Read);
                    // Hardware alpha blending reads the destination; the shader never samples it.
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                    {
                        RTHandle depth = pass.Depth;
                        pass.Properties.Clear();
                        pass.Properties.SetTexture(DepthId, depth.rt);
                        pass.Properties.SetColor(ColorId, pass.Settings.Color);
                        pass.Properties.SetVector(DistanceId, pass.Settings.Distance);
                        pass.Properties.SetVector(MaskId, pass.Settings.Mask);
                        pass.Properties.SetVector(StrengthId, pass.Settings.Strength);
                        context.cmd.DrawProcedural(Matrix4x4.identity, pass.Material, 0,
                            MeshTopology.Triangles, 3, 1, pass.Properties);
                    });
                }
            }
        }
    }
}
