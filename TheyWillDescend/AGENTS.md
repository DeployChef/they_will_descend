# Футалоид — They Will Descend

## Среда разработки

| Компонент | Версия |
|-----------|--------|
| **Unity** | 6000.4.0f1 (Unity 6 LTS) |
| **FMOD Studio Unity Integration** | 2.03 |

## Ссылки на документацию

### Unity
- [Unity 6 Documentation](https://docs.unity3d.com/6000.4/Documentation/Manual/index.html)
- [Unity Scripting API](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/index.html)
- [Entities Graphics (DOTS)](https://docs.unity3d.com/Packages/com.unity.entities@6.4)

### FMOD
- [FMOD Unity API Reference](https://fmod.com/docs/2.03/api/runtime.html)
- [FMOD Runtime API — C#](https://fmod.com/docs/2.03/runtime/api.html)
- [FMOD Unity Integration Docs](https://fmod.com/docs/2.03/unity)
- [FMOD Event Reference](https://fmod.com/features/event)

### FMOD — Ключевые классы Runtime API (C#)
```csharp
using FMOD;
using FMODUnity;

// Основные классы:
RuntimeUtils          — Утилиты для работы с FMOD в редакторе и рантайме
EventRef              — Ссылка на FMOD-событие
EventDescription      — Описание события (метаданные, длительность и т.д.)
EventInstance         — Экземпляр события для воспроизведения
ChannelGroup          — Группировка каналов (master, bus, sfx и т.д.)
Bank                  — Загруженный банк звуков
StudioSystem          — Главный entry point для FMOD Studio API

// FMOD Unity Integration API:
FMODUnity.RuntimeManager  — Основной класс для доступа к FMOD из C#
    .GetEvent(string path)  — Получить EventInstance по пути (например "event:/SFX/Explosion")
    .PlayEvent(string path) — Воспроизвести событие
    .Set3DAttributes()      — Установить 3D-атрибуты позиции

// FMOD Runtime API:
system.createEvent()        — Создать событие
event.start()               — Запустить воспроизведение
event.stop(FMOD.STOP_MODE.AllowFadeout) — Остановить
channel.setVolume()         — Управление громкостью
channel.setPitch()          — Управление питчем
```

### VContainer
- [VContainer Documentation](https://vcontainer.hadashiaw.jp/)
- [VContainer GitHub](https://github.com/hadashiA/VContainer)

## Структура проекта

```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Authoring/      — ECS Authoring (Baker-ы, Authoring-компоненты)
│   │   ├── Core/           — Общие сервисы (Audio, Events)
│   │   ├── Main/           — Сценозависимая логика
│   │   ├── Presentation/   — UI, View-компоненты
│   │   └── Simulation/     — ECS Systems, Components, Agents
│   ├── Scenes/             — Сцены Unity
│   └── SubScenes/          — SubScene для DOTS
├── Plugins/
│   └── FMOD/               — FMOD Unity Integration 2.03
└── Packages/               — manifest.json
```

## Assembly Definitions

| Assembly | Namespace | Зависимости |
|----------|-----------|-------------|
| TheyWillDescend.Authoring | TheyWillDescend.Authoring | Unity.Burst, Unity.Entities |
| TheyWillDescend.Simulation | TheyWillDescend.Simulation | Burst, Collections, Entities |
| TheyWillDescend.Presentation | TheyWillDescend.Presentation | Simulation, UI, Entities |
| TheyWillDescend.Main | TheyWillDescend.Main | Simulation, Presentation |

## Основные технологии

- **Architecture**: DOTS (ECS) + MonoBehaviour (UI/Authoring)
- **DI**: VContainer (jp.co.cyberagent.vcontainer@1.29.1)
- **Audio**: FMOD Studio 2.03 (Runtime API: FMOD, FMODUnity)
- **Render**: URP 17.4.0
- **UI**: TMP (TextMeshPro)
- **Референсный проект**: Futboloid.Core.Audio (Core-сборка, Assembly-CSharp)

## Структура сборок (asmdef)

```
Assembly-CSharp (Core)
├── Futboloid.Core.Audio          ← Референсный проект (AudioEventBus, AudioService, IAudioManager)
├── Futboloid.Core.Infrastructure
└── Futboloid.Main.Audio          ← FMODAudioManager, AudioTestStub

TheyWillDescend.Authoring
├── TheyWillDescend.Authoring     ← ECS Authoring
└── References: Assembly-CSharp

TheyWillDescend.Simulation
├── TheyWillDescend.Simulation    ← ECS Systems, Components
└── References: Assembly-CSharp

TheyWillDescend.Presentation
├── TheyWillDescend.Presentation  ← UI, View, DI-регистрации
├── Futboloid.Infrastructure      ← CoreScopeExtensions, MainScopeExtensions
└── References: Assembly-CSharp, TheyWillDescend.Simulation

TheyWillDescend.Main
├── Futboloid.Main.Infrastructure
└── References: Assembly-CSharp, TheyWillDescend.Presentation
```

## Правила работы с кодом

- Комментарии на английском
- Namespace для Core: `Futboloid.Core.*`
- Namespace для Main: `Futboloid.Main.*`
- Namespace для Presentation: `TheyWillDescend.Presentation.*`
- Namespace для Authoring: `TheyWillDescend.Authoring.*`
- Namespace для Simulation: `TheyWillDescend.Simulation.*`
- DI-регистрации выносятся в `Infrastructure/*ScopeExtensions.cs`
- Entity-имена: `Headquarters`, `ResourceStock` и т.д.

## Правила взаимодействия

### 🔍 Поиск в интернете — обязателен
- **Если пользователь сказал «найди в интернете» — это обязательное требование. Никаких других вариантов не существует.**
- Всегда используй `search_web` или `docs_search` для поиска актуальной информации, если есть хоть малейшие сомнения.
- При работе с API, пакетами, фреймворками — всегда проверяй документацию перед тем, как давать ответ.
- Никогда не додумывай и не угадывай — если не уверен на 100%, найди подтверждение в интернете.

### ✅ Обязательная перепроверка кода
- **Перед тем как считать задачу выполненной — перепроверь весь код.**
- Прочитай файл заново после изменений — убедись, что правки применены корректно.
- Проверь, что все зависимости (using, references, namespaces) согласованы.
- Проверь, что нет опечаток, дубликатов, лишних пустых строк.
- Убедись, что код компилируется — проверь все imports и типы.
- Не говори «задача выполнена», пока не убедился в этом дважды.

### 📝 Общие правила
- Не перефразируй tool arguments — передавай их точно как есть.
- Используй русский язык только в ответах пользователю.
- Делай минимальные изменения — не усложняй без необходимости.
- Приоритет: тест > исправление > повторная проверка.

### 🗣 Стиль общения
- Можно быть неформальным, без лишнего официоза.
- Мат уместен — если ситуация этого заслуживает, не стесняйся.
- Говори как человек, а не как корпоративный робот.
- Коротко и по делу — не размазывай ответ если можно сказать проще.
