using TheyWillDescend.Simulation.City;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Presentation view for the unique central Pyramid (Headquarters).
    /// Placed statically in the scene at (0, 0, 0).
    /// Handles its own visuals, plaza ring, and selection highlight.
    /// Completely separate from BuildingViewBoard (which is strictly for player-constructed buildings).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PyramidView : MonoBehaviour
    {
        [SerializeField] MeshRenderer visualRenderer;
        [SerializeField] MeshFilter plazaFilter;
        [SerializeField] MeshRenderer plazaRenderer;
        [SerializeField] BuildingSelection selection;
        [SerializeField] Color zoneColor = new(0.15f, 0.75f, 1f, 0.45f);
        [SerializeField] Color selectedZoneColor = new(0.95f, 0.82f, 0.2f, 0.55f);

        Material _placedZoneMaterial;
        Material _selectedZoneMaterial;

        public MeshRenderer VisualRenderer => visualRenderer;
        public MeshRenderer PlazaRenderer => plazaRenderer;

        void Awake()
        {
            if (selection == null)
                selection = FindFirstObjectByType<BuildingSelection>();

            EnsureMaterials();
            EnsurePlazaMesh();
        }

        void OnDestroy()
        {
            if (_placedZoneMaterial != null)
                Destroy(_placedZoneMaterial);
            if (_selectedZoneMaterial != null)
                Destroy(_selectedZoneMaterial);
        }

        void Update()
        {
            if (plazaRenderer == null || selection == null)
                return;

            var isSelected = selection.IsPyramidSelected;
            plazaRenderer.sharedMaterial = isSelected ? _selectedZoneMaterial : _placedZoneMaterial;
        }

        void EnsureMaterials()
        {
            if (_placedZoneMaterial != null)
                return;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            _placedZoneMaterial = new Material(shader)
            {
                name = "Pyramid_PlazaZone",
                color = zoneColor
            };
            SetTransparent(_placedZoneMaterial);

            _selectedZoneMaterial = new Material(shader)
            {
                name = "Pyramid_SelectedZone",
                color = selectedZoneColor
            };
            SetTransparent(_selectedZoneMaterial);

            if (plazaRenderer != null)
            {
                plazaRenderer.sharedMaterial = _placedZoneMaterial;
                plazaRenderer.shadowCastingMode = ShadowCastingMode.Off;
                plazaRenderer.receiveShadows = false;
            }
        }

        void EnsurePlazaMesh()
        {
            if (plazaFilter == null || plazaFilter.sharedMesh != null)
                return;

            var radius = RadialGridConfig.Default.InnerRadius;
            plazaFilter.sharedMesh = BuildAnnulusMesh(radius * 0.55f, radius * 0.92f, 48, 0.06f);
        }

        static Mesh BuildAnnulusMesh(float inner, float outer, int segments, float y)
        {
            var verts = new Vector3[segments * 2];
            var tris = new int[segments * 12];
            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var c = Mathf.Cos(angle);
                var s = Mathf.Sin(angle);
                verts[i * 2] = new Vector3(c * inner, y, s * inner);
                verts[i * 2 + 1] = new Vector3(c * outer, y, s * outer);
                var next = (i + 1) % segments;
                var i0 = i * 2;
                var i1 = i * 2 + 1;
                var i2 = next * 2;
                var i3 = next * 2 + 1;
                var t = i * 12;
                tris[t] = i0;
                tris[t + 1] = i1;
                tris[t + 2] = i2;
                tris[t + 3] = i1;
                tris[t + 4] = i3;
                tris[t + 5] = i2;
                tris[t + 6] = i1;
                tris[t + 7] = i0;
                tris[t + 8] = i2;
                tris[t + 9] = i3;
                tris[t + 10] = i1;
                tris[t + 11] = i2;
            }

            var mesh = new Mesh { name = "PyramidPlazaMesh" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void SetTransparent(Material m)
        {
            if (m == null)
                return;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}