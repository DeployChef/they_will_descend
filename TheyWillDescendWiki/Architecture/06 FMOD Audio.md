# 06 FMOD Audio

← [[05 Content Pipeline]] | [[Index]]

> Версия: **FMOD Studio 2.03.11** · FMOD Unity Integration 2.03.x  
> Офиц. API: https://www.fmod.com/docs/2.03/unity/api.html  
> Офиц. User Guide: https://www.fmod.com/docs/2.03/unity/user-guide.html

---

## Структура в репозитории

```
they_will_descend/
├── Audio_FMOD/they will desend/       ← FMOD Studio проект (.fspro)
│   ├── Audio/                          ← исходные wav/mp3 (Git LFS)
│   ├── Metadata/                       ← автоген FMOD (коммитим)
│   ├── Build/Desktop/                  ← FMOD собирает банки сюда
│   └── .user/                          ← игнорируется (.gitignore)
├── TheyWillDescend/Assets/
│   ├── Plugins/FMOD/                  ← плагин интеграции (com.fmod.unity)
│   └── StreamingAssets/Desktop/       ← .bank файлы для Unity (коммитим)
└── .gitattributes                      ← Git LFS: *.wav *.mp3 *.bank
```

### Workflow звукорежиссёра

1. Открыть `Audio_FMOD/they will desend/they will desend.fspro` в FMOD Studio
2. Настроить Build Path: **Edit → Preferences → Build → Build Path** → `Build/` (FMOD сам создаст подпапку `Desktop/`)
3. Редактировать звук → **`Ctrl+B`** (Build)
4. Скопировать `.bank` + `.strings.bank` из `Build/Desktop/` → `TheyWillDescend/Assets/StreamingAssets/Desktop/`
5. Коммит: `.fspro`-изменения + новые банки

### Workflow программиста (без FMOD Studio)

1. `git clone` → банки уже в `StreamingAssets/Desktop/`
2. Unity открывает → звук работает сразу
3. FMOD Studio **не нужен**

---

## Правила именования в FMOD Studio

### События (Events)

| Правило | Пример |
|---|---|
| Путь: `event:/категория/подкатегория/имя` | `event:/gameplay/pyramid/tribute_paid` |
| Стиль: `snake_case` | `event:/ui/button_hover` |
| Группировка по контексту, не по типу звука | `event:/world/ambience/plaza` ✅ · `event:/loops/amb_03` ❌ |

### Банки (Banks)

| Банк | Что внутри | Когда грузить |
|---|---|---|
| `Master` | Всегда загружен, базовый микшер | Автоматически при старте |
| `Master.strings.bank` | Строковые ключи событий | Автоматически вместе с Master |
| `UI` | Интерфейс, кнопки, меню | При старте (всегда нужен) |
| `Gameplay` | События города, жителей, экономики | При загрузке сцены игры |
| `Music` | Музыкальные темы, стемы | По необходимости |
| `Cinematic` | Кризисы, особые моменты | По необходимости (Unload после) |

### Параметры (Parameters)

| Правило | Пример |
|---|---|
| `snake_case` | `intensity`, `population`, `danger_level` |
| Диапазон 0–1 для нормализованных | `heat` 0.0–1.0 |
| Диапазон 0–100 для абсолютных | `population` 0–100 |
| Labeled-параметры для состояний | `weather` → `clear / rain / storm` |

### Шины (Buses)

```
Master
├── Music
├── SFX
│   ├── Gameplay
│   ├── UI
│   └── World
└── Voice
```

---

## API — FMODUnity.RuntimeManager

> Главное окно в систему FMOD из Unity-кода.  
> Namespace: `FMODUnity`  
> Источник: https://www.fmod.com/docs/2.03/unity/api-runtimemanager.html

### Загрузка банков

