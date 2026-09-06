# Аудио-система — полная карта диагностики

**Дата:** 2026-09-06  
**Статус:** 🔴 КРИТИЧЕСКИЕ ПРОБЛЕМЫ — звук не работает

---

## 📌 ОБЗОР СИСТЕМЫ

### Архитектура
```
┌─────────────────────────────────────────────────────────────┐
│                      FMOD Studio                             │
│  Ивенты: Ambience_Town, Ambience_Wind_Generation, Main_Theme │
│  Параметры: Distance (Game Parameter, 0-100)                 │
│  Банки: *.bank → Build/Desktop/                              │
└──────────────────────┬──────────────────────────────────────┘
                       │ Ctrl+B → копировать .bank
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              StreamingAssets/Desktop/                         │
│  - Ambience_Town.bank                                       │
│  - Ambience_Wind_Generator.bank                              │
│  - Main_theme.bank                                          │
│  - Master.bank + Master.strings.bank                         │
└──────────────────────┬──────────────────────────────────────┘
                       │ RuntimeManager.LoadBank()
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Unity (Bootstrap-сцена)                         │
│  ┌─────────────────────┐  ┌──────────────────────────────┐  │
│  │ AudioZoneManager    │  │ GlobalAmbienceManager        │  │
│  │ - 120 зон (локальные)│  │ - список глобальных ивентов  │  │
│  │ - видимость камеры  │  │ - Death Distance (0=всегда) │  │
│  │ - Distance RTPC     │  │ - 3D следует за камерой      │  │
│  └─────────┬───────────┘  └──────────────────────────────┘  │
│            │                                                  │
│            ▼                                                  │
│  ┌─────────────────────┐  ┌──────────────────────────────┐  │
│  │ AudioZoneSettings   │  │ FmodBankLoader               │  │
│  │ - EventReference    │  │ - LoadBanksForEvent()        │  │
│  │ - Zone Death Dist   │  │ - UnloadEventBanks()         │  │
│  │ - Hysteresis        │  │                              │  │
│  └─────────────────────┘  └──────────────────────────────┘  │
│                                                               │
│  ┌─────────────────────┐  ┌──────────────────────────────┐  │
│  │ AudioVisibility     │  │ BuildingViewBoard            │  │
│  │ Checker             │  │ - RegisterAudioSource()      │  │
│  │ - UpdateVisibility  │  │ - FindZoneNear()             │  │
│  └─────────────────────┘  └──────────────────────────────┘  │
│                                                               │
│  ┌─────────────────────┐  ┌──────────────────────────────┐  │
│  │ BuildingAudioSource │  │ AudioZone                    │  │
│  │ - на каждом здании  │  │ - FMOD EventInstance         │  │
│  │ - тип + activity    │  │ - RTPC (Distance, Houses...) │  │
│  │ - Add/RemoveSource  │  │ - UpdateDistanceDeath()      │  │
│  └─────────────────────┘  └──────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔴 КРИТИЧЕСКИЕ ПРОБЛЕМЫ (требуют немедленного решения)

### Проблема 1: Параметр Distance — не Game Parameter
**Статус:** 🔴 КРИТИЧЕСКОЕ  
**Симптом:** `setParameterByName("Distance", value)` из кода молча не работает, значение всегда = 0, профиль FMOD пустой.

**Причина:** В метаданных FMOD-проекта параметр имеет:
- `parameterType: 3` = `AUTOMATIC_EVENT_ORIENTATION` (автоматический, не game-controlled)
- `isExposedRecursively: false` (не экспонирован в API)

**Где проверить:**
```
Audio_FMOD/they will desend/Metadata/ParameterPreset/{f6aa37c3-25f3-4f95-82b6-58f07d25c18f}.xml
```
Ищи:
- `<property name="parameterType">3</property>` → **должно быть 0** (GAME_CONTROLLED)
- `<property name="isExposedRecursively"><value>false</value></property>` → **должно быть true**

**Что сделать в FMOD Studio:**
1. Открыть ивент → панель Parameters (слева внизу, под таймлайном)
2. **Удалить** текущий параметр (автоматический, переименованный в Distance)
3. **+ → Add Parameter → Game Parameter** → имя `Distance`, диапазон **0–100**
4. **Перепривязать автоматизации** громкости/фильтров на новый параметр
5. **Ctrl+B** (сборка банка)
6. **Скопировать .bank в StreamingAssets** (см. Проблема 2)

**Важно:** параметр `Distance` используется в **обоих ивентах** (Ambience_Town и Ambience_Wind_Generation). Проверить оба.

---

### Проблема 2: Банки не копируются автоматически
**Статус:** 🔴 КРИТИЧЕСКОЕ  
**Симптом:** FMOD Studio собрал банк, но Unity не видит изменений → `BankLoadException`, `event not found`.

**Почему:** FMOD Studio **НЕ копирует** банки автоматически после Ctrl+B.

**Где банки должны быть:**
- Сборка: `Audio_FMOD/they will desend/Build/Desktop/`
- Unity: `TheyWillDescend/Assets/StreamingAssets/Desktop/`

**Что делать:**
```powershell
Copy-Item "Audio_FMOD\they will desend\Build\Desktop\*.bank" "TheyWillDescend\Assets\StreamingAssets\Desktop\" -Force
```

**Проверка:**
```powershell
# Проверить, что банки есть в обоих местах
Get-ChildItem "Audio_FMOD\they will desend\Build\Desktop\*.bank" | Select-Object Name
Get-ChildItem "TheyWillDescend\Assets\StreamingAssets\Desktop\*.bank" | Select-Object Name
# Списки должны совпадать
```

**Важно:** после закрытия Unity → Ctrl+B в FMOD → скопировать → открыть Unity заново.

---

### Проблема 3: Имя параметра не совпадает
**Статус:** 🟡 ПРОВЕРИТЬ  
**Симптом:** Код пишет `"Distance"`, но в FMOD параметр называется иначе.

**Где проверяется:**
- Код: `AudioZone.UpdateRTPC()`, `GlobalAmbienceManager.Update()`
- Имя: `"Distance"` (регистр важен!)

**Что сделать в FMOD Studio:**
- Параметр называется ровно `Distance` (без пробелов, без символов)
- Проверить в Event Browser: параметр должен быть виден

---

## 🔍 ПОШАГОВАЯ ДИАГНОСТИКА (по порядку)

### Шаг 1: Проверить банки в StreamingAssets
```powershell
Get-ChildItem "TheyWillDescend\Assets\StreamingAssets\Desktop\*.bank" | Select-Object Name
```
**Ожидаем:**
- Master.bank
- Master.strings.bank
- Ambience_Town.bank
- Ambience_Wind_Generator.bank
- Main_theme.bank

**Если чего-то нет** → скопировать из `Audio_FMOD/they will desend/Build/Desktop/`

---

### Шаг 2: Проверить тип параметра в FMOD Studio
1. Открыть `Ambience_Town` в FMOD Studio
2. Панель Parameters → нажать на параметр Distance
3. В инспекторе справа проверить:
   - **Type:** `Game Controlled` (НЕ Automatic)
   - **Exposed:** галка включена
   - **Min/Max:** 0 / 100

**Повторить для `Ambience_Wind_Generation`**

---

### Шаг 3: Проверить автоматизацию
1. В FMOD Studio открыть ивент
2. На панели параметров нажать правой кнопкой на Distance → **Assign to Parameter**
3. Перетащить на громкость дорожки (Fader)
4. Проверить, что автоматизация появилась на таймлайне

**Для ветра:** Distance 0 = у города → тише, Distance 100 = 120м+ → громче  
**Для города:** наоборот — Distance 0 = пусто, Distance 100 = полная активность

---

### Шаг 4: Проверить сборку и копирование
1. **Закрыть Unity** (обязательно, иначе файл заблокирован)
2. В FMOD Studio: Ctrl+B
3. Если ошибка "file in use" → Unity всё ещё открыта, закрыть полностью
4. Скопировать банки в StreamingAssets (см. Шаг 1)
5. Открыть Unity заново

---

### Шаг 5: Проверить в Unity
1. Открыть Bootstrap-сцену
2. Выбрать **AudioZoneManager**:
   - Settings: `New Audio Zone Settings.asset`
   - Fmod Banks: `Ambience_Town`
   - В консоли: `FmodBankLoader: bank 'Ambience_Town' loaded`
3. Выбрать **GlobalAmbienceManager**:
   - Events: список ивентов
   - У каждого: EventReference заполнен, distanceRtpc = `Distance`, deathDistance = 0 (для ветра)
   - В консоли: `FmodBankLoader: bank 'Ambience_Wind_Generator' loaded`

---

### Шаг 6: Проверить в FMOD Profiler
1. В Unity: **Play**
2. В FMOD Studio: **Window → Connect** (F5) → выбрать игру → Connect
3. **Window → Profiler**
4. Построить здание → посмотреть:
   - Появился ли инстанс `Ambience_Town`?
   - Двигается ли параметр Distance?
   - Сколько голосов звучит?

**Если инстанса нет** → проверить логи:
- `FmodBankLoader: bank 'X' loaded` → банк загружен
- `[AudioZone] ACTIVATED` → зона активирована
- `global event ACTIVATED` → глобальный ивент запущен

---

### Шаг 7: Проверить параметры в коде
Если банки загружены, инстанс создан, но параметр не двигается:
1. Открыть `AudioZone.cs` → `UpdateRTPC()`
2. Убедиться, что `Instance.isValid()` возвращает true
3. Проверить, что `setParameterByName("Distance", value)` не возвращает ошибку
4. В FMOD Profiler посмотреть текущее значение Distance

---

## 🛠 КОД — ГДЕ ЧТО НАХОДИТСЯ

### Файлы и их ответственность

| Файл | Что делает | Где вызывается |
|------|-----------|---------------|
| `AudioZoneManager.cs` | Создаёт 120 зон, тик видимости, Distance-куллинг | Bootstrap-сцена |
| `AudioZone.cs` | Зона: FMOD-инстанс, RTPC, 3D-атрибуты, смерть по дистанции | Вызывается менеджером |
| `AudioVisibilityChecker.cs` | Проверка видимости зон (угол камеры + дистанция) | В составе AudioZoneManager |
| `AudioZoneSettings.cs` | ScriptableObject: настройки зон, EventReference | Ссылается из менеджера |
| `FmodBankLoader.cs` | Загрузка/выгрузка банков, автоопределение по EventReference | Вызывается менеджером |
| `BuildingViewBoard.cs` | Регистрация построек в зонах, авто-поиск менеджера | Game-сцена |
| `BuildingAudioSource.cs` | Компонент на здании: тип, активность, регистрация | Префабы зданий |
| `GlobalAmbienceManager.cs` | Глобальные ивенты (ветер): один инстанс, Death Distance | Bootstrap-сцена |

### Поток данных
```
1. Bootstrap-сцена загружается
   ↓
