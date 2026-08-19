using TheyWillDescend.Simulation.Economy;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.Economy
{
    /// <summary>
    /// Starting ledger. Lives on the Simulation SubScene like GameTime.
    /// </summary>
    public sealed class ResourceStockAuthoring : MonoBehaviour
    {
        [SerializeField] float resource1;
        [SerializeField] float resource2;
        [SerializeField] float resource3;
        [SerializeField] float resource4;

        class ResourceStockBaker : Baker<ResourceStockAuthoring>
        {
            public override void Bake(ResourceStockAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ResourceStock
                {
                    Resource1 = authoring.resource1,
                    Resource2 = authoring.resource2,
                    Resource3 = authoring.resource3,
                    Resource4 = authoring.resource4
                });
            }
        }
    }
}