```csharp
// В проекте не вызывается напрямую — только через наборы AudioBanks (см. «Аудио в проекте»).
Bank gameplayBank = RuntimeManager.LoadBank("Gameplay");
RuntimeManager.LoadBank("Cinematic", loadSamples: true);
RuntimeManager.UnloadBank("Cinematic");
bool loaded = RuntimeManager.HasBankLoaded("Gameplay");
```

### Fire-and-forget (одноразовые звуки)

```csharp
// По EventReference (рекомендуется):
[ParamRef] public EventReference uiClickEvent;

RuntimeManager.PlayOneShot(uiClickEvent, transform.position);

// С привязкой к GameObject (3D позиция следует за объектом):
RuntimeManager.PlayOneShotAttached(uiClickEvent, gameObject);

// Устаревший способ — по строковому пути (избегать):
RuntimeManager.PlayOneShot("event:/ui/button_click", transform.position);
```

### Управляемые инстансы (loop, параметры, stop)

```csharp
EventInstance instance = RuntimeManager.CreateInstance(eventReference);
instance.set3DAttributes(RuntimeUtils.To3DAttributes(transform));
instance.start();
// ... меняем параметры в процессе ...
instance.setParameterByName("intensity", 0.8f);
// ... останавливаем:
instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
instance.release();
```

### Привязка к GameObject

```csharp
EventInstance instance = RuntimeManager.CreateInstance(eventReference);
RuntimeManager.AttachInstanceToGameObject(instance, transform, GetComponent<Rigidbody>());
instance.start();
// Отвязать (если нужно):
RuntimeManager.DetachInstanceFromGameObject(instance);
```

### Шины и VCA

```csharp
Bus sfxBus = RuntimeManager.GetBus("bus:/SFX");
sfxBus.setMute(false);
sfxBus.setVolume(0.8f);
sfxBus.stopAllEvents(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);

VCA musicVca = RuntimeManager.GetVCA("vca:/Music");
musicVca.setVolume(0.5f);
```

### Глобальные параметры

```csharp
RuntimeManager.SetGlobalParameter("danger_level", 0.7f);
float value = RuntimeManager.GetGlobalParameter("danger_level");
```

### Низкоуровневый доступ

```csharp
FMOD.Studio.System studioSystem = RuntimeManager.StudioSystem;  // Studio API
FMOD.System       coreSystem    = RuntimeManager.CoreSystem;     // Core API
```

---

## API — FMOD.Studio.EventInstance

> Namespace: `FMOD.Studio`  
> Источник: https://www.fmod.com/docs/2.03/api/studio-api-eventinstance.html

| Метод | Описание |
|---|---|
| `start()` | Запуск воспроизведения |
| `stop(STOP_MODE mode)` | Остановка: `STOP_IMMEDIATE` или `STOP_ALLOWFADEOUT` |
| `release()` | Освободить ресурсы инстанса. **Вызывать после `start()`** для fire-and-forget, или **после `stop()`** для управляемых |
| `setParameterByName(string name, float value)` | Установить параметр по имени |
| `getParameterByName(string name, out float value)` | Получить текущее значение параметра |
| `setParameterByID(PARAMETER_ID id, float value)` | Установить параметр по ID (быстрее) |
| `set3DAttributes(ATTRIBUTES_3D attributes)` | Задать 3D-позицию (через `RuntimeUtils.To3DAttributes`) |
| `get3DAttributes(out ATTRIBUTES_3D attributes)` | Получить 3D-позицию |
| `getPlaybackState(out PLAYBACK_STATE state)` | Состояние: `PLAYING / SUSTAINING / STOPPED / STARTING / STOPPING` |
| `setVolume(float volume)` | Громкость инстанса (0.0–1.0) |
| `setPitch(float pitch)` | Питч (1.0 = норма) |
| `setTimelinePosition(int position)` | Перемотка (в миллисекундах) |
| `getTimelinePosition(out int position)` | Текущая позиция (мс) |
| `keyOff()` | Триггер key-off (для ASR-орнаментированных событий) |
| `isValid()` | Проверка, что инстанс жив |

---

## API — FMODUnity.EventReference

