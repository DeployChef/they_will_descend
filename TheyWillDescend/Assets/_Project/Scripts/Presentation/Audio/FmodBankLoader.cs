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

        /// <summary>Банки, найденные для ивента (для hot reload — что выгружать).</summary>
        static readonly System.Collections.Generic.List<string> _eventBanks = new();

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

        /// <summary>
        /// Выгружает перечисленные банки и сбрасывает кеш (для hot reload).
        /// </summary>
        public static void UnloadBanks(params string[] bankNames)
        {
            if (bankNames == null)
                return;

            for (var i = 0; i < bankNames.Length; i++)
            {
                var bankName = bankNames[i];
                if (string.IsNullOrEmpty(bankName))
                    continue;

                RuntimeManager.UnloadBank(bankName);
                _loaded.Remove(bankName);
                GameLog.Info($"FmodBankLoader: bank '{bankName}' unloaded.");
            }
        }

        /// <summary>
        /// Находит и загружает банки, в которых лежит ивент (принцип
        /// StudioEventEmitter). ВАЖНО: у EventDescription в FMOD 2.03 НЕТ
        /// getBankList — ищем перебором всех банков системы (Bank.getEventList).
        /// Работает только для уже загруженных банков (RuntimeManager сам
        /// грузит всё из StreamingAssets при ImportType=StreamingAssets).
        /// </summary>
        public static void LoadBanksForEvent(EventReference eventReference)
        {
            var names = GetBanksForEvent(eventReference);

            _eventBanks.Clear();
            _eventBanks.AddRange(names);

            for (var i = 0; i < names.Length; i++)
                LoadBank(names[i]);
        }

        /// <summary>
        /// Возвращает имена банков (без .bank), содержащих ивент.
        /// </summary>
        public static string[] GetBanksForEvent(EventReference eventReference)
        {
            if (eventReference.IsNull)
                return System.Array.Empty<string>();

            var description = RuntimeManager.GetEventDescription(eventReference);
            if (!description.isValid())
            {
                GameLog.Warning("FmodBankLoader: event description not found for EventReference.");
                return System.Array.Empty<string>();
            }

            if (description.getPath(out var eventPath) != FMOD.RESULT.OK || string.IsNullOrEmpty(eventPath))
            {
                GameLog.Warning("FmodBankLoader: failed to resolve event path.");
                return System.Array.Empty<string>();
            }

            // У EventDescription нет getBankList (ошибка API, см. AGENTS.md) —
            // перебираем все загруженные банки и ищем ивент внутри каждого.
            var result = RuntimeManager.StudioSystem.getBankList(out var banks);
            if (result != FMOD.RESULT.OK || banks == null)
            {
                GameLog.Warning($"FmodBankLoader: system getBankList failed: {result}.");
                return System.Array.Empty<string>();
            }

            var names = new System.Collections.Generic.List<string>();
            for (var i = 0; i < banks.Length; i++)
            {
                if (!banks[i].isValid())
                    continue;

                if (banks[i].getEventList(out var events) != FMOD.RESULT.OK || events == null)
                    continue;

                for (var j = 0; j < events.Length; j++)
                {
                    if (!events[j].isValid())
                        continue;
                    if (events[j].getPath(out var p) == FMOD.RESULT.OK && p == eventPath)
                    {
                        // У Bank нет getName — только getPath ("bank:/ИмяБанка").
                        if (banks[i].getPath(out var bankPath) == FMOD.RESULT.OK && !string.IsNullOrEmpty(bankPath))
                        {
                            var bankName = System.IO.Path.GetFileNameWithoutExtension(bankPath.Replace("bank:/", ""));
                            if (!string.IsNullOrEmpty(bankName) && !names.Contains(bankName))
                                names.Add(bankName);
                        }
                        break;
                    }
                }
            }

            return names.ToArray();
        }

        /// <summary>
        /// Выгружает банки, найденные ранее для ивента (LoadBanksForEvent),
        /// для hot reload. Master/Master.strings не трогаются.
        /// </summary>
        public static void UnloadEventBanks()
        {
            UnloadBanks(_eventBanks.ToArray());
            _eventBanks.Clear();
        }
    }
}
