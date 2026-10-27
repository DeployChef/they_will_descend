using TheyWillDescend.Content;
using UnityEngine;

namespace TheyWillDescend.Presentation.Environment
{
    /// <summary>
    /// Root of a game scene's dressing. Brings the scene's <see cref="VisualSet"/>
    /// so the shared run holds no art references of its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneDressing : MonoBehaviour
    {
        [SerializeField] VisualSet visuals;

        public VisualSet Visuals => visuals;
    }
}