> Способ ссылаться на события в коде.  
> В отличие от строковых путей — переживает переименования в FMOD Studio.

```csharp
// В инспекторе — пикер события:
[ParamRef] public EventReference myEvent;

// Проверка:
if (!myEvent.IsNull) { ... }

// Путь:
string path = myEvent.Path;
```

---

## API — StudioEventEmitter (компонент)

> Удобен для 3D-звуков, привязанных к объектам.  
> Namespace: `FMODUnity`

```csharp
// Вешается на GameObject, событие выбирается в инспекторе:
[RequireComponent(typeof(StudioEventEmitter))]
public class Fire : MonoBehaviour {
    StudioEventEmitter emitter;

    void Awake() => emitter = GetComponent<StudioEventEmitter>();

    void OnEnable()  => emitter.Play();
    void OnDisable() => emitter.Stop();
}
```

| Метод | Описание |
|---|---|
| `Play()` | Запуск события |
| `Stop()` | Остановка (с fade, если настроено в FMOD) |
| `SetParameter(string name, float value)` | Установить параметр |
| `IsPlaying()` | Играет ли сейчас |

---

## Аудио в проекте

Три слоя по срокам жизни, как у оболочки ([[09 App Shell]]):

| Слой | Что | Где |
| --- | --- | --- |
| **Процесс** | банки, музыка, снапшоты, UI-звук | `AudioService` — entry point `AppScope` (`Presentation/Audio/Service`) |
| **Сцена Game** | звук мира: ячейки зон, ветер, случайные звуки города, стройка | `WorldAudio` в `Game.unity`; зависимости от `GameLifetimeScope` |
| **Мост ECS → звук** | пауза | `SimPauseAudioBridge` в сцене Game (`Presentation/SimBridge`) |

`AudioService` **не читает ECS**. Всё, что звук должен знать о ране, приносит мост в сцене Game. Симуляция звук не вызывает.

### AudioService

```text
AudioService (ITickable, ILateTickable, IDisposable)
  Catalog   AudioCatalog — все ссылки FMOD
  Banks     AudioBanks: Acquire(set, ct) → IDisposable; счётчик ссылок FMOD
  Music     MusicDirector: Set(MusicTheme) / Set(MusicCue); один инстанс на процесс; следует за текущим слушателем
  Ui        UiAudio: Play(UiState); hover/click по raycast указателя
  SetPaused(bool)   снапшот ESC + пауза музыки
```

Статический вход для UI-кода без внедрения — `UiSound.Play(state)`. Один экземпляр, два входа: DI для сервисов, статика для мелких компонентов UI.

### Каталог вместо строк

`Content/Audio/AudioCatalog.asset` — единственное место с ссылками FMOD: наборы банков, cue музыки (меню / игра), снапшот паузы, UI-событие и его параметр, ветер, шина амбиента, стройка. В коде `event:/`, `snapshot:/`, `bank:/` не встречаются (проверка: `rg "event:/|snapshot:/|bank:/" Presentation`). Настройки звука зон — `Content/Audio/AudioZoneSettings.asset`.

### Наборы банков

| Набор | Банки | Кто держит |
| --- | --- | --- |
| `CoreBanks` | Master.strings, Master, UI, Main_theme | `AudioService`, весь процесс |
| `GameWorldBanks` | Ambience_Town, Ambience_Wind_Generator, BUILDING | `GameEntry`: задача `LoadingTasks` на входе в Game, отпускает в teardown |

- `Acquire` грузит банки с сэмплами и ждёт, пока сэмплы догрузятся — под экраном загрузки.
- Выгрузка — кадром позже, чтобы инстансы сцены успели остановиться.
- При рестарте Game новый набор берётся раньше, чем отпускается старый: счётчик ссылок не падает до нуля, банки не перезагружаются.
- Новый звук мира → его банк в `GameWorldBanks`. Новая сцена со своим звуком → свой набор, берёт её точка входа.

### Музыка

