# They Will Descend — Техническая спецификация аудио-подсистемы и FMOD-интеграции

> [!IMPORTANT]
> **Назначение документа:** единый источник правды по звуковой архитектуре **They Will Descend** для программистов (классы, жизненный цикл, FMOD API, загрузка банков) и саунд-дизайнеров (контракт событий и параметров, которые читает/пишет код). Разделы 3–7 — **точный список FMOD-вызовов и параметров из реального кода** (Music, UI, Snapshot, Ветер, Амбиент, Стройка). Раздел 9 — целевая архитектура MVP-демки. Синхронизирован с мастер-GDD и спецификацией MVP-среза.

---

## 1. Стек, пути и границы системы

| Компонент | Значение |
| :--- | :--- |
| Движок | Unity 6 (URP 17.4.0, Entities 6.4.0), DOTS/ECS симуляция + MonoBehaviour-презентация (VContainer, UniTask) |
| Аудио | **FMOD Studio 2.03.11** для Unity (ImportType = StreamingAssets) |
| Проект FMOD | `Audio_FMOD/they will desend/they will desend.fspro` |
| Сборка банков | `Audio_FMOD/they will desend/Build/Desktop/` |
| Банки в Unity | `TheyWillDescend/Assets/StreamingAssets/Desktop/` (**копируются вручную после Ctrl+B**) |

**Нерушимые границы:**

1. **Симуляция (ECS) не знает про FMOD.** Аудио живёт в Presentation; связь с игрой — через мосты (например `SimPauseAudioBridge`: ECS-пауза → `AudioService.SetPaused`, «игра знает о звуке, звук об игре — нет»).
2. **Один владелец на инстанс** (реестр §3). Музыку кроме `MusicDirector` никто не останавливает.
3. **Все ссылки на события — через `AudioCatalog`** (ScriptableObject, «весь звук, который код называет по имени, в одном ассете саунд-дизайнера»; `EventReference` переживает переименования в FMOD Studio). Строковые пути событий в коде не хранятся.
4. **Код управляет только Game Parameter'ами** (XML `parameterType` 0/1/2). Автоматические (XML ≥ 10) и unexposed — readonly, `setParameterByName` на них молча не работает. Резолв параметров — по `PARAMETER_ID` с `Trim()`-матчем имени (в FMOD-проекте бывают имена с хвостовым пробелом: `"BUILDING "`, `"Distance "`).
5. **FMOD-объекты — структуры:** проверки только `isValid()`, стоп только `FMOD.Studio.STOP_MODE.*` (полный путь).

---

## 2. Каркас: AudioService, AudioCatalog, банки

### 2.1. AudioCatalog (ScriptableObject — владеет саунд-дизайнер)

| Поле | Тип | Что это |
| :--- | :--- | :--- |
| `core` | `AudioBankSet` | Банки процесса: грузятся при старте, живут до выхода (музыка, UI, снэпшот) |
| `gameWorld` | `AudioBankSet` | Банки сцены Game: грузятся вместе со сценой, освобождаются при выходе (амбиент, ветер, стройка) |
| `menuMusic` / `gameplayMusic` | `MusicCue` | Музыкальные кии меню и геймплея (§4) |
| `pauseSnapshot` | `EventReference` | Снэпшот паузы `snapshot:/ESC` (§6) |
| `uiEvent` + `uiStateParameter` | `EventReference` + string | Ивент UI и имя его labeled-параметра, по умолчанию **`UI_States`** (§5) |
| `wind` | `EventReference` | Ветер `event:/AMBIENCE/Ambience_Wind_Generation` (§7) |
| `ambienceBus` | string | Путь шины амбиента (`bus:/Ambience`) — FMOD не имеет bus-reference типа, поэтому строка |
| `construction` | `EventReference` | Событие стройки для видов без своего события (§8) |

`AudioBankSet` — список имён банков `[BankRef] string[]`. `AudioService` (AppScope, `ITickable/ILateTickable/IDisposable`) при создании грузит Core-набор, создаёт `MusicDirector`, `AudioSnapshots`, `UiAudio`. `AudioBanks.Load` — `RuntimeManager.LoadBank(name, loadSamples: true)`, `BankLoadException` логируется как Error (не роняет игру); `Acquire(set)` ждёт докачки сэмплов (`RuntimeManager.AnySampleDataLoading`). Выгрузка набора — через кадр после `OnDestroy` сцен-объектов. `WorldBanksLoaded` — все инстансы мира проверяют его перед созданием. Hot reload (Editor): `AudioBanks.ReloadInEditor(set)` — Unload+Load каждого банка сэта.

---

## 3. Реестр владельцев звуков (кто что дергает)

