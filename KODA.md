# KODA.md — Контекст проекта "They Will Descend"

## Обзор проекта

**They Will Descend** — RTS-игра (Real-Time Strategy), разрабатываемая в Unity. Проект включает:
- RTS-камеру (izoom-стиль, типичная для стратегий)
- NPC с круговым патрулированием
- Интеграцию FMOD Studio для продвинутого аудио
- 3 сцены: Bootstrap, Game, MainMenu + SubScene Simulation

## Структура проекта

```
they will desend/
├── KODA.md                          # Этот файл
├── README.md                        # Глобальный readme
├── Audio_FMOD/
│   └── they will desend/            # Проект FMOD Studio (.fspro)
│       ├── Assets/                  # Аудиоресурсы FMOD
│       ├── Metadata/                # Метаданные FMOD
│       ├── they will desend.fspro   # Главный проект FMOD Studio
│       └── .unsaved/                # Несохранённые данные
├── TheyWillDescend/                 # Unity-проект
│   └── Assets/
│       ├── Plugins/FMOD/            # FMOD Unity Plugin v2.03
│       ├── Scripts/                 # Глобальные скрипты (2 шт)
│       │   ├── RTSCameraController.cs
│       │   └── NpcCircleWalker.cs
│       ├── _Project/                # Основной контент
│       │   ├── Scenes/              # Основные сцены
│       │   │   ├── Bootstrap.unity
│       │   │   ├── Game.unity
│       │   │   └── MainMenu.unity
│       │   ├── SubScenes/           # SubScene
│       │   │   └── Simulation.unity
│       │   ├── Art/
│       │   │   └── Materials/
│       │   └── Scripts/
│       ├── Scripts/                 # Вспомогательные скрипты
│       ├── StreamingAssets/         # Runtime-банки FMOD
│       ├── Settings/
│       ├── TextMesh Pro/
│       └── (ассеты: Fantasy Skybox FREE, Hodaart, RPGPP_LT, Polytope Studio)
```

## Технологии

