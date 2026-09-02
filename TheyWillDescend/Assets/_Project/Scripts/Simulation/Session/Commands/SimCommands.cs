using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Presentation-facing command posting boundary. Gameplay posts commands and lets
    /// <see cref="CommandSystemGroup"/> consume them on the next simulation tick.
    /// </summary>
    public static class SimCommands
    {
        public static bool TryPost<T>(T command)
            where T : unmanaged, IBufferElementData
        {
            if (!SimWorld.TryGet(out var em, out var bag))
                return false;
            if (!em.HasBuffer<T>(bag))
                return false;
            em.GetBuffer<T>(bag).Add(command);
            return true;
        }
    }
}
