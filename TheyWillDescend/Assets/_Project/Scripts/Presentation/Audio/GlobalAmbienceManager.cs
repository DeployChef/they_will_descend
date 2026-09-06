using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Глобальные ивенты атмосферы (ветер и т.п.). Один инстанс на ивент,
    /// не режется на зоны, не зависит от поля зрения камеры.
    /// Distance-принцип: дистанция камеры до опорной точки ивента двигает
    /// Distance-RTPC; при максимальной дистанции инстанс УМИРАЕТ (release) —
    /// не существует и не нагружает систему. При приближении возрождается.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlobalAmbienceManager : MonoBehaviour
    {
        [System.Serializable]
        public sealed class GlobalEvent
        {
            [Tooltip("Ивент. Перетаскивается из FMOD Studio (FMOD → Event Browser).")]
            public EventReference eventReference;

            [Tooltip("Имя Distance-параметра в ивенте (audio LOD), 0–100. Пусто = 'Distance'.")]
            public string distanceRtpc = "Distance";

            [Tooltip("Дистанция смерти (м): дальше — инстанс release. 0 = смерть отключена (ивент звучит всегда). Для ветра ставь 0 — он должен звучать и при максимальном отдалении.")]
            public float deathDistance = 0f;

            [Tooltip("Диапазон нормализации Distance-RTPC (м): dist/range × 100.")]
            public float rtpcRange = 120f;

            [Tooltip("Гистерезис (м): возрождение ближе чем deathDistance - hysteresis, чтобы инстанс не дёргался на границе.")]
            public float hysteresis = 5f;

            [Tooltip("Опорная точка ивента в мире. Если Use City Center — берётся центр города из DOTS.")]
            public Vector3 anchorPoint = Vector3.zero;

            [Tooltip("Брать центр города из CityGrid (DOTS) вместо Anchor Point.")]
            public bool useCityCenter = true;

            [HideInInspector] public EventInstance instance;
            [HideInInspector] public bool isDead = true;
        }

        [Header("Global Events")]
        [SerializeField] List<GlobalEvent> events = new();

        [Header("Camera")]
        [SerializeField] Camera mainCamera;

        [Header("Debug")]
        [SerializeField] bool logActivity = true;

        /// <summary>Центр города (из DOTS), fallback (0,0,0).</summary>
        float3 _cityCenter;

        bool _paused;

        public List<GlobalEvent> Events => events;

        void Start()
        {
            // Банки каждого ивента находятся автоматически (принцип зон).
            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e == null || e.eventReference.IsNull)
                    continue;

                FmodBankLoader.LoadBanksForEvent(e.eventReference);
            }
        }

        void Update()
        {
            if (events == null || events.Count == 0)
                return;

            // Камера может появиться позже Bootstrap — ищем лениво.
            if (mainCamera == null)
                mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            TryUpdateCityCenter();

            var camPos = mainCamera.transform.position;

            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e == null || e.eventReference.IsNull)
                    continue;

                var anchor = e.useCityCenter ? (Vector3)_cityCenter : e.anchorPoint;
                var dist = Vector3.Distance(camPos, anchor);
                var deathEnabled = e.deathDistance > 0f;

                if (e.isDead)
                {
                    // Возрождение: с гистерезисом, чтобы не дёргалось на границе.
                    // Смерть отключена (deathDistance = 0) — создаём сразу.
                    if (!deathEnabled || dist < e.deathDistance - Mathf.Max(0f, e.hysteresis))
                        CreateEvent(e, camPos);
                }
                else
                {
                    if (deathEnabled && dist >= e.deathDistance)
                    {
                        KillEvent(e);
                        continue;
                    }

                    // Audio LOD: 3D-позиция следует за камерой (звук «вокруг»),
                    // затухание управляется Distance-RTPC.
                    Set3DAtCamera(e, camPos);

                    var rtpc = string.IsNullOrEmpty(e.distanceRtpc) ? "Distance" : e.distanceRtpc;
                    var range = e.rtpcRange > 0f ? e.rtpcRange : e.deathDistance;
                    if (!string.IsNullOrEmpty(rtpc) && range > 0f)
                    {
                        var normalized = dist / range * 100f;
                        e.instance.setParameterByName(rtpc, Mathf.Clamp(normalized, 0f, 100f));
                    }
                }
            }
        }

        /// <summary>
        /// Создаёт и запускает инстанс глобального ивента.
        /// </summary>
        void CreateEvent(GlobalEvent e, Vector3 camPos)
        {
            try
            {
                e.instance = RuntimeManager.CreateInstance(e.eventReference);
                if (!e.instance.isValid())
                {
                    if (logActivity)
                        GameLog.Warning("GlobalAmbienceManager: event description not found for a global event.");
                    return;
                }

                Set3DAtCamera(e, camPos);
                e.instance.start();
                e.isDead = false;

                if (logActivity)
                    GameLog.Info("GlobalAmbienceManager: global event ACTIVATED.");
            }
            catch (System.Exception ex)
            {
                if (logActivity)
                    GameLog.Warning($"GlobalAmbienceManager: failed to create instance: {ex.Message}");
            }
        }

        /// <summary>
        /// Полностью освобождает инстанс — звук перестаёт существовать.
        /// </summary>
        void KillEvent(GlobalEvent e)
        {
            if (!e.instance.isValid())
            {
                e.isDead = true;
                return;
            }

            e.instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            e.instance.release();
            e.instance = default;
            e.isDead = true;

            if (logActivity)
                GameLog.Info("GlobalAmbienceManager: global event KILLED (max distance) — instance released.");
        }

        /// <summary>
        /// 3D-атрибуты инстанса: позиция = камера (звук звучит «вокруг»).
        /// </summary>
        static void Set3DAtCamera(GlobalEvent e, Vector3 camPos)
        {
            if (!e.instance.isValid())
                return;

            var attributes = new FMOD.ATTRIBUTES_3D
            {
                position = new FMOD.VECTOR { x = camPos.x, y = camPos.y, z = camPos.z },
                velocity = new FMOD.VECTOR { x = 0f, y = 0f, z = 0f },
                forward = new FMOD.VECTOR { x = 0f, y = 0f, z = 1f },
                up = new FMOD.VECTOR { x = 0f, y = 1f, z = 0f }
            };
            e.instance.set3DAttributes(attributes);
        }

        /// <summary>
        /// Центр города из DOTS (как в AudioZoneManager), fallback (0,0,0).
        /// </summary>
        void TryUpdateCityCenter()
        {
            if (SimWorld.TryGet(out var em, out var bag) && em.HasComponent<CityGrid>(bag))
            {
                var grid = em.GetComponentData<CityGrid>(bag);
                if (grid.Ready != 0 && grid.Config.IsValid)
                    _cityCenter = grid.Center;
            }
        }

        /// <summary>
        /// Пауза всех живых глобальных ивентов (подключить к паузе игры).
        /// </summary>
        public void SetPaused(bool paused)
        {
            _paused = paused;
            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e == null || e.isDead || !e.instance.isValid())
                    continue;
                e.instance.setPaused(paused);
            }
        }

        public bool IsPaused => _paused;

        void OnDisable()
        {
            // Компонент выключается — все инстансы умирают, ничего не звучит.
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i] != null)
                    KillEvent(events[i]);
            }
        }
    }
}
