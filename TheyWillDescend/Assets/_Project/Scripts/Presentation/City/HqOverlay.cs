using UnityEngine;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Plaza ring + click volume for HQ. Ring mesh is assigned at runtime.
    /// </summary>
    public sealed class HqOverlay : MonoBehaviour
    {
        [SerializeField] BuildingIdTag idTag;
        [SerializeField] MeshFilter plazaFilter;
        [SerializeField] MeshRenderer plazaRenderer;
        [SerializeField] CapsuleCollider clickProxy;

        public BuildingIdTag IdTag => idTag;

        public MeshFilter PlazaFilter => plazaFilter;

        public MeshRenderer PlazaRenderer => plazaRenderer;

        public CapsuleCollider ClickProxy => clickProxy;
    }
}