Одно событие `MUSIC_STATES` на процесс. Cue = событие + параметр + метка. Смена cue с тем же событием меняет только метку; с другим — ALLOWFADEOUT старого и старт нового. **Сейчас** cue меню и игры без метки: у глобального параметра `MUSIC_STATES` есть только IDLE / WIN / DEFEAT. Метки меню и игры — за звукорежиссёром; код их уже понимает.

Не ускорять pitch музыки через `timeScale`.

---

## Правила для программистов

### 1. EventReference вместо строк

```csharp
// ✅ Правильно:
[ParamRef] public EventReference tributeEvent;
RuntimeManager.PlayOneShot(tributeEvent, pos);

// ❌ Неправильно (ломается при переименовании в FMOD):
RuntimeManager.PlayOneShot("event:/gameplay/pyramid/tribute", pos);
```

### 2. release() — обязательно

```csharp
// Fire-and-forget — release сразу после start:
var inst = RuntimeManager.CreateInstance(ev);
inst.start();
inst.release();  // инстанс живёт до конца события, потом авто-удаляется

// Управляемый — release после stop:
var inst = RuntimeManager.CreateInstance(ev);
inst.start();
// ... позже ...
inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
inst.release();
```

### 3. Не создавать инстансы каждый кадр

```csharp
// ❌ Плохо (в Update):
void Update() {
    var inst = RuntimeManager.CreateInstance(ev);
    inst.start(); inst.release();
}

// ✅ Хорошо:
void Update() {
    if (condition) RuntimeManager.PlayOneShot(ev, transform.position);
}
```

### 4. Банки — только через наборы

```csharp
// Точка входа сцены:
_loading.Add(AcquireWorldBanks(ct), 1f, "Loading sounds…");
async UniTask AcquireWorldBanks(CancellationToken ct) =>
    _worldBanks = await _audio.Banks.Acquire(_audio.Catalog.GameWorld, ct);

// Teardown сцены:
_worldBanks.Dispose();   // выгрузка кадром позже
```

`RuntimeManager.LoadBank` / `UnloadBank` напрямую в коде фич не зовём: у банка один владелец — набор.

### 5. 3D-позиция — через RuntimeUtils

```csharp
// ✅ Правильно:
instance.set3DAttributes(RuntimeUtils.To3DAttributes(transform));

// ❌ Не вручную собирать ATTRIBUTES_3D.
```

### 6. Звук сцены не живёт дольше её банков

Инстансы звука мира создаются после `Acquire` и останавливаются в `OnDestroy` своего компонента. Сервис процесса не держит инстансы событий из банков сцены.

---

## Настройки FMOD в Unity

**FMOD → Edit Settings** (или `Edit → Project Settings → FMOD Studio`):

| Параметр | Значение |
|---|---|
| **Import Type** | Streaming Assets |
| **Bank Sub Folder** | `Desktop` |
| **Load Banks** | **None** — Core грузит `AudioService` из каталога, GameWorld — `GameEntry`. Список банков живёт в одном месте |
| **Load All Sample Data** | ❌ — сэмплы грузит `Acquire` набора |
| **Live Update** | ✅ в редакторе, ❌ в билде |
| **Studio Project Path** | `../../Audio_FMOD/they will desend/they will desend.fspro` |

---

## Ссылки

- [FMOD 2.03 Unity API](https://www.fmod.com/docs/2.03/unity/api.html)
- [RuntimeManager](https://www.fmod.com/docs/2.03/unity/api-runtimemanager.html)
- [EventInstance (Studio API)](https://www.fmod.com/docs/2.03/api/studio-api-eventinstance.html)
- [Scripting Examples](https://www.fmod.com/docs/2.03/unity/examples-basic.html)
- [User Guide](https://www.fmod.com/docs/2.03/unity/user-guide.html)

---

Связанные разделы: [[01 Folder Structure]] · [[03 Core Systems]] · [[../GDD/08 UI & Visual|UI & Visual]]
