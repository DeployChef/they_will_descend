using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    [DisallowMultipleComponent]
    public sealed class BuildingWorkplaceAuthoring : MonoBehaviour
    {
        [SerializeField] [Min(0)] int slots = 10;

        public int Slots => slots < 0 ? 0 : slots;
    }
}
