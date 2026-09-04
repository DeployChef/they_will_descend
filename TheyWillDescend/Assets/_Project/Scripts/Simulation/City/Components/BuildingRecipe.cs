using TheyWillDescend.Simulation.Economy;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    public enum BuildingRecipeKind : byte
    {
        Input = 1,
        Output = 2
    }

    /// <summary>
    /// Recipe row on a house instance. <see cref="PerHour"/> is the HUD unit
    /// (game hour). Catalog rows live in <see cref="BuildingCatalogRecipe"/>.
    /// </summary>
    public struct BuildingRecipeLine : IBufferElementData
    {
        public BuildingRecipeKind Kind;
        public FixedString64Bytes ResourceId;
        public float PerHour;
    }

    /// <summary>
    /// Recipe row for one catalog type. Lives on the session; Place copies matching
    /// rows onto the instance as <see cref="BuildingRecipeLine"/>.
    /// </summary>
    public struct BuildingCatalogRecipe : IBufferElementData
    {
        public FixedString64Bytes TypeId;
        public BuildingRecipeKind Kind;
        public FixedString64Bytes ResourceId;
        public float PerHour;
    }

    /// <summary>
    /// Immutable prefab-default recipe row. Run setup rebuilds the resolved
    /// <see cref="BuildingCatalogRecipe"/> buffer from these rows.
    /// </summary>
    public struct BaseBuildingCatalogRecipe : IBufferElementData
    {
        public FixedString64Bytes TypeId;
        public BuildingRecipeKind Kind;
        public FixedString64Bytes ResourceId;
        public float PerHour;

        public BuildingCatalogRecipe ToResolved() => new()
        {
            TypeId = TypeId,
            Kind = Kind,
            ResourceId = ResourceId,
            PerHour = PerHour
        };
    }

    public static class BuildingRecipes
    {
        public static float FrameAmount(float perHour, float deltaTime, float dayDuration)
        {
            if (perHour <= 0.0001f || deltaTime <= 0f || dayDuration < 0.0001f)
                return 0f;
            return perHour * deltaTime * 24f / dayDuration;
        }

        public static bool HasLines(DynamicBuffer<BuildingRecipeLine> recipes)
        {
            for (var i = 0; i < recipes.Length; i++)
            {
                if (recipes[i].PerHour > 0.0001f)
                    return true;
            }

            return false;
        }

        public static bool CanRun(
            DynamicBuffer<BuildingRecipeLine> recipes,
            DynamicBuffer<ResourceAmount> stock,
            float deltaTime,
            float dayDuration,
            float load01)
        {
            if (load01 <= 0.0001f)
                return false;
            for (var i = 0; i < recipes.Length; i++)
            {
                var line = recipes[i];
                if (line.Kind != BuildingRecipeKind.Input || line.PerHour <= 0.0001f)
                    continue;
                var need = FrameAmount(line.PerHour, deltaTime, dayDuration) * load01;
                if (!ResourceLedger.Has(stock, line.ResourceId, need))
                    return false;
            }

            return true;
        }

        public static void Apply(
            DynamicBuffer<BuildingRecipeLine> recipes,
            DynamicBuffer<ResourceAmount> stock,
            DynamicBuffer<ResourceInfo> info,
            float deltaTime,
            float dayDuration,
            float load01)
        {
            if (!CanRun(recipes, stock, deltaTime, dayDuration, load01))
                return;

            for (var i = 0; i < recipes.Length; i++)
            {
                var line = recipes[i];
                if (line.PerHour <= 0.0001f)
                    continue;
                var amount = FrameAmount(line.PerHour, deltaTime, dayDuration) * load01;
                if (amount <= 0f)
                    continue;
                if (line.Kind == BuildingRecipeKind.Input)
                    ResourceLedger.Add(stock, line.ResourceId, -amount);
                else if (line.Kind == BuildingRecipeKind.Output)
                    ResourceLedger.AddClamped(stock, info, line.ResourceId, amount);
            }
        }
    }
}