| Компонент | Версия / Детали |
|-----------|----------------|
| **Unity** | LWRP/URP (есть `#if UNITY_URP_EXIST` в коде FMOD) |
| **FMOD** | Unity Plugin v2.03 (официальная документация: https://fmod.com/docs/2.03/unity) |
| **FMOD Studio** | `.fspro` проект в `Audio_FMOD/they will desend/` |
| **Rendering** | URP (Universal Render Pipeline) |

## FMOD Integration

### Версия FMOD
- **Unity Plugin:** 2.03
- **Документация:** https://fmod.com/docs/2.03/unity
- **Файл проекта:** `Audio_FMOD/they will desend/they will desend.fspro`

### Как FMOD подключён к проекту

1. **Плагин** лежит в `TheyWillDescend/Assets/Plugins/FMOD/`
2. **Банки FMOD** собираются из проекта FMOD Studio и кладутся в `StreamingAssets/` (или папку банков, настроенную в Settings)
3. **RuntimeManager** — singleton, автоматически создаётся при старте. Инициализирует FMOD.Studio.System и Core System
4. **Банки загружаются автоматически** при старте (если `ImportType == StreamingAssets` и `BankLoadType == All`)

### FMOD API — основные методы (C#)

Все методы — статические, через `FMODUnity.RuntimeManager`:

```csharp
using FMODUnity;
using FMOD.Studio;
using UnityEngine;

// 1. Загрузить банк (если не загружен автоматически)
RuntimeManager.LoadBank("имя_банка");

// 2. Создать и запустить событие (программный способ)
EventReference eventRef = new EventReference() { Path = "event:/Sounds/Explosion" };
// Или с GUID:
// EventReference eventRef = new EventReference() { Guid = new FMOD.GUID(...) };

EventInstance instance = RuntimeManager.CreateInstance(eventRef);
instance.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position)); // для 3D-звуков
instance.start();
// ... позже:
instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
instance.release();

// 3. PlayOneShot — быстрый звук в позиции
RuntimeManager.PlayOneShot(eventRef, transform.position);

// 4. PlayOneShotAttached — звук, привязанный к GameObject
RuntimeManager.PlayOneShotAttached(eventRef, gameObject);

// 5. Привязать существующий экземпляр к GameObject (для 3D позиционирования)
RuntimeManager.AttachInstanceToGameObject(instance, gameObject);

// 6. Выгрузить банк
RuntimeManager.UnloadBank("имя_банка");

// 7. Получить доступ к studio system напрямую
FMOD.Studio.System studioSystem = RuntimeManager.StudioSystem;
FMOD.System coreSystem = RuntimeManager.CoreSystem;

// 8. Mute/Unmute
bool isMuted = RuntimeManager.IsMuted;
```

### Компоненты Unity для FMOD

| Компонент | Назначение |
|-----------|-----------|
| **StudioEventEmitter** | Привязывает FMOD-событие к GameObject. Воспроизводит событие при наведении, клике или старте |
| **EventHandler** | Аналогичен StudioEventEmitter, с дополнительными триггерами |
| **StudioListener** | Капсула слушателя для 3D-аудио. Нужно повесить на камеру |
| **FMODEventTrack / FMODEventPlayable** | Для Timeline — воспроизведение FMOD-событий в таймлайнах |
| **StudioGlobalParameterTrigger** | Триггер глобальных параметров FMOD |
| **StudioParameterTrigger** | Триггер параметров событий |

### Атрибуты для Inspector

```csharp
// Атрибут для красивого отображения EventReference в Inspector
[EventRefAttribute]
public EventReference explosionSound;

// Атрибут для BankReference
[BankRefAttribute]
public string myBank;

// Атрибут для параметров
[ParamRefAttribute]
public ParamRef rainIntensity;
```

### Настройка банка в FMOD Studio

1. В FMOD Studio: **File → Build**
2. Указать выходную папку (обычно `StreamingAssets` или кастомную)
3. Собрать банки — появятся `.bank` и `.strings` файлы
4. В Unity: настройки через `FMOD → Settings` (Component → FMOD Settings)

### Ключевые классы FMOD

| Класс | Описание |
|-------|----------|
| `RuntimeManager` | Singleton-менеджер. Инициализация, загрузка банков, создание событий |
| `StudioEventEmitter` | Компонент для привязки событий к GameObject |
| `EventHandler` | Старый аналог StudioEventEmitter |
| `EventReference` | Структура с GUID пути к событию в FMOD Studio |
| `StudioListener` | Капсула слушателя для 3D-аудио |
| `Platform` / `PlatformDefault` | Настройки платформ (channels, sample rate, codec) |
| `Settings` | Глобальные настройки FMOD (bank load type, logging и т.д.) |
| `StudioBankLoader` | Асинхронная загрузка банков |
| `RuntimeUtils` | Утилиты (To3DAttributes, DebugLog и т.д.) |

## Сцены

| Сцена | Назначение |
|-------|-----------|
| **Bootstrap** | Начальная сцена, вероятно для инициализации |
| **Game** | Основная игровая сцена |
| **MainMenu** | Главное меню |
| **Simulation** (SubScene) | Подсцена симуляции, включается через Scene Management |

## Существующие скрипты

### RTSCameraController.cs
- RTS-камера (вид сверху, зум, панорамирование)

### NpcCircleWalker.cs
- NPC, ходящий по кругу (патрулирование)

## Сборка и запуск

1. Открыть проект `TheyWillDescend/Assets/` в Unity
2. Убедиться, что FMOD Studio экспортировал банки в правильную папку
3. Проверить `FMOD → Settings` в Unity
4. Запустить сцену Bootstrap (или любую другую через Scenes)

## Настройка FMOD в Unity

Открыть: **FMOD → Settings** или через **Component → FMOD → FMOD Settings**

Ключевые параметры:
- **Import Type:** StreamingAssets (банки кладутся в StreamingAssets)
- **Bank Load Type:** All / Specified / None
- **Automatic Sample Loading:** загружать sample data сразу
- **Target Sub Folder:** подпапка для банков (если есть)
- **Logging Level:** уровень логирования

## Голосов и каналов

Настройки в `Settings` → Platform:
- **Real Channel Count:** реальные каналы (микшер)
- **Virtual Channel Count:** виртуальные каналы
- **Sample Rate:** частота дискретизации
- **DSP Buffer Length / Count:** буферы DSP
- **Speaker Mode:** режим динамиков

## Важные замечания

- `RuntimeManager` — singleton, создаётся автоматически. Не нужно создавать вручную
- `StudioListener` нужно повесить на камеру для 3D-аудио
- Для 3D-событий обязательно вызывай `set3DAttributes()` или используй `AttachInstanceToGameObject()`
- Банки загружаются автоматически при старте (если `BankLoadType == All`)
- Для unload банков используй `RuntimeManager.UnloadBank()`
- `instance.release()` освобождает экземпляр, но не останавливает звук (если не передать STOP_MODE)
- Для плавной остановки: `instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT)`
