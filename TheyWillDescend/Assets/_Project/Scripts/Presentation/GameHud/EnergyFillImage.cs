using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Plain energy indicator: mirrors the orb's smoothed normalized energy into
    /// Image.fillAmount, so 0% energy fills nothing and 100% fills everything.
    ///
    /// Deliberately does NOT read the simulation itself. <see cref="OrbEnergyGlow"/> already
    /// owns the whole chain — ECS read, working range (Range Min/Max) and SmoothDamp — and one
    /// shared value keeps every indicator in lockstep. Do not wire this Image into
    /// ResourceWidget.energyFill: that path divides by the raw stock cap (2000), which pins
    /// the fill near empty, the very problem the working range was added to fix.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    // Run LateUpdate after OrbEnergyGlow (default order 0), otherwise the fill trails
    // the orb by exactly one frame.
    [DefaultExecutionOrder(100)]
    public sealed class EnergyFillImage : MonoBehaviour
    {
        [Tooltip("Indicator that owns the smoothed energy value. Resolved automatically when left empty.")]
        [SerializeField] OrbEnergyGlow source;

        Image _image;
        OrbEnergyGlow _resolved;

        void OnEnable()
        {
            _image = GetComponent<Image>();
            _resolved = Resolve();
        }

        void LateUpdate()
        {
            if (_image == null)
                _image = GetComponent<Image>();

            // Cheap retry only while unresolved: the orb is a scene object, so once found
            // this path never runs again.
            if (_resolved == null)
            {
                _resolved = Resolve();
                if (_resolved == null)
                    return;
            }

            _image.fillAmount = _resolved.CurrentEnergy01;
        }

        OrbEnergyGlow Resolve()
        {
            if (source != null)
                return source;

            // A sibling under the same HUD panel is the usual layout; the scene search is
            // the last resort so forgetting the reference is not a silent failure.
            return GetComponentInParent<OrbEnergyGlow>(true)
                ?? FindAnyObjectByType<OrbEnergyGlow>();
        }
    }
}
