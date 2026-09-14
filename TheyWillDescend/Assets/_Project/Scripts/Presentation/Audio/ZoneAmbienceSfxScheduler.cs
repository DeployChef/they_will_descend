using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.City;
using UnityEngine;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Планировщик случайных городских звуков («живчик города»), чтобы амбиент
    /// не был статичной подложкой.
    ///
    /// Носитель — активная аудио-зона: зона слышна только когда она в конусе
    /// камеры и в ней есть постройки, поэтому планировщик стреляет ТОЛЬКО по
    /// зонам с IsActive == true. Полный зум камеры глушит все зоны — кандидаты
    /// исчезают сами, отдельной проверки не нужно.
    ///
    /// Вероятность — независимый бросок на каждую активную зону в тик
    /// планировщика (не квота). После выстрела зона уходит в персональный
    /// кулдаун с разбросом, чтобы зоны не стреляли синхронно залпом.
    ///
    /// Ивент одноразовый (fire-and-forget): инстанс запускается в случайной
    /// точке зоны и ДОИГРЫВАЕТ САМ — планировщик его не глушит. Освобождение
    /// только когда FMOD сообщил PLAYBACK_STATE.STOPPED. Параллельность
    /// ограничена потолком Max Concurrent — сколько таких звуков звучит одновременно.
    ///
    /// Банк общий с основным амбиентом (Ambience_Town), поэтому загрузку банков
    /// не трогает: ивент доступен всегда, когда доступен основной амбиент.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ZoneAmbienceSfxScheduler : MonoBehaviour
    {
        /// <summary>
        /// Ожидаемая длина ивента, если FMOD её не отдал (сек). Ивенты городских
        /// случайностей — длинные цепочки (Ambience_Town_SFX_Random = 72 с), а длину
        /// FMOD не отдаёт, пока сэмпл не загружен. Занижать нельзя: инстанс
        /// глушится по этому сроку как страховка от утечки.
        /// </summary>
        const float UnknownLengthSeconds = 120f;

        /// <summary>
        /// Минимальный запас после ожидаемого конца (сек). Страховка от утечки
        /// инстанса, который не перешёл в STOPPED. Живой звук по этому сроку
        /// глушиться не должен, поэтому запас щедрый.
        /// </summary>
        const float MinLifetimeMargin = 30f;

        [Tooltip("Менеджер зон. Если пусто — находится автоматически.")]
        [SerializeField] AudioZoneManager zoneManager;

        /// <summary>Живые выстрелы: инстанс + потолок жизни (страховка от утечки).</summary>
        struct LiveShot
        {
            public EventInstance Instance;
            public int ZoneIndex;
            public float ForceReleaseTime;
        }

        /// <summary>Кулдаун каждой зоны (мировое время, до которого зона не тянет жребий).</summary>
        private float[] _zoneCooldownUntil;

        /// <summary>Время следующего тика планировщика.</summary>
        private float _nextTickTime;

        /// <summary>Живые инстансы звуков.</summary>
        private readonly List<LiveShot> _live = new();

        /// <summary>Ошибка «ивента нет» логируется один раз, чтобы не спамить.</summary>
        private bool _eventMissingLogged;

        /// <summary>Число звуков, играющих прямо сейчас.</summary>
        public int LiveCount => _live.Count;

        void Update()
        {
            // Менеджер может появиться позже (additive-сцены) — ищем лениво.
            if (zoneManager == null)
                zoneManager = FindFirstObjectByType<AudioZoneManager>();

            var settings = zoneManager != null ? zoneManager.Settings : null;
            if (settings == null)
                return;

            var zones = zoneManager.Zones;
            if (zones == null || zones.Length == 0)
                return;

            SyncCooldowns(zones, settings);
            ReleaseFinishedShots(settings);

            if (Time.unscaledTime < _nextTickTime)
                return;

            _nextTickTime = Time.unscaledTime + settings.SfxRandomTickInterval;
            RollZones(zones, settings);
        }

        /// <summary>
        /// Жребий по зонам: только активные, у которых истёк кулдаун,
        /// независимый бросок вероятности.
        /// </summary>
        private void RollZones(AudioZone[] zones, AudioZoneSettings settings)
        {
            var chance = settings.SfxRandomChance;
            if (chance <= 0f || _live.Count >= settings.SfxRandomMaxConcurrent)
                return;

            for (var i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                if (zone == null || !zone.IsActive)
                    continue;

                if (Time.unscaledTime < _zoneCooldownUntil[i])
                    continue;

                if (UnityEngine.Random.value > chance)
                    continue;

                TryFire(zone, settings, i);

                if (_live.Count >= settings.SfxRandomMaxConcurrent)
                    break;
            }
        }

        /// <summary>
        /// Запускает одноразовый звук в случайной точке зоны.
        /// </summary>
        private bool TryFire(AudioZone zone, AudioZoneSettings settings, int zoneIndex)
        {
            var position = zone.GetRandomSoundPosition(settings.SfxRandomJitterRadius);

            EventInstance instance;
            try
            {
                // Приоритет у EventReference (drag-and-drop), строковый путь — fallback.
                instance = !settings.SfxRandomEventReference.IsNull
                    ? RuntimeManager.CreateInstance(settings.SfxRandomEventReference)
                    : RuntimeManager.CreateInstance(settings.SfxRandomEventPath);
            }
            catch (System.Exception e)
            {
                LogOnce($"failed to create instance: {e.Message}");
                return false;
            }

            if (!instance.isValid())
            {
                LogOnce($"event '{settings.SfxRandomEventPath}' not found. Build banks in FMOD Studio and copy them to StreamingAssets.");
                return false;
            }

            instance.set3DAttributes(new FMOD.ATTRIBUTES_3D
            {
                position = new FMOD.VECTOR { x = position.x, y = position.y, z = position.z },
                velocity = new FMOD.VECTOR { x = 0f, y = 0f, z = 0f },
                forward = new FMOD.VECTOR { x = 0f, y = 0f, z = 1f },
                up = new FMOD.VECTOR { x = 0f, y = 1f, z = 0f }
            });

            instance.start();

            // Длинную цепочку не глушим: инстанс освобождается, когда FMOD сам
            // сообщит STOPPED. ForceReleaseTime — только страховка от утечки.
            var lifetime = ShotSeconds(instance, settings, out var lengthKnown);

            _zoneCooldownUntil[zoneIndex] = Time.unscaledTime + CooldownSeconds(settings);
            _live.Add(new LiveShot
            {
                Instance = instance,
                ZoneIndex = zoneIndex,
                ForceReleaseTime = Time.unscaledTime + lifetime,
            });

            if (settings.LogSfxRandom)
                GameLog.Info($"[SfxRandom] zone {zone.Sector}x{zone.Radial} at {position} "
                    + $"(live {_live.Count}/{settings.SfxRandomMaxConcurrent}, "
                    + $"lifetime {lifetime:F1}s{(lengthKnown ? " from FMOD" : " fallback")})");

            return true;
        }

        /// <summary>
        /// Освобождает доигравшие инстансы и освобождает слоты параллельности.
        /// Нормальный путь — STOPPED от FMOD: звук доиграл сам, мы его не трогали.
        /// </summary>
        private void ReleaseFinishedShots(AudioZoneSettings settings)
        {
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                var shot = _live[i];

                if (!shot.Instance.isValid())
                {
                    _live.RemoveAt(i);
                    continue;
                }

                shot.Instance.getPlaybackState(out var state);
                // STOPPING = ивент сам затухает, дождёмся STOPPED и отпустим.
                var stopped = state == PLAYBACK_STATE.STOPPED;

                if (!stopped && Time.unscaledTime < shot.ForceReleaseTime)
                    continue;

                // Доиграл сам или сработала страховка. Если инстанс ещё играл —
                // страховка сработала раньше конца, это видно в логах.
                if (!stopped && settings.LogSfxRandom)
                    Debug.LogWarning($"[SfxRandom] force-release zone {shot.ZoneIndex} while state={state} "
                        + $"(ивент короче ожидаемого? проверь длину в FMOD)");

                shot.Instance.release();
                _live.RemoveAt(i);
            }
        }

        /// <summary>
        /// Кулдаун зоны с разбросом (чтобы зоны не стреляли синхронно).
        /// </summary>
        private static float CooldownSeconds(AudioZoneSettings settings)
        {
            var baseCooldown = Mathf.Max(0.1f, settings.SfxRandomZoneCooldown);
            var jitter = baseCooldown * settings.SfxRandomCooldownJitter;
            return baseCooldown + UnityEngine.Random.Range(-jitter, jitter);
        }

        /// <summary>
        /// Потолок жизни инстанса (сек) — страховка от утечки, а НЕ длительность
        /// воспроизведения: живой звук глушится только hot reload'ом банков.
        /// Длина берётся из FMOD; если FMOD её не отдал (сэмпл ещё не загружен) —
        /// щедрый запас, чтобы длинную цепочку не обрезать.
        /// </summary>
        private static float ShotSeconds(EventInstance instance, AudioZoneSettings settings, out bool lengthKnown)
        {
            var seconds = UnknownLengthSeconds;
            lengthKnown = false;

            if (instance.getDescription(out var description) == FMOD.RESULT.OK
                && description.isValid()
                && description.getLength(out var lengthMs) == FMOD.RESULT.OK
                && lengthMs > 0)
            {
                seconds = lengthMs * 0.001f;
                lengthKnown = true;
            }

            return seconds + Mathf.Max(MinLifetimeMargin, settings.SfxRandomLifetimeMargin);
        }

        /// <summary>
        /// Синхронизирует массив кулдаунов с числом зон. При пересборке зон
        /// (сдвиг центра сетки) зоны новые — жребий раздается случайным стартовым
        /// отложенным сроком, чтобы не было залпа в первый же тик.
        /// </summary>
        private void SyncCooldowns(AudioZone[] zones, AudioZoneSettings settings)
        {
            if (_zoneCooldownUntil != null && _zoneCooldownUntil.Length == zones.Length)
                return;

            _zoneCooldownUntil = new float[zones.Length];
            var spread = Mathf.Max(0.1f, settings.SfxRandomZoneCooldown);
            for (var i = 0; i < _zoneCooldownUntil.Length; i++)
                _zoneCooldownUntil[i] = Time.unscaledTime + UnityEngine.Random.Range(0f, spread);
        }

        /// <summary>
        /// Глушит все живые звуки. Вызывается при hot reload банков: инстансы
        /// ссылаются на ивенты, которые сейчас выгрузятся.
        /// </summary>
        public void SilenceAll()
        {
            for (var i = 0; i < _live.Count; i++)
            {
                var shot = _live[i];
                if (!shot.Instance.isValid())
                    continue;

                shot.Instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                shot.Instance.release();
            }

            _live.Clear();

            if (_zoneCooldownUntil != null)
                System.Array.Clear(_zoneCooldownUntil, 0, _zoneCooldownUntil.Length);
        }

        void OnDestroy() => SilenceAll();

        private void LogOnce(string message)
        {
            if (_eventMissingLogged)
                return;

            _eventMissingLogged = true;
            GameLog.Warning($"[ZoneAmbienceSfxScheduler] {message}");
        }
    }
}
