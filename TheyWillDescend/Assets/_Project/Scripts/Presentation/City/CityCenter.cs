using UnityEngine;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// World anchor for the polar city. Temporary stand-in for the pyramid —
    /// currently the center house at the plaza.
    /// </summary>
    public sealed class CityCenter : MonoBehaviour
    {
        public static CityCenter Active { get; private set; }

        public Vector3 Position => transform.position;

        void OnEnable() => Active = this;

        void OnDisable()
        {
            if (Active == this)
                Active = null;
        }
    }
}
