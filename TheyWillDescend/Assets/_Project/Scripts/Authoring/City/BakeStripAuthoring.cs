using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    /// <summary>
    /// Child stays on the prefab asset (Prefab Mode) and is stripped from the
    /// baked ECS stamp. World UI preview uses this so Canvas is not Instantiated
    /// as an entity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BakeStripAuthoring : MonoBehaviour
    {
        class Baker : Baker<BakeStripAuthoring>
        {
            public override void Bake(BakeStripAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<BakingOnlyEntity>(entity);
            }
        }
    }
}
