using System.Collections.Generic;
using _Project.Scripts.Simulation.City;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.Scripts.Presentation.City
{
    /// <summary>
    /// Visual underlay only: rings + per-ring cluster spokes.
    /// No fine micro-grid (FP has none — roads are freer later).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RadialGridGuide : MonoBehaviour
    {
        [SerializeField] Transform cityCenter;
        [SerializeField] RadialGridConfig config = RadialGridConfig.Default;

        [Header("Underlay")]
        [SerializeField] float yOffset = 0.08f;
        [SerializeField] float lineWidth = 0.05f;
        [SerializeField] Color underlayColor = new(0.15f, 0.55f, 1f, 1f);
        [SerializeField] bool visibleInEditMode = true;
        [SerializeField] bool visibleInPlayMode = true;

        MeshFilter _meshFilter;
        MeshRenderer _meshRenderer;
        Mesh _mesh;
        Material _runtimeMaterial;
        int _builtHash;

        public RadialGridConfig Config => config;
        public Transform CenterTransform => cityCenter != null ? cityCenter : transform;

        void OnEnable()
        {
            EnsureComponents();
            RebuildUnderlayMesh(force: true);
        }

        void OnDisable()
        {
            if (_mesh == null)
                return;
            if (Application.isPlaying)
                Destroy(_mesh);
            else
                DestroyImmediate(_mesh);
            _mesh = null;
        }

        void OnDestroy()
        {
            if (_runtimeMaterial == null)
                return;
            if (Application.isPlaying)
                Destroy(_runtimeMaterial);
            else
                DestroyImmediate(_runtimeMaterial);
            _runtimeMaterial = null;
        }

        void OnValidate()
        {
            lineWidth = Mathf.Max(0.01f, lineWidth);
            EnsureConfigDefaults();
            if (isActiveAndEnabled)
                RebuildUnderlayMesh(force: true);
        }

        void EnsureConfigDefaults()
        {
            if (config.RingCount <= 0)
                config.RingCount = RadialGridConfig.Default.RingCount;
            if (config.RadialStep <= 0f)
                config.RadialStep = RadialGridConfig.Default.RadialStep;
            if (config.InnerBandClusterCount <= 0)
                config.InnerBandClusterCount = RadialGridConfig.Default.InnerBandClusterCount;
        }

        void LateUpdate()
        {
            FollowCenter();
            var want = Application.isPlaying ? visibleInPlayMode : visibleInEditMode;
            if (_meshRenderer != null)
                _meshRenderer.enabled = want && config.IsValid;
            if (want)
                RebuildUnderlayMesh(force: false);
        }

        void FollowCenter()
        {
            var c = CenterTransform;
            if (c == null)
                return;
            transform.position = c.position + Vector3.up * yOffset;
            transform.rotation = Quaternion.identity;
        }

        void EnsureComponents()
        {
            _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null)
                _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            if (_runtimeMaterial == null)
                _runtimeMaterial = CreateLineMaterial(underlayColor);

            _meshRenderer.sharedMaterial = _runtimeMaterial;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            _meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void RebuildUnderlayMesh(bool force)
        {
            EnsureConfigDefaults();
            if (!config.IsValid)
                return;

            EnsureComponents();
            FollowCenter();

            var hash = ComputeHash();
            if (!force && hash == _builtHash && _mesh != null)
                return;
            _builtHash = hash;

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "RadialClusterUnderlay" };
                _mesh.MarkDynamic();
                _mesh.indexFormat = IndexFormat.UInt32;
            }
            else
                _mesh.Clear();

            var halfW = lineWidth * 0.5f;
            var verts = new List<Vector3>(8192);
            var tris = new List<int>(16384);

            for (var ring = 1; ring <= config.RingCount; ring++)
            {
                var radius = config.RingLineRadius(ring);
                var segments = Mathf.Max(48, config.GetClusterCount(Mathf.Min(ring - 1, config.RingCount - 1)));
                AppendCircleRibbon(verts, tris, radius, segments, halfW);
            }

            for (var ring = 0; ring < config.RingCount; ring++)
            {
                var r0 = config.RingLineRadius(ring);
                var r1 = config.RingLineRadius(ring + 1);
                var clusters = config.GetClusterCount(ring);
                for (var i = 0; i < clusters; i++)
                {
                    var theta = (i / (float)clusters) * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Sin(theta), 0f, Mathf.Cos(theta));
                    AppendSegmentRibbon(verts, tris, dir * r0, dir * r1, halfW);
                }
            }

            _mesh.SetVertices(verts);
            _mesh.SetTriangles(tris, 0, true);
            _mesh.RecalculateBounds();
            _mesh.RecalculateNormals();
            _meshFilter.sharedMesh = _mesh;
            ApplyColor(_runtimeMaterial, underlayColor);
        }

        static void AppendCircleRibbon(
            List<Vector3> verts, List<int> tris, float radius, int segments, float halfWidth)
        {
            for (var i = 0; i < segments; i++)
            {
                var a0 = (i / (float)segments) * Mathf.PI * 2f;
                var a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius;
                var p1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius;
                AppendSegmentRibbon(verts, tris, p0, p1, halfWidth);
            }
        }

        static void AppendSegmentRibbon(
            List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, float halfWidth)
        {
            var delta = b - a;
            if (delta.sqrMagnitude < 1e-8f)
                return;
            var perp = Vector3.Cross(delta.normalized, Vector3.up);
            if (perp.sqrMagnitude < 1e-8f)
                perp = Vector3.right;
            else
                perp.Normalize();
            perp *= halfWidth;

            var i0 = verts.Count;
            verts.Add(a - perp);
            verts.Add(a + perp);
            verts.Add(b + perp);
            verts.Add(b - perp);
            tris.Add(i0);
            tris.Add(i0 + 1);
            tris.Add(i0 + 2);
            tris.Add(i0);
            tris.Add(i0 + 2);
            tris.Add(i0 + 3);
        }

        int ComputeHash()
        {
            unchecked
            {
                var h = config.InnerRadius.GetHashCode();
                h = (h * 397) ^ config.RadialStep.GetHashCode();
                h = (h * 397) ^ config.RingCount;
                h = (h * 397) ^ config.InnerBandClusterCount;
                h = (h * 397) ^ lineWidth.GetHashCode();
                return h;
            }
        }

        static Material CreateLineMaterial(Color color)
        {
            var shader =
                Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader)
            {
                name = "RadialUnderlay_Runtime",
                hideFlags = HideFlags.HideAndDontSave
            };
            ApplyColor(mat, color);
            mat.renderQueue = (int)RenderQueue.Transparent + 50;
            return mat;
        }

        static void ApplyColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            mat.color = color;
        }
    }
}
