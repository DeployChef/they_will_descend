using UnityEditor;
using UnityEngine;

namespace TheyWillDescend.Authoring.Editor
{
    /// <summary>
    /// Supplies the buffer required by Point Grass shaders while their runtime
    /// displacement manager is not running in Edit Mode.
    /// </summary>
    [InitializeOnLoad]
    internal static class PointGrassEditorPreview
    {
        private static readonly int DisplacementObjects = Shader.PropertyToID("_DisplacementObjects");
        private static readonly int DisplacementCount = Shader.PropertyToID("_DisplacementCount");

        private static ComputeBuffer _emptyBuffer;

        static PointGrassEditorPreview()
        {
            EditorApplication.delayCall += BindPreviewBuffer;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseBuffer;
            EditorApplication.quitting += ReleaseBuffer;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
                BindPreviewBuffer();
        }

        private static void BindPreviewBuffer()
        {
            if (_emptyBuffer == null || !_emptyBuffer.IsValid())
            {
                // PG_DisplacementInclude.hlsl: float3 position, float radius, float strength.
                _emptyBuffer = new ComputeBuffer(1, 5 * sizeof(float));
                _emptyBuffer.SetData(new float[5]);
            }

            // D3D12 requires a valid buffer even when the displacement loop has
            // zero iterations. Runtime per-renderer property blocks override this fallback.
            Shader.SetGlobalBuffer(DisplacementObjects, _emptyBuffer);
            Shader.SetGlobalInt(DisplacementCount, 0);

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
        }

        private static void ReleaseBuffer()
        {
            if (_emptyBuffer == null)
                return;

            Shader.SetGlobalBuffer(DisplacementObjects, (ComputeBuffer)null);
            Shader.SetGlobalInt(DisplacementCount, 0);
            _emptyBuffer.Release();
            _emptyBuffer = null;
        }
    }
}
