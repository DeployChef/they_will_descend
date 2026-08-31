using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Session catalog: type key → baked stamp. Numbers live on the prefab entity
    /// (<see cref="BuildingType"/>, costs, recipe), not copied here.
    /// </summary>
    public struct BuildingPrototype : IBufferElementData
    {
        public FixedString64Bytes TypeId;
        public Entity Prefab;
    }

    public static class BuildingCatalog
    {
        public static bool TryResolve(
            DynamicBuffer<BuildingPrototype> catalog,
            in FixedString64Bytes typeId,
            out BuildingPrototype prototype)
        {
            prototype = default;
            if (typeId.IsEmpty || catalog.Length == 0)
                return false;

            for (var i = 0; i < catalog.Length; i++)
            {
                if (catalog[i].TypeId != typeId || catalog[i].Prefab == Entity.Null)
                    continue;
                prototype = catalog[i];
                return true;
            }

            return false;
        }
    }
}
