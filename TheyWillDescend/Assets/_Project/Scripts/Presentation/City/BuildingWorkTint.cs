using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Cube colour stub: construction / working / idle. Later the same flags drive Animator.
    /// </summary>
    public static class BuildingWorkTint
    {
        public static void Apply(EntityManager em, Entity entity, BuildingCatalogAsset catalog)
        {
            if (!em.HasComponent<URPMaterialPropertyBaseColor>(entity))
                return;

            var constructing = em.HasComponent<Construction>(entity);
            var working = em.HasComponent<Workplace>(entity)
                && !em.GetComponentData<Workplace>(entity).IsPaused
                && em.GetComponentData<Workplace>(entity).WorkingCount > 0;

            var colors = ResolveColors(em, entity, catalog);
            var color = constructing
                ? colors.construction
                : working
                    ? colors.working
                    : colors.idle;
            em.SetComponentData(entity, new URPMaterialPropertyBaseColor
            {
                Value = new float4(color.r, color.g, color.b, color.a)
            });
        }

        static (Color idle, Color working, Color construction) ResolveColors(
            EntityManager em,
            Entity entity,
            BuildingCatalogAsset catalog)
        {
            var fallback = (
                idle: new Color(0.55f, 0.55f, 0.58f, 1f),
                working: new Color(0.35f, 0.82f, 0.42f, 1f),
                construction: new Color(0.95f, 0.78f, 0.28f, 1f));
            if (catalog == null || !em.HasComponent<Building>(entity))
                return fallback;
            var typeId = em.GetComponentData<Building>(entity).TypeId.ToString();
            var prefab = catalog.FindPrefab(typeId);
            var view = prefab != null ? prefab.GetComponent<BuildingView>() : null;
            if (view == null)
                return fallback;
            return (view.IdleColor, view.WorkingColor, view.ConstructionColor);
        }
    }
}
