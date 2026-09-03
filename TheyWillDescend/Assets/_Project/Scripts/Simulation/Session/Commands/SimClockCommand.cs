using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    public enum SimClockCommandKind : byte
    {
        SetSessionInGame = 1,
        TogglePlayerPause = 2,
        SetSpeed = 3,
        SetBuildLocked = 4,
        Restore = 5,
        SetPlayerPause = 6,
        ToggleTimePause = 7,
        SetTimePause = 8
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
            Kind = SimClockCommandKind.ToggleTimePause
        };

        public static SimClockCommand TimePaused(bool paused) => new()
        {
            Kind = SimClockCommandKind.SetTimePause,
            Value = paused ? 1 : 0
        };

        public static SimClockCommand PlayerPaused(bool paused) => new()
        {
            Kind = SimClockCommandKind.SetPlayerPause,
            Value = paused ? 1 : 0
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

        public static SimClockCommand Restore(int speed, bool timePaused) => new()
        {
            Kind = SimClockCommandKind.Restore,
            Value = speed,
            Secondary = timePaused ? 1 : 0
        };
    }
}