| Класс | Звук | Событие FMOD | Набор банков |
| :--- | :--- | :--- | :--- |
| `MusicDirector` (через `AudioService.Music`) | Музыка | `MusicCue.Event` (MUSIC_STATES) | Core |
| `UiAudio` (через `AudioService.Ui`) | UI | `AudioCatalog.UiEvent` | Core |
| `AudioSnapshots` | Снэпшот паузы | `AudioCatalog.PauseSnapshot` (`snapshot:/ESC`) | Core |
| `WindAmbience` | Ветер | `AudioCatalog.Wind` | GameWorld |
| `AudioCell` (реестр — `AudioZoneManager`) | Амбиент города | `AudioZoneSettings.EventReference` (`event:/AMBIENCE/Ambience_Town`) | GameWorld |
| `ZoneAmbienceSfxScheduler` | Случайные городские SFX | `AudioZoneSettings.SfxRandomEventReference` (`event:/AMBIENCE/Ambience_Town_SFX_Random`) | GameWorld |
| `BuildingConstructionAudio` | Стройка | Своё на префабе или `AudioCatalog.Construction` (`event:/BUILD`) | GameWorld |

Точка входа UI-листьев: статический `UiSound.Play(UiState)` → `AudioService.Current?.Ui.Play(...)` (вне Play — тихо).

---

## 4. Музыка — MusicDirector / MusicCue

### 4.1. Контракт FMOD

**Один живой инстанс** `EventInstance` на всё время процесса. Смена кия:
- **тот же ивент, другой `Label`** → меняется состояние (переходы внутри ивента — работа FMOD, тайминги/фейды саунд-дизайнера);
- **другой ивент** → кроссфейд: старый `stop(ALLOWFADEOUT)` + `release()`, новый `CreateInstance` + `start()`.

**Что дергает код (MusicDirector):**

| Вызов FMOD | Когда | Зачем |
| :--- | :--- | :--- |
| `RuntimeManager.CreateInstance(cue.Event)` | смена музыкального ивента | новый инстанс (`EventNotFoundException` → GameLog.Error, ивент ищется в Core-наборе) |
| `instance.setParameterByNameWithLabel(cue.Parameter, cue.Label)` | кий с состоянием | переключение состояния. При `ERR_EVENT_NOTFOUND` — retry **глобально**: `StudioSystem.setParameterByNameWithLabel(...)` (параметр может быть global). Не `OK` → Warning |
| `instance.setPaused(paused)` | пауза игры (§6) | музыка стоит вместе с снэпшотом |
| `instance.set3DAttributes(listenerAttributes)` | каждый `LateTick` (`Music.FollowListener()`) | ивент 3D: «сидит» на текущем слушателе (`StudioSystem.getListenerAttributes(0, out ...)`), сцены меняют слушателей, музыка переживает их все |
| `instance.start()` | после создания | запуск |
| `instance.stop(ALLOWFADEOUT/IMMEDIATE)` + `release()` + `clearHandle()` | смена ивента / Dispose | корректный teardown структуры |

**MusicCue (структура, сериализуется в AudioCatalog):**

```csharp
public struct MusicCue {
    public EventReference Event;   // ивент музыки (пусто = Stop(ALLOWFADEOUT), тишина)
    public string Parameter;       // labeled-параметр НА ивенте (или global). Пусто = играть как есть
    public string Label;           // лейбл состояния для этого кия
    public bool HasState => ...;   // Parameter и Label непустые
}
```

`MusicTheme` { `Menu`, `Gameplay` } → `AudioCatalog.Music(theme)` отдает `menuMusic` / `gameplayMusic`. Код не переключает внутренние состояния генерации — только стартовый Label кия.

**Требования к FMOD (для саунд-дизайнера):**
- Параметр `cue.Parameter` — **labeled Game Parameter** на самом ивенте MUSIC_STATES либо **глобальный** labeled-параметр; лейблы в киях каталога должны совпадать с лейблами FMOD один в один.
- Внутренние состояния/переходы (стейт-машина музыки, вложенные ивенты вроде main_soundtrack) — целиком внутри FMOD, код их не трогает.

---

## 5. UI — UiAudio (один персистентный ивент + flash стейта)

### 5.1. Контракт FMOD

**Один инстанс UI-ивента на весь процесс**, создаётся в конструкторе `AudioService`. Инструменты ивента висят на листе параметра и срабатывают при входе параметра в их значение — каждый звук UI это «вспышка» состояния.

**Параметр `UI_States`** (имя — `AudioCatalog.UiStateParameter`, по умолчанию `UI_States`): **labeled Game Parameter на самом ивенте**, значения:

| Значение | Лейбл | Когда ставит код |
| :--- | :--- | :--- |
| 0 | `NONE` | возврат в тишину после вспышки (или старт — ставится принудительно ДО `start()`, иначе ивент открывается на пресете и играет звук на старте) |
| 1 | `HOVER` | луч из курсора нашёл интерактивный `Selectable` (raycast `EventSystem.RaycastAll`, без компонентов на кнопках) |
| 2 | `YES` | подтверждение — `UiSound.Play(UiState.Yes)` из кода UI |
| 3 | `NO` | отказ — `UiSound.Play(UiState.No)` |
| 4 | `WARNING` | предупреждение — `UiSound.Play(UiState.Warning)` |
| 5 | `CLICK` | нажатие ЛКМ над интерактивным элементом |

