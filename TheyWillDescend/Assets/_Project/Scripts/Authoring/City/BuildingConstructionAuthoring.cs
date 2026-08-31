using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    [DisallowMultipleComponent]
    public sealed class BuildingConstructionAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds to raise this house. 0 = appears finished.")]
        float duration = 8f;

        public float Duration => duration < 0f ? 0f : duration;
    }
}
