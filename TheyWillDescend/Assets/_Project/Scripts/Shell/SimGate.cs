using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.Io;
using TheyWillDescend.Simulation.Session;
using UnityEngine;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Shell clock policy. ECS mirrors EffectiveMode and Speed only.
    /// Two independent bools: player pause and build freeze can both be true.
    /// </summary>
    public sealed class SimGate
    {
        public static SimGate Active { get; private set; }

        bool _sessionInGame;

        public int Speed { get; private set; } = 1;

        public bool PlayerPaused { get; private set; }

        public bool BuildLocked { get; private set; }

        public SimRunMode EffectiveMode
        {
            get
            {
                if (!_sessionInGame)
                    return SimRunMode.Off;
                if (PlayerPaused || BuildLocked)
                    return SimRunMode.Frozen;
                return SimRunMode.Running;
            }
        }

        public SimRunMode Current => EffectiveMode;

        public bool SessionInGame => _sessionInGame;

        public void BindAsActive()
        {
            Active = this;
        }

        public static void ClearActive()
        {
            Active = null;
        }

        public void SetSessionInGame(bool inGame)
        {
            _sessionInGame = inGame;
            PlayerPaused = false;
            BuildLocked = false;
            GameLog.Info(inGame ? $"SimGate session InGame x{Speed}." : "SimGate session Off.");
        }

        public void SetSpeed(int speed)
        {
            if (!_sessionInGame || BuildLocked)
                return;

            if (speed < 1)
                speed = 1;
            if (speed > 3)
                speed = 3;

            Speed = speed;
            PlayerPaused = false;
            GameLog.Info($"SimGate speed x{Speed} → {EffectiveMode}.");
        }

        public void TogglePlayerPause()
        {
            if (!_sessionInGame || BuildLocked)
                return;

            PlayerPaused = !PlayerPaused;
            GameLog.Info($"SimGate player pause → {EffectiveMode} (x{Speed}).");
        }

        public void SetBuildLocked(bool locked)
        {
            if (locked && !_sessionInGame)
                return;

            BuildLocked = locked;
            GameLog.Info($"SimGate build locked={locked} → {EffectiveMode} (x{Speed}).");
        }

        public void RestoreFromSnapshot(int speed, bool playerPaused)
        {
            if (speed < 1)
                speed = 1;
            if (speed > 3)
                speed = 3;

            Speed = speed;
            PlayerPaused = playerPaused;
            GameLog.Info($"SimGate restore x{Speed} paused={playerPaused} → {EffectiveMode}.");
        }

        public void PushClock(float unscaledDeltaTime)
        {
            SimIo.SetClock(EffectiveMode, Speed, unscaledDeltaTime);
        }
    }
}
