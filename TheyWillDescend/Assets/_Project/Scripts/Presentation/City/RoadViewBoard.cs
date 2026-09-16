using System.Collections.Generic;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Poses authored road-section prefabs along committed <see cref="RoadSegment"/> edges.
    /// Under construction: crew site tint until Construction completes.
    /// </summary>
    public sealed class RoadViewBoard : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] GameObject sectionPrefab;
        [SerializeField] float yOffset = 0.04f;
        [SerializeField] Color finishedColor = new(0.62f, 0.48f, 0.32f, 1f);
        [SerializeField] Color constructionColor = new(0.95f, 0.78f, 0.28f, 1f);

        readonly List<Transform> _views = new(64);
        readonly List<MeshRenderer> _renderers = new(64);
        MaterialPropertyBlock _block;
        Transform _root;
        EntityQuery _sites;

        void LateUpdate() => Pump();

        void OnDisable() => Clear();

        void OnDestroy() => _sites = default;

        public void Pump()
        {
            if (sectionPrefab == null)
                return;
            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasBuffer<RoadSegment>(bag))
            {
                Clear();
                return;
            }

            var grid = em.HasComponent<CityGrid>(bag)
                ? em.GetComponentData<CityGrid>(bag)
                : default;
            var width = grid.Config.IsValid
                ? RoadMath.RoadWidth(grid.Config)
                : 1.6f;
            var y = grid.Center.y + yOffset;
            var segments = em.GetBuffer<RoadSegment>(bag);
            Ensure(segments.Length);
            _block ??= new MaterialPropertyBlock();

            if (_sites == default)
            {
                _sites = em.CreateEntityQuery(
                    ComponentType.ReadOnly<RoadSpan>(),
                    ComponentType.ReadOnly<Building>());
            }

            var progress = new NativeHashMap<int, float>(math.max(8, _sites.CalculateEntityCount()), Allocator.Temp);
            using (var siteEntities = _sites.ToEntityArray(Allocator.Temp))
            using (var buildings = _sites.ToComponentDataArray<Building>(Allocator.Temp))
            {
                for (var i = 0; i < buildings.Length; i++)
                {
                    var amount = 1f;
                    if (em.HasComponent<Construction>(siteEntities[i]))
                        amount = em.GetComponentData<Construction>(siteEntities[i]).Normalized;
                    progress.TryAdd(buildings[i].Id, amount);
                }
            }

            for (var i = 0; i < segments.Length; i++)
            {
                var a = new RoadNode { Ring = segments[i].RingA, Fine = segments[i].FineA };
                var b = new RoadNode { Ring = segments[i].RingB, Fine = segments[i].FineB };
                RoadSectionPose.Apply(
                    _views[i], y,
                    RoadMath.NodeWorld(grid.Center, grid.Config, a),
                    RoadMath.NodeWorld(grid.Center, grid.Config, b),
                    width);
                var done = 1f;
                if (segments[i].SiteId != 0)
                    progress.TryGetValue(segments[i].SiteId, out done);
                var color = Color.Lerp(constructionColor, finishedColor, done);
                if (i < _renderers.Count && _renderers[i] != null)
                {
                    _block.Clear();
                    _block.SetColor(ColorId, color);
                    _renderers[i].SetPropertyBlock(_block);
                }
            }

            progress.Dispose();
            for (var i = segments.Length; i < _views.Count; i++)
            {
                if (_views[i] != null)
                    _views[i].gameObject.SetActive(false);
            }
        }

        void Ensure(int count)
        {
            if (_root == null)
            {
                var go = new GameObject("RoadViews");
                go.transform.SetParent(transform, false);
                _root = go.transform;
            }

            while (_views.Count < count)
            {
                var instance = Instantiate(sectionPrefab, _root);
                instance.name = $"Road_{_views.Count}";
                _views.Add(instance.transform);
                _renderers.Add(instance.GetComponentInChildren<MeshRenderer>());
            }
        }

        void Clear()
        {
            for (var i = 0; i < _views.Count; i++)
            {
                if (_views[i] != null)
                    _views[i].gameObject.SetActive(false);
            }
        }
    }
}
