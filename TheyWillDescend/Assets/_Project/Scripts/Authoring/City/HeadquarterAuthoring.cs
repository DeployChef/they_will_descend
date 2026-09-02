using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Gods;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    /// <summary>
    /// Main plaza building. Place on the Simulation SubScene.
    /// </summary>
    public sealed class HeadquarterAuthoring : MonoBehaviour
    {
        [SerializeField] int buildingId = 1;

        class HeadquarterBaker : Baker<HeadquarterAuthoring>
        {
            public override void Bake(HeadquarterAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Building
                {
                    Id = authoring.buildingId > 0 ? authoring.buildingId : 1
                });
                AddComponent<Headquarters>(entity);
                AddComponent(entity, new BuildingMeshSize
                {
                    Horizontal = BuildingPrefabMetrics.HorizontalSize(authoring.gameObject)
                });
                AddBuffer<PyramidFeedLine>(entity);
            }
        }
    }
}