**Что дергает код:**

| Вызов FMOD | Когда | Детали |
| :--- | :--- | :--- |
| `RuntimeManager.CreateInstance(uiEvent)` | старт процесса | Core-набор |
| `description.getParameterDescriptionByName("UI_States")` | резолв `PARAMETER_ID` | Проверка `PARAMETER_FLAGS.READONLY` — если авто-параметр, Warning и UI-звука нет |
| `instance.setParameterByID(id, (float)state, ignoreseekspeed: true)` | каждый `Play(state)` | Один и тот же стейт дважды внутри вспышки игнорируется |
| `instance.start()` | после резолва | один раз |
| `instance.stop(ALLOWFADEOUT)` + `release()` | Dispose | — |

**Механика вспышки (Flash):** FMOD читает параметры миксерными чанками ~20 мс — вспышка длиной в один кадр между чанками теряется. Поэтому после `Play(state != None)` код держит значение `FlashSeconds = 0.1 с` (unscaled), затем `Tick()` возвращает `NONE`. Дубликат стейта внутри окна не отправляется. HOVER/CLICK полностью автоматические (никаких вызовов из кнопок), YES/NO/WARNING — точечные `UiSound.Play(...)`.

---

## 6. Пауза — AudioSnapshots + SimPauseAudioBridge

**Единственный источник истины паузы — `SimControl.PlayerPaused` в ECS.** `SimPauseAudioBridge` (Game-сцена, `ILateTickable`) каждый LateTick сравнивает и при смене зовёт `AudioService.SetPaused(paused)`, который:

1. `AudioSnapshots.SetPaused(true)` → `RuntimeManager.CreateInstance(snapshot ESC)` + `start()`; выход → `stop(ALLOWFADEOUT)` + `release()`. Снэпшот один, пересоздаётся на каждую паузу.
2. `MusicDirector.SetPaused(true)` → `instance.setPaused(true)` (см. §4).

Прямой toggle по Esc из аудио-кода запрещён (разъезжался с кнопкой Continue). Выход из Game-сцены снимает паузу (`Dispose` моста).

---

## 7. Ветер — WindAmbience (глобальный Distance по высоте камеры)

### 7.1. Контракт FMOD

**Один eternal 2D-инстанс** `AudioCatalog.Wind` (`event:/AMBIENCE/Ambience_Wind_Generation`), создаётся через кадр после старта (банки GameWorld), освобождается со сценой. `AttachInstanceToGameObject` не используется.

**Параметр `Distance` — ГЛОБАЛЬНЫЙ Continuous 0..1** (резолвится через `StudioSystem.getParameterDescriptionList` + `Trim()`/`OrdinalIgnoreCase` → `PARAMETER_ID`; прямой матч по имени ломается на хвостовом пробеле `"Distance "`).

**Что дергает код (каждый LateUpdate, с экспоненциальным сглаживанием):**

| Вызов FMOD | Когда | Детали |
| :--- | :--- | :--- |
| `StudioSystem.setParameterByID(distanceId, value, ignoreseekspeed: true)` | каждый кадр (сглаженное значение) | **value = высота камеры над городом, нормированная 0..1**: `height = cam.y − 0` → `Clamp01((height − minCameraDistance) / (maxCameraDistance − minCameraDistance))`, inspector: `minCameraDistance = 8`, `maxCameraDistance = 65`. Прижались к земле = 0 (тише), взлетели = 1 (громче). Пан камеры по карте (WASD) параметр НЕ меняет — только вертикаль. Сглаживание асимметричное: спад `smoothSpeed = 4`, нарастание `smoothSpeedRise = 1.5` (ветер разгоняется плавно) |
| `StudioSystem.getParameterByID(id, out v)` | debug (readback) | подтверждение, что FMOD получил; расхождение → дамп всех глобальных параметров |

Debug: `mouseWheelControlsDistance` — ручное управление колесом (шаг 0.1), по умолчанию ВЫКЛ.

**Шина амбиента:** путь из `AudioCatalog.AmbienceBus`. Инстанс перематывается в ChannelGroup шины после `start()` (у EventInstance нет setBus: `Bus.unlockChannelGroup()` → `getChannelGroup` обоих → `busGroup.addGroup(instGroup)` → `lockChannelGroup()`). `SetMuted(bool)` — `Bus.setMute` одним вызовом; шины нет — Warning + volume-fallback на инстансах.

---

## 8. Городской амбиент — ячейки (AudioZoneManager / AudioCell) и живчик

### 8.1. Контракт FMOD: `event:/AMBIENCE/Ambience_Town`

