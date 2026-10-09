using System.Collections.Generic;
using UnityEngine;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.Audio;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Реестр аудио-ячеек. Ячейка — динамический кластер построек: первая постройка
    /// рождает ячейку, следующие притягиваются к ближайшей неполной ячейке в радиусе
    /// привязки (по центроиду или по любой её постройке), на лимите открывается
    /// новая. Пустая ячейка перестаёт существовать вместе с инстансом.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioZoneManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] AudioZoneSettings settings;
        [SerializeField] AudioVisibilityChecker visibilityChecker;

        [Header("FMOD Banks")]
        [Tooltip("Банки с ивентом (без расширения .bank). Используется, если EventReference в настройках не задан. Master грузится всегда.")]
        [SerializeField] string[] fmodBanks = { "Ambience_Town" };

        /// <summary>Живые ячейки. Порядок не фиксирован — идентификатор у каждой свой.</summary>
        private readonly List<AudioCell> _cells = new();

        /// <summary>Следующий сквозной id ячейки (для логов и подписей).</summary>
        private int _nextCellId;

        /// <summary>Счётчик кадров между тиками видимости.</summary>
        private int _frameCounter;

        /// <summary>Банки загружены (один раз; hot reload пересоздаёт ячейки сам).</summary>
        private bool _banksLoaded;

        public AudioZoneSettings Settings => settings;
        public AudioVisibilityChecker VisibilityChecker => visibilityChecker;

        /// <summary>Живые ячейки (только для чтения).</summary>
        public IReadOnlyList<AudioCell> Cells => _cells;

        void Awake()
        {
            if (visibilityChecker == null)
                visibilityChecker = GetComponentInChildren<AudioVisibilityChecker>();

            if (settings == null)
            {
                GameLog.Error("AudioZoneManager: AudioZoneSettings is not assigned.");
                enabled = false;
                return;
            }
        }

        void Update()
        {
            if (!enabled || settings == null)
                return;

            EnsureBanksLoaded();

            // Тик видимости каждые 2 кадра.
            _frameCounter++;
            if (_frameCounter < 2)
                return;

            _frameCounter = 0;
            PruneEmptyCells();
            UpdateVisibilityBatch();
        }

        /// <summary>
        /// Грузит банки до первого создания инстансов. Если задан EventReference —
        /// банки определяются автоматически из ивента (принцип StudioEventEmitter).
        /// </summary>
        private void EnsureBanksLoaded()
        {
            if (_banksLoaded)
                return;

            _banksLoaded = true;

            if (!settings.EventReference.IsNull)
                FmodBankLoader.LoadBanksForEvent(settings.EventReference);
            else
                FmodBankLoader.LoadBanks(fmodBanks);
        }

        // ===== Привязка построек =====

        /// <summary>
        /// Привязать постройку: к ближайшей неполной ячейке в радиусе привязки,
        /// иначе — новая ячейка с этой постройкой. Возвращает ячейку привязки.
        /// </summary>
        public AudioCell AttachSource(BuildingAudioSource source)
        {
            if (settings == null || source == null)
                return null;

            EnsureBanksLoaded();

            var pos = source.transform.position;
            var radius = settings.AttachRadius;

            AudioCell best = null;
            var bestDist = float.MaxValue;

            for (var i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                if (cell == null || cell.IsDisposed || cell.IsFull)
                    continue;

                var d = cell.DistanceTo(pos);
                if (d <= radius && d < bestDist)
                {
                    bestDist = d;
                    best = cell;
                }
            }

            var isNew = best == null;
            if (isNew)
            {
                best = new AudioCell(_nextCellId++, settings);
                _cells.Add(best);
            }

            source.Manager = this;
            source.LinkedCell = best;
            best.AddSource(source);

            // Мгновенная активация: ячейка уже в поле зрения — звук появляется
            // сразу после постройки, без ожидания тика видимости.
            if (best.IsVisible && !best.IsActive)
                best.SetActive(true);

            if (settings.LogZoneActivity)
            {
                GameLog.Info(isNew
                    ? $"AudioZoneManager: NEW cell {best.Id} at {pos} (no cell within {radius:F1} m)."
                    : $"AudioZoneManager: building -> cell {best.Id} ({best.Count}/{best.Capacity}), dist {bestDist:F1} m.");
            }

            return best;
        }

        /// <summary>
        /// Отвязать постройку. Опустевшая ячейка уничтожается — её инстанс
        /// освобождается, звук перестаёт существовать.
        /// </summary>
        public void DetachSource(BuildingAudioSource source)
        {
            var cell = source != null ? source.LinkedCell : null;
            if (cell == null)
                return;

            cell.RemoveSource(source);

            if (!cell.IsEmpty)
                return;

            source.LinkedCell = null;
            _cells.Remove(cell);
            cell.Dispose();

            if (settings != null && settings.LogZoneActivity)
                GameLog.Info($"AudioZoneManager: cell {cell.Id} emptied and destroyed ({_cells.Count} cells left).");
        }

        /// <summary>
        /// Страховка: удаляет ячейки, которые опустели в обход DetachSource
        /// (уничтоженный объект, смена сцены).
        /// </summary>
        private void PruneEmptyCells()
        {
            for (var i = _cells.Count - 1; i >= 0; i--)
            {
                var cell = _cells[i];
                if (cell == null)
                {
                    _cells.RemoveAt(i);
                    continue;
                }

                if (!cell.IsEmpty)
                    continue;

                cell.Dispose();
                _cells.RemoveAt(i);

                if (settings.LogZoneActivity)
                    GameLog.Info($"AudioZoneManager: pruned empty cell {cell.Id}.");
            }
        }


        /// <summary>
        /// Сбрасывает реестр ячеек: инстансы освобождаются, постройки остаются
        /// жить. Используется hot reload'ом банков.
        /// </summary>
        private void ClearCells()
        {
            for (var i = 0; i < _cells.Count; i++)
                _cells[i]?.Dispose();

            _cells.Clear();
            _nextCellId = 0;
        }

        /// <summary>
        /// Перелинковывает все активные BuildingAudioSource заново — после hot
        /// reload ячеек. Неактивные сами придут через OnEnable.
        /// </summary>
        private void RelinkAllSources()
        {
            var sources = FindObjectsByType<BuildingAudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                // Строгая проверка: исключаем уничтоженные объекты и префабы,
                // чтобы не считать их реальными постройками.
                if (source == null || !source.gameObject.activeInHierarchy)
                    continue;

                AttachSource(source);
            }

            if (settings != null && settings.LogZoneActivity)
                GameLog.Info($"AudioZoneManager: relinked {sources.Length} buildings into {_cells.Count} cells "
                    + $"(capacity {settings.CellCapacity}, attach radius {settings.AttachRadius:F1} m).");
        }


        /// <summary>
        /// Обновляет видимость ячеек + Distance RTPC (audio LOD) и 3D-позицию
        /// активных ячеек.
        /// </summary>
        private void UpdateVisibilityBatch()
        {
            if (_cells.Count == 0)
                return;

            if (visibilityChecker != null)
            {
                visibilityChecker.UpdateVisibility(_cells);
                visibilityChecker.ApplyVisibility();
            }

            // Audio LOD + 3D-позиция активных ячеек: каждый тик.
            // Дистанционная смерть: дальше Zone Death Distance инстанс умирает.
            var cam = visibilityChecker != null ? visibilityChecker.Camera : Camera.main;
            if (cam == null)
                return;

            var camPos = cam.transform.position;
            var rtpcName = settings.DistanceRtpcName;
            var rtpcRange = settings.DistanceRtpcRange;
            var deathDist = settings.ZoneDeathDistance;
            var deathHyst = settings.ZoneDeathHysteresis;

            for (var i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                if (cell == null)
                    continue;

                cell.UpdateDistanceDeath(camPos, deathDist, deathHyst);

                if (!cell.IsActive)
                    continue;

                cell.UpdateDistanceRtpc(camPos, rtpcName, rtpcRange);
                cell.Update3DAttributes();
            }
        }

        void OnDestroy() => ClearCells();

        /// <summary>
        /// Hot reload: выгружает ивентовые банки, грузит заново, пересобирает
        /// ячейки и перелинковывает постройки — без перезапуска сцены. Правой
        /// кнопкой по заголовку компонента в инспекторе во время Play.
        /// Master и Master.strings не трогаем — они нужны для резолва путей.
        /// </summary>
        [ContextMenu("Reload FMOD Banks")]
        private void ReloadFmodBanks()
        {
            GameLog.Info("AudioZoneManager: hot reload banks started.");

            // Случайные городские звуки лежат в том же банке — их инстансы
            // тоже сошлются на выгружаемые ивенты. Глушим их до выгрузки.
            FindFirstObjectByType<ZoneAmbienceSfxScheduler>()?.SilenceAll();

            // Глушим все ячейки до выгрузки банков (инстансы ссылаются на ивенты).
            ClearCells();

            // Выгружаем ивентовые банки (найденные ранее для EventReference)
            // и вручную указанные. Master/Master.strings не трогаем — они
            // нужны для резолва путей ивентов.
            FmodBankLoader.UnloadEventBanks();
            FmodBankLoader.UnloadBanks(fmodBanks);

            // Грузим заново: EventReference → автоопределение, иначе ручной список.
            _banksLoaded = false;
            EnsureBanksLoaded();

            // Пересобираем ячейки из стоящих построек.
            RelinkAllSources();

            GameLog.Info("AudioZoneManager: hot reload banks done.");
        }

        // ===== Debug Gizmos =====

        /// <summary>Показывать гизмо ячеек (рисуются всегда, не только при выделении).</summary>
        [Header("Debug")]
        [SerializeField] bool showGizmos = true;

        void OnDrawGizmos()
        {
            if (!showGizmos || settings == null)
                return;

            for (var i = 0; i < _cells.Count; i++)
            {
                _cells[i]?.OnDrawGizmos(settings);
            }
        }
    }
}