2. AudioZoneManager.Awake() → проверяет настройки
   ↓
3. AudioZoneManager.BuildZones() → создаёт 120 зон
   ├─ FmodBankLoader.LoadBanksForEvent(EventReference) → банки грузятся автоматически
   └─ Для каждого банка: RuntimeManager.LoadBank(bankName)
   ↓
4. GlobalAmbienceManager.Start() → загружает банки глобальных ивентов
   ↓
5. Каждый тик (каждые 2 кадра):
   ├─ AudioVisibilityChecker.UpdateVisibility() → какие зоны видны
   ├─ AudioVisibilityChecker.ApplyVisibility() → SetActive(true/false)
   ├─ AudioZone.UpdateDistanceDeath() → смерть по дистанции
   └─ AudioZone.UpdateDistanceRtpc() → Distance-RTPC для активных зон
   ↓
6. Постройка здания (BuildingViewBoard):
   ├─ RegisterAudioSource() → ищет ближайшую зону
   ├─ zone.AddAudioSource(source) → регистрирует в зоне
   └─ zone.SetActive(true) → если зона видна → звук появляется
```

---

## ❌ ЧАСТЫЕ ОШИБКИ

### 1. Параметр автоматический, не game-controlled
**Симптом:** `setParameterByName` молча не работает, значение всегда 0  
**Решение:** В FMOD Studio создать **Game Parameter** (не Automatic)

### 2. Параметр не экспонирован
**Симптом:** параметр не виден в Event Browser, `setParameterByName` не работает  
**Решение:** В FMOD Studio включить галку **Exposed** у параметра

### 3. Банки не скопированы
**Симптом:** `BankLoadException`, `event not found` в консоли  
**Решение:** Ctrl+B в FMOD Studio → скопировать .bank в StreamingAssets → перезапустить Unity

### 4. Автоматизация не перепривязана
**Симптим:** параметр есть, но громкость не меняется  
**Решение:** В FMOD Studio перетащить параметр на громкость дорожки, создать кривую автоматизации

### 5. Имя параметра не совпадает
**Симптом:** код пишет `"Distance"`, а в FMOD параметр называется иначе  
**Решение:** Имя должно быть ровно `"Distance"` (регистр важен!)

### 6. Зона звучит до постройки здания
**Симптом:** звук появляется, когда здания ещё нет  
**Решение:** Проверить `activeInHierarchy` в `RelinkExistingSources`, `ActivityLevel > 0.01f` в `SetActive()`

### 7. Ветер умирает при отдалении
**Симптом:** при максимальном отдалении ветра нет  
**Решение:** В GlobalAmbienceManager: **Death Distance = 0** (смерть отключена, звук всегда)

---

## 📊 ЧЕК-ЛИСТ ФИНАЛЬНОЙ ПРОВЕРКИ

- [ ] Банки в StreamingAssets: Master, Ambience_Town, Ambience_Wind_Generator, Main_theme
- [ ] Параметр Distance в обоих ивентах: **Game Parameter**, **Exposed**, **0–100**
- [ ] Автоматизация громкости перепривязана на Distance в обоих ивентах
- [ ] В Bootstrap-сцене: AudioZoneManager настроен, GlobalAmbienceManager настроен
- [ ] В консоли при запуске: `bank 'Ambience_Town' loaded`, `bank 'Ambience_Wind_Generator' loaded`
- [ ] В FMOD Profiler: инстансы появляются при постройке зданий
- [ ] В FMOD Profiler: параметр Distance двигается при движении камеры
- [ ] В FMOD Profiler: голоса звучат (не пустой профиль)
- [ ] Ветер звучит всегда (Death Distance = 0)
- [ ] Звук города не звучит до постройки здания

---

## 🔧 КОМАНДЫ ДЛЯ ПРОВЕРКИ

### Проверить банки в проекте
```powershell
# Банки в FMOD Studio
Get-ChildItem "Audio_FMOD\they will desend\Build\Desktop\*.bank" | Select-Object Name

