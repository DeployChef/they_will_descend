using TheyWillDescend.Content;
using TheyWillDescend.Simulation.City;
using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Authoring.Scenario
{
    /// <summary>
    /// Editor manipulator for one scenario house. Pose is a view of (type, cluster, ring).
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class ScenarioBuildingPreview : MonoBehaviour
    {
        [SerializeField] string typeId;
        [SerializeField] int cluster;
        [SerializeField] int radial;

        public string TypeId
        {
            get => typeId;
            set => typeId = value ?? string.Empty;
        }

        public int Cluster
        {
            get => cluster;
            set => cluster = math.max(0, value);
        }

        public int Radial
        {
            get => radial;
            set => radial = math.max(0, value);
        }

        public ScenarioBuildingRecord ToRecord() => new()
        {
            TypeId = typeId,
            Cluster = cluster,
            Radial = radial
        };

        public void ApplyPose(
            in RadialGridConfig config,
            float3 center,
            in BuildingFootprint footprint)
        {
            RadialFootprintMath.FootprintMarkerPose(
                center, config, cluster, radial, footprint,
                out var position, out var rotation);
            transform.SetPositionAndRotation(position, rotation);
        }

        public bool SnapFromWorld(
            in RadialGridConfig config,
            float3 center,
            in BuildingFootprint footprint)
        {
            if (!RadialFootprintMath.TrySnapAnchor(
                    center, config, (float3)transform.position, out var snappedCluster, out var snappedRadial))
                return false;
            cluster = snappedCluster;
            radial = snappedRadial;
            ApplyPose(config, center, footprint);
            return true;
        }
    }
}
