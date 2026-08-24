using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.Scenario
{
    /// <summary>
    /// Marks this GameObject's entity as bake-only. Put on every preview GO
    /// (root and mesh children). A baker cannot tag someone else's entity —
    /// children with MeshRenderer / BuildingAuthoring own their own entities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenarioBakingOnlyAuthoring : MonoBehaviour
    {
        class Baker : Baker<ScenarioBakingOnlyAuthoring>
        {
            public override void Bake(ScenarioBakingOnlyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<BakingOnlyEntity>(entity);
            }
        }
    }
}
