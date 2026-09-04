using FMODUnity;
using TheyWillDescend.Infrastructure.Logging;
using UnityEngine;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Загрузчик FMOD-банков для аудио-зон.
    /// Загружает Master + банки с ивентом ambience_town перед созданием инстансов.
    /// Вызывается из AudioZoneManager один раз при старте.
    /// </summary>
    public sealed class FmodBankLoader
    {
        /// <summary>Загруженные банки (защита от повторной загрузки).</summary>
        static readonly System.Collections.Generic.HashSet<string> _loaded = new();

        /// <summary>
        /// Загружает Master-банк и перечисленные банки. Безопасно вызывать повторно.
        /// </summary>
        public static void LoadBanks(params string[] bankNames)
        {
            LoadBank("Master");

            if (bankNames == null)
                return;

            for (var i = 0; i < bankNames.Length; i++)
                LoadBank(bankNames[i]);
        }

        /// <summary>
        /// Загружает один банк, если он ещё не загружен.
        /// </summary>
        public static void LoadBank(string bankName)
        {
            if (string.IsNullOrEmpty(bankName) || _loaded.Contains(bankName))
                return;

            try
            {
                if (RuntimeManager.HasBankLoaded(bankName))
                {
                    _loaded.Add(bankName);
                    return;
                }

                RuntimeManager.LoadBank(bankName, loadSamples: true);
                _loaded.Add(bankName);
                GameLog.Info($"FmodBankLoader: bank '{bankName}' loaded.");
            }
            catch (BankLoadException e)
            {
                GameLog.Warning($"FmodBankLoader: bank '{bankName}' missing or failed to load. {e.Message}");
            }
        }

        /// <summary>
        /// Проверяет, что все перечисленные банки загружены.
        /// </summary>
        public static bool AreBanksLoaded(params string[] bankNames)
        {
            if (bankNames == null)
                return true;

            for (var i = 0; i < bankNames.Length; i++)
            {
                if (!RuntimeManager.HasBankLoaded(bankNames[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Сброс кеша (для домен-релода в редакторе).
        /// </summary>
        public static void ResetCache() => _loaded.Clear();
    }
}
