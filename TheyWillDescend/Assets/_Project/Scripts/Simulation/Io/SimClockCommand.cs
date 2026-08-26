using Unity.Entities;

namespace TheyWillDescend.Simulation.Io
{
    public enum SimClockCommandKind : byte
    {
        SetSessionInGame = 1,
        TogglePlayerPause = 2,
        SetSpeed = 3,
        SetBuildLocked = 4,
        Restore = 5
    }

    public struct SimClockCommand : IBufferElementData
    {
        public SimClockCommandKind Kind;
        public int Value;
        public int Secondary;
    }
}