Один `EventInstance` на ячейку (кластер до `CellCapacity = 4` построек), позиция — центроид кластера. Дерево ивента (LOD-ветки HQ/MQ/LQ на авто-параметре Distance, вложенные TOWN_DESTINY_* с парами IF_*/*_DESTINY) — см. §8.3.

**Параметры, которые ставит код на инстанс ячейки (все — с кэшем отправленных значений, повторы в FMOD не уходят):**

| Параметр | Тип | Диапазон | Метод | Кто и когда ставит |
| :--- | :--- | :--- | :--- | :--- |
| `IF_VILLAGE_SECTORE` | Discrete | 0 / 1 | `Instance.setParameterByName` | `AudioCell.UpdateRTPC()` — есть ли хоть один готовый жилой дом (`House`) в ячейке |
| `VILLAGE_DESTINY` | Discrete | 0..CellCapacity | `Instance.setParameterByName` | там же — сколько готовых жилых домов в ячейке |
| `Distance` (инстансный!) | Continuous | 0..100 | `Instance.setParameterByName(rtpcName, dist/range*100)` | `AudioCell.UpdateDistanceRtpc()` каждый тик видимости — дистанция камера→центроид, нормированная по `distanceRtpcRange = 49.5` (должна равняться Max Distance ивента). Камера стоит — вызова нет |
| `FlyBy` | Continuous | −1..1 | `Instance.setParameterByName(flyByRtpcName, v)` | `AudioCell.UpdateFlyByRtpc()` — ручной доплер: радиальная скорость камеры к ячейке (горизонтальная проекция), `v = clamp(radial / flyByMaxSpeed=30, −1, 1)`; ниже `flyByGateSpeed = 5` м/с — 0 (питч не плывёт при панорамировании). +1 летим на ячейку (питч вверх), −1 удаляемся. Пустое имя параметра = фича выкл. **Параметр обязан быть Game Parameter на самом ивенте (не вложенном)** |

**Авто-параметры ивента (из кода НЕ управляются, readonly):** `Distance` (Normalized 0..1) и `Elevation` — движет FMOD сам по 3D-позиции; на них висит выбор веток LOD (0..0.33 HQ / ..0.67 MQ / дальше LQ). Пары IF_FACTORY/CULTURE/ENERGY_SECTORE + *_DESTINY — задел в FMOD, счётчиков в коде нет (новый класс построек = счётчик в `RecalculateParameters()` + RTPC в `UpdateRTPC()`).

**Прочие вызовы FMOD на инстансе ячейки:**

| Вызов | Когда |
| :--- | :--- |
| `RuntimeManager.CreateInstance(settings.EventReference)` + `start()` | ячейка ожила (в конусе + есть готовые постройки + активность > 0.01); guard-кэш параметров при этом сбрасывается |
| `Instance.set3DAttributes(...)` | только когда реально сдвинулся центроид (позиция, velocity=0, forward +Z, up +Y) |
| `Instance.stop(ALLOWFADEOUT)` + `release()` | мёртвое состояние: вне конуса и вне ближнего радиуса / дальше `zoneDeathDistance=100` / ячейка без готовых построек |

**Слышимость ячейки** (`AudioVisibilityChecker`, горизонтальный конус): в конусе — живая (дальние наравне с ближними); вне конуса, но ближе `nearKeepAliveRadius = 40` (по горизонтали от камеры) — живёт, приглушает FMOD (Event Orientation automation); иначе — мёртвая. Полный зум камеры (`IsFullyZoomedOut`) глушит все. Возрождение с гистерезисом `zoneDeathHysteresis = 5`. Тик менеджера — раз в 2 кадра: FlushDirtyCells → PruneEmptyCells → UpdateVisibilityBatch.

**Активность:** `BuildingAudioSource` (тип `BuildingAudioSourceType`: None/House/Workshop/Market/Infrastructure/Decoration + `activityWeight` 0..1) — сумма весов готовых построек / Capacity = `ActivityLevel` (внутренний метрик оживления, в FMOD НЕ идёт). `CountsForAmbience` (Construction снята) — участвует в амбиенсе; строящееся здание не звучит.

### 8.2. Живчик города — ZoneAmbienceSfxScheduler

One-Shot `SfxRandomEventReference` (`event:/AMBIENCE/Ambience_Town_SFX_Random`, банк общий с амбиентом). Независимый бросок на каждую **активную** ячейку в тик (`chance = 0.25`, `tick = 2.5 c`), персональный кулдаун ячейки с разбросом (`cooldown = 15 c`, `jitter = 0.5`), потолок параллельности `maxConcurrent = 4`. Точка звука — случайная готовая постройка + разброс `jitterRadius = 1.5` м. Вызовы FMOD: `CreateInstance` + `set3DAttributes` + `start()`; освобождение по `getPlaybackState() == STOPPED` (страховка от утечки: force-release через длину ивента + margin, ивенты длинные — до 72 c).

### 8.3. Стройка — BuildingConstructionAudio

**Параметр `BUILDING`** (в FMOD у имени хвостовой пробел `"BUILDING "` — резолв по ID через Trim): **labeled Game Parameter на ивенте** `event:/BUILD`, значения:

| Значение | Лейбл | Когда ставит код |
| :--- | :--- | :--- |
| 0 | `NONE` | демонтаж / стройка снята / COMPLETE доиграл |
| 1 | `PLACEMENT` | чертёж поставлен, рабочие ещё не пришли (`Construction.Elapsed = 0`) — ваншот |
| 2 | `BUILD` | рабочие на площадке (`Elapsed > 0`) — держится до конца стройки |
| 3 | `COMPLETE` | `Construction` снята — ваншот; при загрузке снапшота готового здания НЕ играется (первый синк) |

Вызовы: `CreateInstance` (лениво, при видимой ячейке) → резолв `PARAMETER_ID` (`getParameterDescriptionByIndex` + Trim + проверка READONLY) → `AttachInstanceToGameObject` (следует за зданием) → `setParameterByID(id, (float)state, ignoreseekspeed: true)` + `start()`. Гейтинг: ячейка постройки (`LinkedCell`) невидима — `stop(ALLOWFADEOUT)` + `release()` (ваншоты в невидимой зоне не доигрываются, BUILD возобновляется при возврате).

---

## 8.4. Полный профиль параметров FMOD-проекта (профайлинг метаданных + ответы Валентина)

Снято напрямую с `Metadata/Event/*.xml` + `ParameterPreset/*.xml` (цепочка: Event → `parameters` → ParameterProxy → GameParameter). Параметры делятся на **внешние** (дергает Unity-код) и **внутренние** (живут только внутри FMOD: автоматические, цепи вероятностей генеративной музыки, оптимизационные переключатели). Внутренние — часть механики оптимизации, не мусор, не чистить.

### 8.4.1. Глобальные параметры (видны всем ивентам; код — через `StudioSystem`)

| Параметр | Тип | Диапазон | Назначение (✅ подтверждено) | Владелец |
| :--- | :--- | :--- | :--- | :--- |
| `MUSIC_STATES ` (с пробелом) | Labeled | 0..6 | Стейт-машина музыки. **Используются только 0..2**: `MUSIC_IDLE+STRESS` / `WIN` / `DEFEAT`; `Value E/F/G` и 6 — заглушки, назвать позже | Код: MusicDirector (лейбл кия через `setParameterByNameWithLabel`) |
| `Distance ` (с пробелом) | Continuous | 0..1 | Высота камеры → громкость ветра. **Только ветер, других владельцев нет** | Код: WindAmbience (по ID) |
| `STRESS` | Continuous | 0..N | **Напряжение города** — читают VILLAGE_HQ (слой амбиента), MUSIC_STATES + ADAPTIVE_GENERATIVE MUSIC. Код должен ставить из симуляции (мораль/опасность) — точка интеграции TODO | Код (симуляция) |
| `DAY-NIGHT` | Continuous | — | **Игровые сутки** для амбиента ветра (ночь/день). Код должен ставить по таймлайну симуляции — точка интеграции TODO | Код (таймлайн) |
| `ENERGY` | Continuous | — | Задел на будущее (энергия Пирамиды в музыку), **пока не используется** | — |

### 8.4.2. Ивенты: полная карта параметров

| Ивент | Параметр | Тип | Диапазон/лейблы | Класс / кто ставит |
| :--- | :--- | :--- | :--- | :--- |
| **Ambience_Town** (`event:/AMBIENCE/TOWN_DESTINY/Ambience_Town`) | `Distance` | 10 — АВТО | 0..1 | внутренний: LOD-ветки HQ/MQ/LQ. Отправка 0..100 из кода в него игнорируется (см. §8.4.3) |
| | `Elevation` | 5 — авто | −90..90 | внутренний |
| | `Event Orientation` | 7 — авто | −180..180 | внутренний: приглушение вне конуса |
| | `IF_VILLAGE/FACTORY/CULTURE/ENERGY_SECTORE`, `VILLAGE/FACTORY/CULTURE/ENERGY_DESTINY`, `STRESS` | 1 — user | 0..2 / 0..6 | ✅ **внешние через рекурсивную экспозицию**: объявлены на вложенных, но проброшены в банк родителя — код ставит их на инстанс Ambience_Town (§8.4.3) |
| | `FlyBy` | 0 — user | −1..1 | ✅ внешний: AudioCell.UpdateFlyByRtpc (доплер пролёта) |
| **TOWN_DESTINY_HQ / _MQ** | `IF_VILLAGE/FACTORY/CULTURE/ENERGY_SECTORE` | 1 — user | 0..2 | внутренние (форсируются инструментами сверху) |
| **VILLAGE_HQ / _MQ / _LQ** | `IF_VILLAGE_SECTORE` | 1 | 0..2 | внутренние |
| | `VILLAGE_DESTINY` | 1 | 0..6 | внутренние (сколько жилых домов — шлёт Unity через верхний ивент) |
| **FACTORY_HQ / CULTURE_HQ / ENERGY_HQ** | `*_DESTINY` | 1 | 0..6 | внутренние (задел под классы построек) |
| **HQ / MQ / LQ, TOWN_DESTINY_LQ, Nested Event 2/3/4, main_soundtrack** | — | — | — | без параметров, полностью внутренние — **код никогда не трогает** |
| **Ambience_Wind_Generation** | `Distance ` (глоб.) | 0 | 0..1 | ✅ внешний: высота камеры |
| | `DAY-NIGHT` (глоб.) | 0 | — | внешний: сутки, интеграция TODO |
| | `WEATHER` | Labeled | 0..2 | внешний: погода — код переключает (буря/катаклизмы). Лейбл `DEAFULT` → поправить на `DEFAULT`, `Value C` назвать по третьему состоянию |
| | `Wheel_Scroll` | 0 | — | локальный, **оставить** (не используется кодом, резерв) |
| **Ambience_Town_SFX_Random** | `Distance` | 10 — авто | — | внутренний |
| **BUILD** | `BUILDING ` (с пробелом) | Labeled | 0..4: NONE/PLACEMENT/BUILD/COMPLETE | ✅ внешний: BuildingConstructionAudio (по ID) |
| | `Distance` | 10 — авто | — | внутренний |
| **UI** | `UI_States` | Labeled | 0..7 | ✅ внешний: UiAudio (по ID). **Используются 0..5** (NONE/HOVER/YES/NO/WARNING/CLICK), 6–7 — резерв, лейблы задать позже |
| **MUSIC_STATES** | `MUSIC_STATES ` (глоб.) | Labeled | 0..6 | ✅ внешний: лейбл кия |
| | `inside global` | Discrete | 0..3 | **внутренний**: переключатель вероятностных цепей — при переходе между стейтами отрубает неиграющие вложенные ивенты, чтобы в итоге играл **один** вложенный ивент (оптимизация) |
| | `STRESS` (глоб.) | 0 | — | внешний (напряжение города), интеграция TODO |
| | `stress timer` | Continuous | — | **внутренний**: наоборот — STRESS гоняет stress timer, тот копится и переводит музыку на **суперстрессовый стейт**. Код не трогает |
| **ADAPTIVE_GENERATIVE MUSIC** | `STRESS`, `ENERGY` (глоб.) | 0 | — | STRESS — внешний (TODO), ENERGY — задел |
| | `ins` | Discrete | 0..6 | **внутренний**: селектор инструмента/слоя генерации (вероятности FMOD) |
| **click** | `type click ` | Labeled | 0..3: none/tree/build/buildings | **внешний**: инспект-клик ПО ТИПУ UI, на что нажал игрок — дерево/постройку/технологии. Код будет ставить по источнику клика (интеграция TODO) |

### 8.4.3. Проброс параметров вложенных ивентов — КАК ЭТО РАБОТАЕТ (проверено, правки не нужны)

Параметры, объявленные на **вложенных** ивентах (TOWN_DESTINY_*, VILLAGE_*…), **автоматически экспонируются в список параметров родительского ивента в собранном банке** (`isExposedRecursively` у Game Parameter по умолчанию включён; в XML-метаданных флаг не пишется, когда стоит дефолт — поэтому Studio-метаданные верхнего ивента «не знают» про эти параметры, а банк знает). Поэтому `Instance.setParameterByName("IF_VILLAGE_SECTORE", …)` на инстансе верхнего `Ambience_Town` **работает**: значение уходит вниз во вложенные инстансы. Подтверждено двумя независимыми проверками:

1. **`FMODStudioCache.asset`** (интеграция строит его из банков): у `event:/AMBIENCE/TOWN_DESTINY/Ambience_Town` в банке **10 параметров**: `IF_VILLAGE_SECTORE`, `IF_FACTORY_SECTORE`, `IF_CULTURE_SECTORE`, `IF_ENERGY_SECTORE`, `VILLAGE_DESTINY`, `FACTORY_DESTINY`, `CULTURE_DESTINY`, `ENERGY_DESTINY`, **`STRESS`** (тоже проброшен! можно ставить на инстанс ячейки, не только глобально), `FlyBy`. Авто-параметры (Distance/Elevation/Event Orientation) из кэша выброшены (readonly-фильтр интеграции), но в банке живут и кормят automation.
2. **Слуховой тест Валентина**: 1/2/3/4 дома в ячейке — плотность VILLAGE_DESTINY слышимо меняет слои. Работает.

Следствия для спеки и кода:

- **Ничего не вешать на родительский ивент вручную не нужно** — проброс автоматический (работает и для будущих FACTORY/CULTURE/ENERGY: просто счётчик в коде + setParameterByName, как VILLAGE).
- **`STRESS` доступен на инстансе Ambience_Town** — когда появиться интеграция «напряжение города», можно ставить прямо в ячейки (локальное напряжение района), а глобально — через StudioSystem (музыка).
- **Отправка `Distance` 0..100 из `AudioCell.UpdateDistanceRtpc` уходит в readonly авто-параметр и игнорируется** — слышимый LOD делает сам FMOD по позиции слушателя. Это не баг (звук правильный), просто мёртвый вызов; можно убрать как чистку кода, звучание не изменится. Гизмо-строка `dist=` при этом показывает ридбэк-дефолт — так и должно быть у авто-параметров.
- Реальный путь ивента: `event:/AMBIENCE/TOWN_DESTINY/Ambience_Town` (папка AMBIENCE → подпапка TOWN_DESTINY). Коду пути не важны — везде `EventReference` по GUID.
3. **FMOD Studio (гигиена, не горит):** переименовать `DEAFULT` → `DEFAULT` (WEATHER), задать лейблы UI_States 0..5, назвать `Value E/F/G` MUSIC_STATES когда определятся стейты.
4. **Точки будущей интеграции кода:** `STRESS` (напряжение города → амбиент+музыка), `DAY-NIGHT` (сутки → ветер), `WEATHER` (погода/буря → ветер), `type click` (инспект-клик по типу UI).
5. Висящие ссылки на удалённые параметры в reject/confirm/impact/complete/click — следы вложенных ивентов UI (remote-automation), не мешают, не трогаем.

---

## 9. MVP-цель: целевой звуковой контракт демки

### 9.1. Категории зданий ↔ аудио-классы

| Категория GDD | BuildingAudioSourceType | Пара RTPC в Ambience_Town | Статус |
| :--- | :--- | :--- | :--- |
| Жилые (Residential) | `House` | `IF_VILLAGE_SECTORE` / `VILLAGE_DESTINY` | ✅ код |
| Добыча/Индустрия | `Workshop` | `IF_FACTORY_SECTORE` / `FACTORY_DESTINY` | FMOD готов, счётчик TODO |
| Продовольствие | `Workshop`/новый | `IF_CULTURE_SECTORE` / `CULTURE_DESTINY` | FMOD готов, счётчик TODO |
| Наука | `Workshop` | `IF_ENERGY_SECTORE` / `ENERGY_DESTINY` | FMOD готов, счётчик TODO |
| Ритуалы/Энергия | `Infrastructure`/новый | + параметры §9.3 | частично |
| Инфраструктура | `Infrastructure` | приглушённый вклад в слой | частично |

Правило пары: `IF_<CLASS>_SECTORE` (дискретный флаг 0..2) + `<CLASS>_DESTINY` (плотность 0..Capacity). Work Loop построек — НЕ инстанс на здание: плотность выражается слоями амбиента; индивидуальные лупы только уникальным сюжетным объектам (Пирамида, Алтари, Копи, Врата).

### 9.2. Пять звуковых состояний постройки

| Состояние | Реализация |
| :--- | :--- |
| Build_Start/Loop/Done | ✅ `BuildingConstructionAudio` (PLACEMENT/BUILD/COMPLETE) |
| Work_Loop | через DESTINY-слои; целевой `WorkIntensity` на уровне ячейки/уникальных зданий |
| Idle_Blocked | TODO: остановка при ночи/дефиците/засухе |
| UI_Inspect_Select | TODO: 6 вариаций по категории (сейчас общий CLICK) |
| Damage_Hit/Demolish | TODO: One-Shot; тембр удара бури — по `PyramidDomeActive` |

### 9.3. Целевые параметры (контракт MVP)

| Параметр | Тип | Диапазон | За что отвечает |
| :--- | :--- | :--- | :--- |
| `BuildingCategory` | Labeled | 0 Residential … 5 Infra | тембр инспект-клика и разрушения |
| `WorkIntensity` | Continuous | 0 (простой/ночь) … 1 (полная смена) | громкость/плотность рабочего лупа |
| `MineHorizon` | Discrete | 1 (поверхность) / 2 (глубокий) | крепи, гул, газ на 2-м горизонте |
| `PyramidWaterActive` | Boolean | 0/1 (`IsIrrigated`) | насосы + вода в каналах; обрыв — вздох турбин |
| `PyramidDomeActive` | Boolean | 0/1 | гул купола; удары бури = рикошет о щит |
| `EraTension` | Continuous | 0 (передышка) … 2 (катаклизм) | плотность вулканического ветра, подложка эры |
| `Sacrifice_Delivered` | One-Shot событие | — | ритуальный удар + «вдох» ядра при сдаче дани |

Все новые — Game Parameter (XML 0/1/2), чистые имена без пробелов. Ключевые One-Shot: доставка дани, обвал шахты, смена эры, удар метеора, катсцена Колосса (режиссура — `ColossusCutsceneDirector`, звук — сценный ивент/серия One-Shot; музыка приглушается снэпшотом).

**Снэпшоты за Эрами/бурями** — по образцу ESC: инстанс снэпшота от `AudioSnapshots`-подобного владельца, старт/стоп с фейдом.

---

## 10. Микшер-план (4 шины)

`bus:/Music`, `bus:/Ambience`, `bus:/SFX`, `bus:/UI` (дети Master; VCA не заводить — 4 шины закрываются `bus.setVolume()`). Развеска: музыка → Music; Ambience_Town, SFX_Random, Wind → Ambience; BUILD и мировые One-Shot → SFX; UI-ивент → UI. У вложенных ивентов музыки Output не переопределять (иначе мимо Music-шины и снэпшота). Снэпшот ESC: Music down + lowpass, Ambience/SFX down, UI не трогать. После шин: Ctrl+B → скопировать ВСЕ банки в `StreamingAssets/Desktop/`. Слайдеры: `RuntimeManager.GetBus("bus:/Music").setVolume(v)` + PlayerPrefs.

---

## 11. FMOD API 2.03 — справка (проверено на проекте)

| Действие | ✅ Правильно | ❌ Неправильно |
| :--- | :--- | :--- |
| Проверка инстанса | `instance.isValid()` | `== null` (структура) |
| Сброс | `instance = default`, `clearHandle()` | `= null` |
| Параметр по имени | `setParameterByName(name, v)` | `setParameter`, `setParameterFloat` (нет) |
| Параметр по лейблу | `setParameterByNameWithLabel(name, label)` | — |
| Мгновенная установка | `setParameterByID(id, v, ignoreseekspeed: true)` | без ignoreseekspeed (ползёт) |
| Резолв параметра | `description.getParameterDescriptionByName/ByIndex` + `Trim()`-матч + проверка `PARAMETER_FLAGS.READONLY` | слепой матч по имени (хвостовые пробелы) |
| Остановка | `FMOD.Studio.STOP_MODE.ALLOWFADEOUT` (полный путь) | `STOP_MODE.*` без префикса |
| Состояние | `getPlaybackState(out ...)` | — |
| Шина | `getBus(path)`, `setMute`, `setVolume`; реparentинг инстанса — `unlockChannelGroup`/`getChannelGroup`/`addGroup`/`lockChannelGroup` | `instance.setBus` (нет метода) |
| Слушатель | `StudioSystem.getListenerAttributes(0, out ...)` → `instance.set3DAttributes` | — |
| Имя банка | `bank.getPath()` → `bank:/Имя` | `bank.getName()` (нет) |
| Банки ивента | `StudioSystem.getBankList` → `bank.getEventList` | `eventDescription.getBankList()` (CS1061) |
| Имя параметра (desc) | `(string)desc.name` | печать `desc.name` (StringWrapper) |
| Глобальный параметр | резолв `PARAMETER_ID` (Trim + OrdinalIgnoreCase) → `StudioSystem.setParameterByID(id, v, true)` | слепой `StudioSystem.setParameterByName` |
| Глобальный vs инстансный | глобальный — через `StudioSystem`, инстансный — через `instance` | смешивать (scoped-двойники имён) |

---

## 12. Диагностика

1. Путь ивента — `Metadata/Event/*.xml` + `Metadata/EventFolder/*.xml` (строки в .bank сжаты).
2. Управляемость параметра — `Metadata/ParameterPreset/*.xml`: `parameterType` (0/1/2 = user, ≥10 = readonly-авто) + `isExposedRecursively`. **Шкала XML ≠ API-enum.**
3. Live Update: FMOD Settings (per-platform) → порт 9264 → Play → Window → Connect (F5) → реальные значения RTPC на инстансах. Debug Overlay — оверлей в Game View.
4. «У него варнинг, у меня нет» при одном коде = РАЗНЫЕ БАНКИ: сравнить `LastWriteTime` банков с метаданными.
5. Гизмо ячеек (`ShowZoneLabels`) уже показывает sent/readback RTPC и LOD-ветку — первая линия диагностики амбиента.
6. Порядок правки: FMOD Studio → Ctrl+B → скопировать ВСЕ банки в `StreamingAssets/Desktop/` → Play (+ `ReloadInEditor` при необходимости).

---

## 13. Бэклог аудио

- [ ] Микшер: 4 шины + снэпшот ESC по шинам (§10), затем слайдеры громкости.
- [ ] MUSIC_STATES → 2D в FMOD (убрать Spatialiser у вложенных тоже) → убрать `FollowListener`.
- [ ] Live Update: доходят ли `IF_*/*_DESTINY` до вложенных ивентов.
- [ ] Счётчики FACTORY / CULTURE / ENERGY в `AudioCell.RecalculateParameters()`.
- [ ] MVP-параметры §9.3 + One-Shot'ы (дань, обвал, эра, буря, Колосс).
- [ ] `UI_Inspect_Select` — 6 вариаций по BuildingCategory.
- [ ] `ActivityLevel` ячейки → RTPC (сейчас внутренний метрик, в FMOD не идёт).
