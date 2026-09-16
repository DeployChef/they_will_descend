using TheyWillDescend.Simulation.City;
using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Prefab footprint chrome: authored quad + outline shader.
    /// Code only poses the quad and pushes polar uniforms.
    /// </summary>
    public sealed class BuildingOverlay : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int CityCenterId = Shader.PropertyToID("_CityCenter");
        static readonly int InnerRadiusId = Shader.PropertyToID("_InnerRadius");
        static readonly int OuterRadiusId = Shader.PropertyToID("_OuterRadius");
        static readonly int Angle0Id = Shader.PropertyToID("_Angle0");
        static readonly int AngleSpanId = Shader.PropertyToID("_AngleSpan");
        static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");

        [SerializeField] BuildingIdTag idTag;
        [SerializeField] MeshFilter zoneFilter;
        [SerializeField] MeshRenderer zoneRenderer;
        [SerializeField] MeshCollider zoneCollider;
        [SerializeField] float yOffset = 0.12f;

        Material _instance;
        bool _ownsInstance;

        public BuildingIdTag IdTag => idTag;

        public MeshFilter ZoneFilter => zoneFilter;

        public MeshRenderer ZoneRenderer => zoneRenderer;

        public MeshCollider ZoneCollider => zoneCollider;

        public void SetVisible(bool visible)
        {
            if (zoneRenderer != null)
                zoneRenderer.enabled = visible;
            if (zoneCollider != null)
                zoneCollider.enabled = false;
        }

        public void SetTint(Color color)
        {
            if (!EnsureInstance())
                return;
            _instance.SetColor(ColorId, color);
        }

        public bool ApplyFootprint(
            float3 cityCenter,
            in RadialGridConfig config,
            float turns0,
            int anchorRadial,
            in BuildingFootprint footprint,
            float lineWidth)
        {
            if (!TrySector(config, turns0, anchorRadial, footprint, out var r0, out var r1, out var a0, out var span))
                return false;

            PoseOverSector(cityCenter, r0, r1, a0, span, lineWidth);
            if (!EnsureInstance())
                return true;

            _instance.SetVector(CityCenterId, new Vector4(cityCenter.x, cityCenter.y, cityCenter.z, 0f));
            _instance.SetFloat(InnerRadiusId, r0);
            _instance.SetFloat(OuterRadiusId, r1);
            _instance.SetFloat(Angle0Id, a0);
            _instance.SetFloat(AngleSpanId, span);
            _instance.SetFloat(LineWidthId, Mathf.Max(0.02f, lineWidth));
            return true;
        }

        bool EnsureInstance()
        {
            if (_instance != null)
                return true;
            if (zoneRenderer == null)
                return false;
            if (zoneRenderer.sharedMaterial == null)
                return false;
            _instance = zoneRenderer.material;
            _ownsInstance = true;
            return true;
        }

        void OnDestroy()
        {
            if (!_ownsInstance || _instance == null)
                return;
            if (Application.isPlaying)
                Destroy(_instance);
            else
                DestroyImmediate(_instance);
            _instance = null;
        }

        void PoseOverSector(
            float3 cityCenter, float r0, float r1, float a0, float span, float lineWidth)
        {
            var pad = Mathf.Max(0.02f, lineWidth) * 2f;
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;
            EncapsulateArc(cityCenter, r0, a0, span, ref minX, ref maxX, ref minZ, ref maxZ);
            EncapsulateArc(cityCenter, r1, a0, span, ref minX, ref maxX, ref minZ, ref maxZ);

            var visual = zoneFilter != null ? zoneFilter.transform : transform;
            var midX = (minX + maxX) * 0.5f;
            var midZ = (minZ + maxZ) * 0.5f;
            visual.SetPositionAndRotation(
                new Vector3(midX, cityCenter.y + yOffset, midZ),
                Quaternion.Euler(90f, 0f, 0f));
            visual.localScale = new Vector3(
                Mathf.Max(0.05f, maxX - minX + pad),
                Mathf.Max(0.05f, maxZ - minZ + pad),
                1f);
            if (zoneCollider != null)
                zoneCollider.enabled = false;
        }

        static bool TrySector(
            in RadialGridConfig config,
            float turns0,
            int anchorRadial,
            in BuildingFootprint footprint,
            out float r0,
            out float r1,
            out float a0,
            out float span)
        {
            r0 = r1 = a0 = span = 0f;
            if (!config.IsValid || !footprint.IsValid)
                return false;
            if (anchorRadial < 0 || anchorRadial + footprint.DepthRadialRings > config.RingCount)
                return false;
            var n = config.GetClusterCount(anchorRadial);
            if (n <= 0)
                return false;

            turns0 -= Mathf.Floor(turns0);
            if (turns0 < 0f)
                turns0 += 1f;
            span = footprint.WidthClusters / (float)n * Mathf.PI * 2f;
            a0 = turns0 * Mathf.PI * 2f;
            r0 = config.RingLineRadius(anchorRadial);
            r1 = config.RingLineRadius(anchorRadial + footprint.DepthRadialRings);
            return r1 > r0 && span > 0f;
        }

        static void EncapsulateArc(
            float3 center, float radius, float a0, float span,
            ref float minX, ref float maxX, ref float minZ, ref float maxZ)
        {
            Encapsulate(Polar(center, a0, radius), ref minX, ref maxX, ref minZ, ref maxZ);
            Encapsulate(Polar(center, a0 + span, radius), ref minX, ref maxX, ref minZ, ref maxZ);
            const float step = 1.57079637f;
            var start = Mathf.Ceil(a0 / step) * step;
            for (var a = start; a < a0 + span; a += step)
                Encapsulate(Polar(center, a, radius), ref minX, ref maxX, ref minZ, ref maxZ);
        }

        static Vector3 Polar(float3 center, float angle, float radius) =>
            new(center.x + Mathf.Sin(angle) * radius, 0f, center.z + Mathf.Cos(angle) * radius);

        static void Encapsulate(
            Vector3 p, ref float minX, ref float maxX, ref float minZ, ref float maxZ)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.z < minZ) minZ = p.z;
            if (p.z > maxZ) maxZ = p.z;
        }
    }
}
