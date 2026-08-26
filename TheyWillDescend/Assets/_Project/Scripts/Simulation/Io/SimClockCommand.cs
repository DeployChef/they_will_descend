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

        public static SimClockCommand InGame(bool inGame) => new()
        {
            Kind = SimClockCommandKind.SetSessionInGame,
            Value = inGame ? 1 : 0
        };

        public static SimClockCommand TogglePause() => new()
        {
            Kind = SimClockCommandKind.TogglePlayerPause
        };

        public static SimClockCommand Speed(int speed) => new()
        {
            Kind = SimClockCommandKind.SetSpeed,
            Value = speed
        };

        public static SimClockCommand BuildLocked(bool locked) => new()
        {
            Kind = SimClockCommandKind.SetBuildLocked,
            Value = locked ? 1 : 0
        };

        public static SimClockCommand Restore(int speed, bool playerPaused) => new()
        {
            Kind = SimClockCommandKind.Restore,
            Value = speed,
            Secondary = playerPaused ? 1 : 0
        };
    }
}
