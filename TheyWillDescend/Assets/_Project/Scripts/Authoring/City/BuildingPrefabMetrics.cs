using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    public static class BuildingPrefabMetrics
    {
        public static float HorizontalSize(GameObject prefab)
        {
            if (prefab == null)
                return 1f;
            var size = 0f;
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
            {
                if (filters[i].GetComponentInParent<BakeStripAuthoring>() != null)
                    continue;
                var mesh = filters[i].sharedMesh;
                if (mesh == null)
                    continue;
                var s = mesh.bounds.size;
                var ls = filters[i].transform.localScale;
                var x = s.x * Mathf.Abs(ls.x);
                var z = s.z * Mathf.Abs(ls.z);
                if (x > size)
                    size = x;
                if (z > size)
                    size = z;
            }

            return size > 0.001f ? size : 1f;
        }
    }
}
