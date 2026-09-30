using VContainer.Unity;

namespace TheyWillDescend.Main
{
    /// <summary>
    /// Child of <see cref="RootLifetimeScope"/>. Awake points the parent at Root
    /// so a scene loaded later does not need a cross-scene inspector reference.
    /// </summary>
    public abstract class SceneLifetimeScope : LifetimeScope
    {
        protected override void Awake()
        {
            parentReference = ParentReference.Create<RootLifetimeScope>();
            base.Awake();
        }
    }
}
