using UnityEngine;

namespace _Project.Scripts.Infrastructure.Logging
{
    /// <summary>
    /// Thin logging facade for the project.
    /// Simulation may call this from managed (non-Burst) systems.
    /// Do not call from Burst jobs — emit a domain event and log in a managed system instead.
    /// </summary>
    public static class GameLog
    {
        public enum Level : byte
        {
            Verbose = 0,
            Info = 1,
            Warning = 2,
            Error = 3
        }

        /// <summary>When false, Verbose is discarded.</summary>
        public static bool EnableVerbose =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        /// <summary>Minimum level that reaches the sink. Info by default in player builds.</summary>
        public static Level MinimumLevel =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Level.Verbose;
#else
            Level.Info;
#endif

        public static void Verbose(string channel, string message) =>
            Write(Level.Verbose, channel, message);

        public static void Info(string channel, string message) =>
            Write(Level.Info, channel, message);

        public static void Warning(string channel, string message) =>
            Write(Level.Warning, channel, message);

        public static void Error(string channel, string message) =>
            Write(Level.Error, channel, message);

        public static void Write(Level level, string channel, string message)
        {
            if (level == Level.Verbose && !EnableVerbose)
                return;
            if (level < MinimumLevel)
                return;

            var line = $"[{channel}] {message}";

            switch (level)
            {
                case Level.Warning:
                    Debug.LogWarning(line);
                    break;
                case Level.Error:
                    Debug.LogError(line);
                    break;
                default:
                    Debug.Log(line);
                    break;
            }
        }
    }
}
