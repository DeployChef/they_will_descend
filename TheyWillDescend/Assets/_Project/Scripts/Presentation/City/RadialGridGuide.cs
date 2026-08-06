using System.Collections.Generic;
using _Project.Scripts.Simulation.City;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.Scripts.Presentation.City
{
    /// <summary>
    /// Shared ring radii for fine and quantum.
    /// Play mesh = quantum underlay (fewer angular spokes).
    /// Scene gizmos = full fine angular math on the same rings.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RadialGridGuide : MonoBehaviour
    {
        [SerializeField] Transform cityCenter;
        [SerializeField] RadialGridConfig config = RadialGridConfig.Default;

        [Header("In-game underlay (quanta on shared rings)")]
        [SerializeField] float yOffset = 0.08f;
        [SerializeField] float lineWidth = 0.08f;
        [SerializeField] Color underlayColor = new(0.15f, 0.55f, 1f, 1f);
        [SerializeField] bool underlayVisibleInEditMode = true;
        [SerializeField] bool underlayVisibleInPlayMode = true;

        [Header("Scene gizmos (fine math, same rings)")]
        [SerializeField] bool fineGridGizmosInScene = true;
        [SerializeField] Color fineGizmoColor = new(0.2f, 1f, 0.4f, 0.35f);

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
            if (config.AngularDivisions <= 0)
                config.AngularDivisions = RadialGridConfig.Default.AngularDivisions;
            if (config.AngularQuantum <= 0)
                config.AngularQuantum = RadialGridConfig.Default.AngularQuantum;

            if (config.AngularDivisions % config.AngularQuantum != 0)
                config.AngularDivisions =
                    (config.AngularDivisions / config.AngularQuantum) * config.AngularQuantum;
            if (config.AngularDivisions <= 0)
                config.AngularDivisions = config.AngularQuantum;
        }

        void LateUpdate()
        {
            FollowCenter();
            var wantUnderlay = Application.isPlaying ? underlayVisibleInPlayMode : underlayVisibleInEditMode;
            if (_meshRenderer != null)
                _meshRenderer.enabled = wantUnderlay && config.IsValid;

            if (wantUnderlay)
                RebuildUnderlayMesh(force: false);
        }

        void FollowCenter()
        {
            var center = CenterTransform;
            if (center == null)
                return;
            transform.position = center.position + Vector3.up * yOffset;
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
                _mesh = new Mesh { name = "RadialQuantumUnderlay" };
                _mesh.MarkDynamic();
                _mesh.indexFormat = IndexFormat.UInt32;
            }
            else
            {
                _mesh.Clear();
            }

            // Quantum angular density, SHARED ring radii with fine.
            var spokeCount = Mathf.Max(1, config.AngularQuantaCount);
            var ringCount = config.RingCount;
            var segments = Mathf.Max(64, spokeCount);
            var inner = config.InnerRadius;
            var outer = config.RingLineRadius(ringCount);
            var halfW = lineWidth * 0.5f;

            var verts = new List<Vector3>(4096);
            var tris = new List<int>(8192);

            for (var ring = 1; ring <= ringCount; ring++)
            {
                var radius = config.RingLineRadius(ring);
                AppendCircleRibbon(verts, tris, radius, segments, halfW);
            }

            for (var i = 0; i < spokeCount; i++)
            {
                var theta = (i / (float)spokeCount) * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Sin(theta), 0f, Mathf.Cos(theta));
                AppendSegmentRibbon(verts, tris, dir * inner, dir * outer, halfW);
            }

            _mesh.SetVertices(verts);
            _mesh.SetTriangles(tris, 0, true);
            _mesh.RecalculateBounds();
            _mesh.RecalculateNormals();
            _meshFilter.sharedMesh = _mesh;

            if (_runtimeMaterial != null)
                ApplyColor(_runtimeMaterial, underlayColor);
        }

        static void AppendCircleRibbon(
            List<Vector3> verts,
            List<int> tris,
            float radius,
            int segments,
            float halfWidth)
        {
            for (var i = 0; i < segments; i++)
            {
                var t0 = (float)i / segments;
                var t1 = (float)(i + 1) / segments;
                var a0 = t0 * Mathf.PI * 2f;
                var a1 = t1 * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius;
                var p1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius;
                AppendSegmentRibbon(verts, tris, p0, p1, halfWidth);
            }
        }

        static void AppendSegmentRibbon(
            List<Vector3> verts,
            List<int> tris,
            Vector3 a,
            Vector3 b,
            float halfWidth)
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
                h = (h * 397) ^ config.AngularDivisions;
                h = (h * 397) ^ config.AngularQuantum;
                h = (h * 397) ^ yOffset.GetHashCode();
                h = (h * 397) ^ lineWidth.GetHashCode();
                h = (h * 397) ^ underlayColor.GetHashCode();
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

        /// <summary>
        /// Scene-only fine math: same ring lines, denser angular spokes.
        /// </summary>
        void OnDrawGizmos()
        {
            if (!fineGridGizmosInScene || !config.IsValid)
                return;

            EnsureConfigDefaults();

            var center = CenterTransform.position + Vector3.up * yOffset;
            var inner = config.InnerRadius;
            var outer = config.RingLineRadius(config.RingCount);
            var fineSpokes = Mathf.Max(1, config.AngularDivisions);
            var segments = Mathf.Max(32, Mathf.Min(fineSpokes, 128));

            Gizmos.color = fineGizmoColor;

            // SAME ring radii as underlay.
            for (var ring = 1; ring <= config.RingCount; ring++)
                DrawGizmoCircle(center, config.RingLineRadius(ring), segments);

            for (var i = 0; i < fineSpokes; i++)
            {
                var theta = (i / (float)fineSpokes) * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Sin(theta), 0f, Mathf.Cos(theta));
                Gizmos.DrawLine(center + dir * inner, center + dir * outer);
            }
        }

        static void DrawGizmoCircle(Vector3 center, float radius, int segments)
        {
            var prev = center + new Vector3(0f, 0f, radius);
            for (var i = 1; i <= segments; i++)
            {
                var t = (float)i / segments;
                var a = t * Mathf.PI * 2f;
                var next = center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