# Банки в Unity
Get-ChildItem "TheyWillDescend\Assets\StreamingAssets\Desktop\*.bank" | Select-Object Name

# Должны совпадать!
```

### Проверить тип параметра
```powershell
# Открыть XML параметра в FMOD проекте
Get-Content "Audio_FMOD\they will desend\Metadata\ParameterPreset\*.xml" | Select-String "GameParameter|parameterType|isExposedRecursively"
```
**Ожидаем:** `<property name="parameterType"><value>0</value></property>` (0 = GameControlled)  
**Не ожидаем:** `<value>3</value>` (3 = AutomaticEventOrientation)

### Проверить путь ивента
```powershell
# Имя ивента в XML
Select-String -Path "Audio_FMOD\they will desend\Metadata\Event\*.xml" -Pattern "<value>Ambience_Town</value>"
```
**Ожидаем:** `Ambience_Town` (регистр важен!)

---

## 📝 ПРИМЕЧАНИЯ

### Почему параметр не работает (глубокая причина)
FMOD 2.03 имеет два типа параметров:
1. **Game Controlled (0)** — управляется из кода через `setParameterByName`
2. **Automatic (1–9)** — FMOD сам считает (расстояние, ориентация, скорость и т.д.)

Когда параметр создан как **Automatic** и имеет `isExposedRecursively: false`:
- FMOD не даёт доступ к нему из кода
- `setParameterByName` молча игнорируется
- Значение всегда = `initialValue` (обычно 0)
- Если громкость автоматизирована от этого параметра — она стоит в нуле → тишина

**Решение:** пересоздать параметр как **Game Parameter** — тогда FMOD даст доступ из кода, и значение будет управляться.

### Почему банки нужно копировать вручную
FMOD Studio и Unity — два отдельных приложения. FMOD Studio собирает банки в свою папку (`Build/Desktop/`), но не копирует их в Unity-проект. Unity читает банки из `StreamingAssets/Desktop/` при старте. Поэтому после каждого изменения в FMOD Studio нужно:
1. Закрыть Unity (иначе файл заблокирован)
2. Ctrl+B в FMOD Studio (сборка)
3. Скопировать `.bank` в `StreamingAssets/Desktop/`
4. Открыть Unity заново

---

**Последнее обновление:** 2026-09-06  
**Автор:** Koda AI (на основе диагностики проекта)
