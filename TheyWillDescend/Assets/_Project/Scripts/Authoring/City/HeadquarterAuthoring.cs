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
                AddComponent<Headquarters>(entity);
                AddBuffer<PyramidFeedLine>(entity);

            }
        }
    }
}
