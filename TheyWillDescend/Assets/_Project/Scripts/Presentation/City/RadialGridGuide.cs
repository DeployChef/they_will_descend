using System.Collections.Generic;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Io;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Polar underlay: Scene view always via Gizmos; Game view mesh only in build mode.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RadialGridGuide : MonoBehaviour
    {
        [SerializeField] RadialGridConfig config = RadialGridConfig.Default;

        [Header("Underlay")]
        [SerializeField] float yOffset = 0.08f;
        [SerializeField] float lineWidth = 0.05f;
        [SerializeField] Color underlayColor = new(0.15f, 0.55f, 1f, 1f);
        [SerializeField] bool drawSceneGizmos = true;

        MeshFilter _meshFilter;
        MeshRenderer _meshRenderer;
        Mesh _mesh;
        Material _runtimeMaterial;
        int _builtHash;
        bool _buildModeActive;

        public RadialGridConfig Config
        {
            get
            {
                if (Application.isPlaying && TryGetSimGrid(out var grid))
                    return grid.Config;
                return config;
            }
        }

        /// <summary>Game-view mesh underlay only while placing.</summary>
        public void SetBuildModeActive(bool active)
        {
            _buildModeActive = active;
            ApplyPlayMeshVisibility();
            if (active && Application.isPlaying)
                RebuildUnderlayMesh(force: false);
        }

        void OnEnable()
        {
            EnsureComponents();
            if (Application.isPlaying && _buildModeActive)
                RebuildUnderlayMesh(force: true);
            ApplyPlayMeshVisibility();
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
            if (Application.isPlaying && _buildModeActive && isActiveAndEnabled)
                RebuildUnderlayMesh(force: true);
            ApplyPlayMeshVisibility();
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
            if (!Application.isPlaying)
                return;

            FollowCenter();
            if (!_buildModeActive)
                return;

            ApplyPlayMeshVisibility();
            RebuildUnderlayMesh(force: false);
        }

        /// <summary>
        /// Scene view: always (edit or play). Does not affect Game view.
        /// </summary>
        void OnDrawGizmos()
        {
            if (!drawSceneGizmos || !config.IsValid)
                return;

            EnsureConfigDefaults();
            var origin = GetDrawOrigin();
            Gizmos.color = underlayColor;

            for (var ring = 1; ring <= config.RingCount; ring++)
            {
                var radius = config.RingLineRadius(ring);
                var segments = Mathf.Max(48, config.GetClusterCount(Mathf.Min(ring - 1, config.RingCount - 1)));
                DrawGizmoCircle(origin, radius, segments);
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
                    Gizmos.DrawLine(origin + dir * r0, origin + dir * r1);
                }
            }
        }

        void ApplyPlayMeshVisibility()
        {
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
                return;

            // Mesh is for Game view during build only. Edit Mode uses gizmos.
            _meshRenderer.enabled = Application.isPlaying && _buildModeActive && config.IsValid;
        }

        Vector3 GetDrawOrigin()
        {
            if (Application.isPlaying && TryGetSimCenter(out var center))
                return (Vector3)center + Vector3.up * yOffset;
            return transform.position;
        }

        void FollowCenter()
        {
            if (!Application.isPlaying || !TryGetSimCenter(out var center))
                return;
            transform.position = (Vector3)center + Vector3.up * yOffset;
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

        static void DrawGizmoCircle(Vector3 origin, float radius, int segments)
        {
            var prev = origin + new Vector3(0f, 0f, radius);
            for (var i = 1; i <= segments; i++)
            {
                var a = (i / (float)segments) * Mathf.PI * 2f;
                var next = origin + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
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

        static bool TryGetSimGrid(out CityGrid grid)
        {
            grid = default;
            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasComponent<CityGrid>(bag))
                return false;
            grid = em.GetComponentData<CityGrid>(bag);
            return grid.Ready != 0 && grid.Config.IsValid;
        }

        static bool TryGetSimCenter(out float3 center)
        {
            if (!TryGetSimGrid(out var grid))
            {
                center = default;
                return false;
            }

            center = grid.Center;
            return true;
        }
    }
}
