using System;
using System.Collections.Generic;
using TheyWillDescend.Presentation.Cameras;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheyWillDescend.Presentation.Environment
{
    /// <summary>Uses lighter crowns and suppresses selected trunk shadows while the pause camera is dominant.</summary>
    [DisallowMultipleComponent]
    public sealed class PauseTreeCrownLod : MonoBehaviour
    {
        [SerializeField] PauseCameraSwitch menuCameraSwitch;
        [SerializeField, Range(.05f, .95f)] float menuLodBlendThreshold = .5f;
        [SerializeField] CrownMeshes[] crownMeshes = Array.Empty<CrownMeshes>();
        [SerializeField] MeshRenderer[] menuTrunkRenderers = Array.Empty<MeshRenderer>();

        readonly List<(MeshFilter filter, CrownMeshes meshes)> _crowns = new();
        readonly Dictionary<MeshRenderer, ShadowCastingMode> _trunkShadowModes = new();
        bool _menuMeshesApplied;

        [Serializable]
        struct CrownMeshes
        {
            public Mesh gameplay;
            public Mesh menu;
        }

        void OnEnable() => Refresh();

        void LateUpdate()
        {
            bool useMenu = menuCameraSwitch && menuCameraSwitch.MenuBlendProgress >= menuLodBlendThreshold;
            if (useMenu != _menuMeshesApplied)
                Apply(useMenu);
        }

        void OnDisable()
        {
            if (_menuMeshesApplied)
                Apply(false);
            _crowns.Clear();
        }

        /// <summary>Rebuilds the crown list after changing the forest hierarchy at runtime.</summary>
        public void Refresh()
        {
            if (_menuMeshesApplied)
                Apply(false);
            _crowns.Clear();

            var byMesh = new Dictionary<Mesh, CrownMeshes>();
            foreach (var meshes in crownMeshes)
                if (meshes.gameplay && meshes.menu)
                    byMesh.TryAdd(meshes.gameplay, meshes);
            if (byMesh.Count == 0) return;

            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh && byMesh.TryGetValue(filter.sharedMesh, out var meshes))
                    _crowns.Add((filter, meshes));
        }

        void Apply(bool useMenu)
        {
            foreach (var (filter, meshes) in _crowns)
            {
                var expected = useMenu ? meshes.gameplay : meshes.menu;
                if (filter && filter.sharedMesh == expected)
                    filter.sharedMesh = useMenu ? meshes.menu : meshes.gameplay;
            }

            if (useMenu)
                DisableTrunkShadows();
            else
                RestoreTrunkShadows();

            _menuMeshesApplied = useMenu;
        }

        void DisableTrunkShadows()
        {
            foreach (var renderer in menuTrunkRenderers)
            {
                if (!renderer || renderer.shadowCastingMode == ShadowCastingMode.Off)
                    continue;

                _trunkShadowModes.Add(renderer, renderer.shadowCastingMode);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        void RestoreTrunkShadows()
        {
            foreach (var (renderer, mode) in _trunkShadowModes)
                if (renderer && renderer.shadowCastingMode == ShadowCastingMode.Off)
                    renderer.shadowCastingMode = mode;

            _trunkShadowModes.Clear();
        }
    }
}
