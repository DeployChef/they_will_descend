using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Posts a command onto the singleton entity's buffer. Systems consume it.
    /// </summary>
    public static class SimCommands
    {
        public static bool TryPost<T>(T command)
            where T : unmanaged, IBufferElementData
        {
            if (!SimWorld.TryGet(out var em, out var bag))
                return false;
            if (!em.HasBuffer<T>(bag))
                em.AddBuffer<T>(bag);
            em.GetBuffer<T>(bag).Add(command);
            return true;
        }

        public static bool TryRequestDespawnAllAgents()
        {
            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasComponent<SimBridge>(bag))
                return false;
            var data = em.GetComponentData<SimBridge>(bag);
            data.DespawnAllAgents = 1;
            em.SetComponentData(bag, data);
            return true;
        }

        public static bool TryRequestDespawnAllBuildings()
        {
            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasComponent<SimBridge>(bag))
                return false;
            var data = em.GetComponentData<SimBridge>(bag);
            data.DespawnAllBuildings = 1;
            em.SetComponentData(bag, data);
            return true;
        }

        public static void Playback()
        {
            if (!SimWorld.TryGet(out var em, out _))
                return;
            SimCommandPlayback.Run(em);
        }
    }
}
