using System;
using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Paint or erase roads. Hover shows one section (green / red) like a house ghost.
    /// </summary>
    public sealed class RoadPaintController : MonoBehaviour
    {
        const int ModeOff = 0;
        const int ModePaint = 1;
        const int ModeErase = 2;

        [SerializeField] RadialGridGuide gridGuide;
        [SerializeField] GameObject sectionPrefab;
        [SerializeField] Color validColor = new(0.35f, 0.82f, 0.42f, 1f);
        [SerializeField] Color invalidColor = new(0.92f, 0.22f, 0.16f, 1f);
        [SerializeField] Color eraseColor = new(0.95f, 0.38f, 0.22f, 1f);
        [SerializeField] Color lockedColor = new(0.5f, 0.5f, 0.52f, 1f);
        [SerializeField] float yOffset = 0.05f;

        readonly List<RoadNode> _path = new(64);
        readonly List<Transform> _ghosts = new(32);
        readonly List<Material> _ghostMats = new(32);

        int _mode;
        bool _dragging;
        float2 _start;
        int _validCount;
        RoadNode _hoverA;
        RoadNode _hoverB;
        bool _hoverValid;
        int _eraseHoverId;
        int _eraseLastId;
        Transform _ghostRoot;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly FixedString64Bytes RoadType = new(RoadNetwork.TypeId);

        public bool IsPainting => _mode != ModeOff;

        public event Action Finished;

        public void BeginPainting()
        {
            if (!CanBegin())
                return;
            _mode = ModePaint;
            ResetStroke();
            gridGuide.SetBuildModeActive(true);
            GameLog.Info("Road paint mode.");
        }

        public void BeginErasing()
        {
            if (!CanBegin())
                return;
            _mode = ModeErase;
            ResetStroke();
            gridGuide.SetBuildModeActive(true);
            GameLog.Info("Road erase mode.");
        }

        public void CancelPainting()
        {
            if (_mode == ModeOff)
                return;
            _mode = ModeOff;
            ResetStroke();
            if (gridGuide != null)
                gridGuide.SetBuildModeActive(false);
            SetGhosts(0);
            GameLog.Info("Road tool cancelled.");
        }

        bool CanBegin()
        {
            if (gridGuide == null)
            {
                GameLog.Error("RoadPaintController: RadialGridGuide is not assigned.");
                return false;
            }

            if (sectionPrefab == null)
            {
                GameLog.Error("RoadPaintController: assign road section prefab.");
                return false;
            }

            return true;
        }

        void ResetStroke()
        {
            _dragging = false;
            _validCount = 0;
            _hoverValid = false;
            _eraseHoverId = 0;
            _eraseLastId = 0;
            _path.Clear();
            SetGhosts(0);
        }

        void Update()
        {
            if (_mode == ModeOff)
                return;

            if (Mouse.current != null
                && Mouse.current.rightButton.wasPressedThisFrame
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                CancelPainting();
                Finished?.Invoke();
                return;
            }

            if (gridGuide == null || !TryGetCity(out var center, out var config))
                return;

            if (!TryPointer(center.y, out var world))
            {
                if (!_dragging)
                    SetGhosts(0);
                return;
            }

            var cursor = new float2(world.x, world.z);
            if (_mode == ModeErase)
            {
                TickErase(center, config, cursor);
                return;
            }

            TickPaint(center, config, cursor);
        }

        void TickPaint(float3 center, RadialGridConfig config, float2 cursor)
        {
            if (Mouse.current != null
                && Mouse.current.leftButton.wasPressedThisFrame
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                _dragging = true;
                _start = cursor;
            }

            if (_dragging)
                RebuildPathGhost(center, config, cursor);
            else
                RebuildHoverGhost(center, config, cursor);

            if (_dragging
                && Mouse.current != null
                && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                var drag = math.lengthsq(cursor - _start);
                if (_validCount >= 2 && drag > 0.04f)
                    TryCommitPath(config);
                else if (_hoverValid)
                    TryCommitSection(config, _hoverA, _hoverB);
                _dragging = false;
                _validCount = 0;
            }
        }

        void TickErase(float3 center, RadialGridConfig config, float2 cursor)
        {
            RebuildEraseGhost(center, config, cursor);
            var pressed = Mouse.current != null
                && Mouse.current.leftButton.isPressed
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
            if (!pressed)
            {
                _eraseLastId = 0;
                return;
            }

            if (_eraseHoverId <= 0 || _eraseHoverId == _eraseLastId)
                return;
            if (!SimCommands.RequestDemolishRoad(_eraseHoverId))
                GameLog.Error("Demolish road: sim world not ready.");
            _eraseLastId = _eraseHoverId;
        }

        void RebuildHoverGhost(float3 center, RadialGridConfig config, float2 cursor)
        {
            CopyWorld(out var occupied, out var roads);
            _hoverValid = false;
            if (!RoadMath.TryPickPaintSection(center, config, cursor, out _hoverA, out _hoverB))
            {
                occupied.Dispose();
                roads.Dispose();
                SetGhosts(0);
                return;
            }

            var blocked = RoadMath.EdgeBlocked(config, occupied, _hoverA, _hoverB)
                          || RoadMath.Exists(roads, _hoverA, _hoverB)
                          || !CanAfford(1);
            _hoverValid = !blocked;
            occupied.Dispose();
            roads.Dispose();
            PoseGhosts(center, config, _hoverA, _hoverB, _hoverValid ? validColor : invalidColor);
        }

        void RebuildPathGhost(float3 center, RadialGridConfig config, float2 cursor)
        {
            CopyWorld(out var occupied, out var roads);
            _validCount = RoadPathfinder.Build(
                center, config, occupied, roads, _start, cursor, _path);
            occupied.Dispose();
            roads.Dispose();

            var fine = new NativeArray<RoadNode>(_path.Count, Allocator.Temp);
            for (var i = 0; i < _path.Count; i++)
                fine[i] = _path[i];
            var corners = new NativeList<RoadNode>(16, Allocator.Temp);
            var validCorners = new NativeList<RoadNode>(16, Allocator.Temp);
            RoadMath.CollapseToSections(fine, _path.Count, corners, config);
            RoadMath.CollapseToSections(fine, _validCount, validCorners, config);
            fine.Dispose();

            var edges = math.max(0, corners.Length - 1);
            var validEdges = math.max(0, validCorners.Length - 1);
            validCorners.Dispose();
            if (edges == 0 && _hoverValid)
            {
                corners.Dispose();
                PoseGhosts(center, config, _hoverA, _hoverB, validColor);
                return;
            }

            var unaffordable = !CanAfford(math.max(1, validEdges));
            EnsureGhosts(edges);
            var width = RoadMath.RoadWidth(config);
            var y = center.y + yOffset;
            for (var i = 0; i < edges; i++)
            {
                var a = RoadMath.NodeWorld(center, config, corners[i]);
                var b = RoadMath.NodeWorld(center, config, corners[i + 1]);
                RoadSectionPose.Apply(_ghosts[i], y, a, b, width);
                var valid = i < validEdges && !unaffordable;
                if (i < _ghostMats.Count && _ghostMats[i] != null)
                    _ghostMats[i].SetColor(ColorId, valid ? validColor : invalidColor);
            }

            corners.Dispose();
        }

        void RebuildEraseGhost(float3 center, RadialGridConfig config, float2 cursor)
        {
            _eraseHoverId = 0;
            CopyWorld(out var occupied, out var roads);
            occupied.Dispose();
            var index = RoadMath.NearestSegmentIndex(center, config, roads, cursor);
            if (index < 0)
            {
                roads.Dispose();
                SetGhosts(0);
                return;
            }

            var segment = roads[index];
            roads.Dispose();
            _eraseHoverId = segment.Protected != 0 ? 0 : segment.Id;
            var a = new RoadNode { Ring = segment.RingA, Fine = segment.FineA };
            var b = new RoadNode { Ring = segment.RingB, Fine = segment.FineB };
            PoseGhosts(center, config, a, b, segment.Protected != 0 ? lockedColor : eraseColor);
        }

        void PoseGhosts(
            float3 center, RadialGridConfig config, in RoadNode a, in RoadNode b, Color color)
        {
            EnsureGhosts(1);
            var width = RoadMath.RoadWidth(config);
            var y = center.y + yOffset;
            RoadSectionPose.Apply(
                _ghosts[0], y,
                RoadMath.NodeWorld(center, config, a),
                RoadMath.NodeWorld(center, config, b),
                width);
            if (_ghostMats.Count > 0 && _ghostMats[0] != null)
                _ghostMats[0].SetColor(ColorId, color);
        }

        void TryCommitPath(RadialGridConfig config)
        {
            if (_validCount < 2)
                return;
            var nodes = new NativeArray<RoadNode>(_validCount, Allocator.Temp);
            for (var i = 0; i < _validCount; i++)
                nodes[i] = _path[i];
            TryCommitNodes(config, nodes, _validCount);
            nodes.Dispose();
        }

        void TryCommitSection(RadialGridConfig config, in RoadNode a, in RoadNode b)
        {
            var nodes = new NativeArray<RoadNode>(2, Allocator.Temp);
            nodes[0] = a;
            nodes[1] = b;
            TryCommitNodes(config, nodes, 2);
            nodes.Dispose();
        }

        void TryCommitNodes(RadialGridConfig config, NativeArray<RoadNode> nodes, int count)
        {
            var sections = RoadMath.BillableSections(nodes, count, config);
            if (sections <= 0 || !CanAfford(sections))
                return;
            if (!SimCommands.RequestRoadStroke(nodes, count))
                GameLog.Error("Road stroke: sim world not ready.");
            if (!CanAfford(1))
            {
                CancelPainting();
                Finished?.Invoke();
            }
        }

        static void CopyWorld(out NativeArray<OccupiedCell> occupied, out NativeArray<RoadSegment> roads)
        {
            occupied = default;
            roads = default;
            if (SimWorld.TryGet(out var em, out var bag))
            {
                if (em.HasBuffer<OccupiedCell>(bag))
                {
                    var cells = em.GetBuffer<OccupiedCell>(bag);
                    occupied = new NativeArray<OccupiedCell>(cells.Length, Allocator.Temp);
                    for (var i = 0; i < cells.Length; i++)
                        occupied[i] = cells[i];
                }

                if (em.HasBuffer<RoadSegment>(bag))
                {
                    var buffer = em.GetBuffer<RoadSegment>(bag);
                    roads = new NativeArray<RoadSegment>(buffer.Length, Allocator.Temp);
                    for (var i = 0; i < buffer.Length; i++)
                        roads[i] = buffer[i];
                }
            }

            if (!occupied.IsCreated)
                occupied = new NativeArray<OccupiedCell>(0, Allocator.Temp);
            if (!roads.IsCreated)
                roads = new NativeArray<RoadSegment>(0, Allocator.Temp);
        }

        void EnsureRoot()
        {
            if (_ghostRoot != null)
                return;
            var go = new GameObject("RoadGhosts");
            go.transform.SetParent(transform, false);
            _ghostRoot = go.transform;
        }

        void EnsureGhosts(int count)
        {
            EnsureRoot();
            while (_ghosts.Count < count)
            {
                var instance = Instantiate(sectionPrefab, _ghostRoot);
                instance.name = "RoadGhost";
                var renderer = instance.GetComponentInChildren<MeshRenderer>();
                Material mat = null;
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    mat = renderer.material;
                    renderer.sharedMaterial = mat;
                }
                _ghosts.Add(instance.transform);
                _ghostMats.Add(mat);
            }

            for (var i = 0; i < _ghosts.Count; i++)
            {
                if (_ghosts[i] != null)
                    _ghosts[i].gameObject.SetActive(i < count);
            }
        }

        void SetGhosts(int count)
        {
            EnsureGhosts(count);
            for (var i = 0; i < _ghosts.Count; i++)
            {
                if (_ghosts[i] != null)
                    _ghosts[i].gameObject.SetActive(i < count);
            }
        }

        static bool TryPointer(float groundY, out Vector3 world)
        {
            world = default;
            var cam = Camera.main;
            if (cam == null || Mouse.current == null)
                return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;
            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            if (!plane.Raycast(ray, out var enter))
                return false;
            world = ray.GetPoint(enter);
            return true;
        }

        static bool TryGetCity(out float3 center, out RadialGridConfig config)
        {
            center = default;
            config = default;
            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasComponent<CityGrid>(bag))
                return false;
            var grid = em.GetComponentData<CityGrid>(bag);
            if (grid.Ready == 0 || !grid.Config.IsValid)
                return false;
            center = grid.Center;
            config = grid.Config;
            return true;
        }

        static bool CanAfford(int sections)
        {
            if (!SimWorld.TryGet(out var em, out var bag)
                || !em.HasBuffer<BuildingCatalogCost>(bag)
                || !em.HasBuffer<ResourceAmount>(bag))
                return true;
            return BuildingCosts.CanAffordScaled(
                em.GetBuffer<BuildingCatalogCost>(bag),
                RoadType,
                em.GetBuffer<ResourceAmount>(bag),
                sections);
        }

        void OnDisable()
        {
            if (_mode != ModeOff)
                CancelPainting();
        }

        void OnDestroy()
        {
            for (var i = 0; i < _ghostMats.Count; i++)
            {
                if (_ghostMats[i] != null)
                    Destroy(_ghostMats[i]);
            }
            _ghostMats.Clear();
        }
    }
}
