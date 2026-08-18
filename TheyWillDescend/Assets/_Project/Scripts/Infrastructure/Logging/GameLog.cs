using UnityEngine;

namespace _Project.Scripts.Infrastructure.Logging
{
    /// <summary>
    /// Thin logging facade. Managed only — not Burst.
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

        public static bool EnableVerbose =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        public static Level MinimumLevel =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Level.Verbose;
#else
            Level.Info;
#endif

        public static void Verbose(string message) => Write(Level.Verbose, message);

        public static void Info(string message) => Write(Level.Info, message);

        public static void Warning(string message) => Write(Level.Warning, message);

        public static void Error(string message) => Write(Level.Error, message);

        public static void Write(Level level, string message)
        {
            if (level == Level.Verbose && !EnableVerbose)
                return;
            if (level < MinimumLevel)
                return;

            switch (level)
            {
                case Level.Warning:
                    Debug.LogWarning(message);
                    break;
                case Level.Error:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }
    }
}
