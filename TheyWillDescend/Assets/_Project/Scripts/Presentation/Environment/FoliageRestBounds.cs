using UnityEngine;

namespace TheyWillDescend.Presentation.Environment
{
    /// <summary>
    /// Authored root-space bounds shared by a plant's LODs. Geometry remains
    /// non-readable; wind-padded render bounds never determine the size policy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FoliageRestBounds : MonoBehaviour
    {
        [SerializeField] Bounds restingBounds;
        [SerializeField] Mesh[] meshes = System.Array.Empty<Mesh>();

        public Bounds RestingBounds => restingBounds;

        public static bool TryGet(MeshFilter filter, out Bounds bounds, out Transform space)
        {
            bounds = default;
            space = filter.transform;
            var authored = filter.GetComponentInParent<FoliageRestBounds>();
            if (!authored || authored.restingBounds.size.sqrMagnitude <= .000001f)
                return false;
            foreach (var mesh in authored.meshes)
            {
                if (!mesh || mesh != filter.sharedMesh) continue;
                bounds = authored.restingBounds;
                space = authored.transform;
                return true;
            }
            return false;
        }
    }
}
