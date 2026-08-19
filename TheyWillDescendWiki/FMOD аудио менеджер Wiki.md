---
tags:
  - audio
  - architecture
  - manager
  - fmod
aliases:
  - Аудио FMOD
  - FMOD Audio
  - FMOD Аудио система
---

# FMOD Аудио система

← [[Home|Главная]] | [[Архитектура/Индекс архитектуры|Архитектура]] | [[Архитектура/DI и LifetimeScope|DI]]

> [!date] Обновлено
> **16.08.2026** — FMOD-версия (`FMODUnity.RuntimeManager`, `FMOD.Studio.System`), заглушка для `MatchStartedEvent`, трек паузы (`_pauseSource`), раздельный fade in/out, `stopOnPause`, точный таймер fade.

---

## Краткая выжимка

Геймплей **не вызывает звук напрямую**. `AudioService` слушает `IGameEventBus` и передаёт **FMOD Event Path** в `IAudioManager`. UI и кнопки могут вызывать `IAudioManager.Play(...)` напрямую.

```
BallMotion → bus.Publish(BallContactEvent)
                    ↓
              AudioService (App scope) → Play("event:/SFX/Gameplay/BallHit")
                    ↓
              FMODAudioManager (Root scene, config: FMODAudioCatalog)
                    ↓
              Music / SfxPool / UiPool → FMOD Buses
```

---

## Слои

| Слой | Класс | Где | Задача |
|------|-------|-----|--------|
| CMS | `FMODAudioCatalog` + `FMODSoundDefinition` | asset в Inspector менеджера | Event Path, Bus, priority, cooldown, pitch, volume, loop |
| Маппинг | `AudioService` | App scope (DI) | Подписки на шину → FMOD Event Path |
| Unity | `FMODAudioManager` | Root.unity | Конфиг, cooldown, overlap, пул, музыка, fade |

**Один менеджер** на всю игру. Контекст (меню / пауза / поле) — через `NavigationChangedEvent`.

---

## Каналы воспроизведения

| Канал | FMOD-механика | Для чего |
|-------|---------------|----------|
| `Music` | 1 `EventInstance` | Loop, fade in/out, fade pause/resume |
| `Pause` | 1 `EventInstance` (`_pauseSource`) | Трек паузы (`UiPauseOpen`), fade in при входе, pause при выходе |
| `GameplaySfx` | FMOD Voice Pool (8, `sfxPoolSize`) | Удары, голы, комбо, баффы |
| `UiSfx` | FMOD Voice Pool (3, `uiPoolSize`) | UI |

Размер пулов задаётся **только** в Inspector `FMODAudioManager` (`Max Voices` для соответствующего Bus).

---

## FMOD API (Unity Integration 2.03.11)

| API | Где | Для чего |
|-----|-----|----------|
| `FMODUnity.RuntimeManager` | Высокоуровневое | `PlayOneShot()`, `CreateInstance()`, `GetInstance()` |
| `FMOD.Studio.System` | Низкоуровневое | Прямое управление инстансами через `RuntimeManager.StudioSystem` |
| `FMODUnity.EventReference` | Inspector | Привязка событий в Unity-компонентах |

> [!warning] **Не использовать** устаревший `[FMODUnity.EventRef]` — вместо него `FMODUnity.EventReference`.

---

## Музыка матча

| Когда | Действие |
|-------|----------|
| Вход в `OnField` (`NavigationChanged`) | `Play(MusicMatch)` — случайный трек из каталога |
| `MatchStartedEvent` (подача мяча) | **Заглушка**: только свисток `MatchStart`, без музыки |
| `OnField → Pause` / `OnField → MainMenu` / `Pause → MainMenu` | fade out музыки + `Pause()` + `PlayPauseSound` (fade in трека паузы) |
| `Pause → OnField` / возврат из меню с паузой матча | `Pause()` трека паузы + `UnPause()` музыки + fade in музыки |
| `MatchEndedEvent` | `MatchEnd` + `StopMusic` (fade out) |
| `PitchResetRequestedEvent` на поле | `StopMusic` + `StopPauseSound` + новый `MusicMatch` (рестарт турнира) |
| `PitchResetRequestedEvent` не на поле | только `StopMusic`; старт при следующем `OnField` |

`MusicMatch` в каталоге: `loop: true` — выбранный трек зацикливается. Автосмена на другой трек после окончания **не реализована**.

### Трек паузы

| Параметр | Значение |
|----------|----------|
| Event Path | `event:/UI/Pause/Open` (настраивается через `pauseEventPath`) |
| EventInstance | `_pauseSource` (отдельный от музыки) |
| Fade in | `fadeDuration` при входе в паузу |
| Fade out | `fadeOutDuration` при полном выходе из паузы на поле |
| Пауза | `pause()` / `play()` — синхронно с музыкой |
| Loop | `true` — зацикливается при входе в паузу |

> [!note] **Важно:** Трек паузы запускается **только** через `_pauseSource` из `PauseMusic()`. Прямой вызов `Play("event:/UI/Pause/Open")` из `AudioService` **убран** во избежание дублирования.

---

## Маппинг событий → звуки

**Полный справочник для звукаря:** [[Каталог событий и звуков]].

| Событие | Условие | FMOD Event Path / действие |
|---------|---------|---------------------------|
| `BallContactEvent` | `Wall` | `event:/SFX/Gameplay/BallHit` |
| `BallContactEvent` | `PlayerKeeper` / `Defender` | `event:/SFX/Gameplay/BallHitMan` |
| `GoalScoredEvent` | `IsPlayerGoal` | `event:/SFX/Gameplay/GoalScored` |
| `GoalScoredEvent` | !`IsPlayerGoal` | `event:/SFX/Gameplay/GoalConceded` |
| `MatchStartedEvent` | — | `event:/SFX/Gameplay/MatchStart` |
| `MatchEndedEvent` | — | `event:/SFX/Gameplay/MatchEnd` + stop music |
| `PitchResetRequestedEvent` | на поле | stop + `event:/Music/Match` |
| `PitchResetRequestedEvent` | не на поле | stop music |
| `MatchTimeAdjustedEvent` | Δt > 0 / < 0 | `event:/SFX/Gameplay/TimeBonus` / `event:/SFX/Gameplay/TimePenalty` |
| `DefenderHitEvent` | — | `event:/SFX/Gameplay/DefenderHit` |
| `DefenderDestroyedEvent` | — | `event:/SFX/Gameplay/DefenderDestroyed` |
| `DefenderPromotionStartedEvent` | — | `event:/SFX/Gameplay/PromotionStarted` |
| `DefenderPromotionCompletedEvent` | — | `event:/SFX/Gameplay/PromotionCompleted` |
| `DefenderReturnedHomeEvent` | — | `event:/SFX/Gameplay/DefenderReturned` |
| `DefenderRoleChangedEvent` | `IsGoalkeeper` | `event:/SFX/Gameplay/DefenderRoleChanged` |
| `PerkPickedEvent` | — | `event:/SFX/UI/PerkPick` |
| `RunProgressionUpdatedEvent` | уровень вырос | `event:/SFX/UI/LevelUp` |
| `PitchPhaseChangedEvent` | `Reshuffle` / `BonusPick` | `event:/SFX/UI/ReshuffleStart` / `event:/SFX/UI/BonusPickOpen` |
| `ComboScoreChangedEvent` | множитель вырос | `event:/SFX/UI/ComboMultiplierUp` |
| `ComboScoreChangedEvent` | множитель упал на ≥ 2 | `event:/SFX/UI/ComboMultiplierDown` |
| `ComboScoreChangedEvent` | `DeltaPoints > 0` | `event:/SFX/UI/ScorePoints` |
| `StatusEffectAppliedEvent` | бафф / дебафф | `event:/SFX/Gameplay/BuffApplied` / `event:/SFX/Gameplay/DebuffApplied` |
| `StatusEffectRemovedEvent` | `Consumed` | `event:/SFX/UI/BuffConsumed` |
| `NavigationChangedEvent` | вход в `OnField` | `event:/Music/Match` (если не resume) |
| `NavigationChangedEvent` | пауза / меню | fade pause music |
| `NavigationChangedEvent` | возврат на поле | fade resume music |
| `NavigationChangedEvent` | MainMenu / Pause / Tournament | `event:/UI/Menu/Open` / `event:/UI/Pause/Open` / `event:/UI/Tournament/Open` |

Константы path: `FMODAudioCatalog.Paths` в `FMODAudioCatalog.cs`.

---

## Прямой вызов (UI, кнопки)

```csharp
[Inject] private IAudioManager _audio;

_audio.Play(FMODAudioCatalog.Paths.UiMenuOpen);
_audio.Play(FMODAudioCatalog.Paths.BallHit, pitch: 1.1f, pitchRandomRange: 0.05f);
```

Если `pitch` / `pitchRandomRange` не переданы — берутся из `FMODSoundDefinition` в каталоге.

---

## Файлы

| Файл | Путь |
|------|------|
| `FMODAudioCatalog.cs` | `Futboloid.Core/Audio/` |
| `FMODSoundDefinition.cs` | `Futboloid.Core/Audio/` |
| `AudioService.cs` | `Futboloid.Core/Audio/` |
| `IAudioManager.cs` | `Futboloid.Core/Audio/` |
| `FMODAudioManager.cs` | `Futboloid.Main/Audio/` |

---

## DI

```csharp
// RootScopeExtensions
builder.RegisterComponentInHierarchy<FMODAudioManager>().As<IAudioManager>();

// AppScopeExtensions
builder.Register<AudioService>(Lifetime.Singleton);
```

`FMODAudioCatalog` **не** регистрируется в App DI — только ссылка в Inspector `FMODAudioManager`.

`AudioService` резолвится при старте App scope. При `AppGameState.Exit` — `Dispose`, `StopAll`.

---

## Настройка в Unity

Подробно: [[Инструкция по настройке]].

1. **Root.unity** — объект `Audio` + компонент `FMODAudioManager`, поле **Config** → `FMODAudioCatalog.asset`
2. **FMODAudioCatalog** — `Assets/_Projects/Resources/Data/Settings/FMODAudioCatalog`
3. **Game.unity** — старый `AudioManager` / `Speaker_*` удалены
4. **FMOD Studio** — создать Buses: `Music`, `SFX`, `UI`, `Pause`

---

## Связанные заметки

- [[Каталог событий и звуков]]
- [[Инструкция по настройке]]
- [[Система приоритетов и наложения]]
- [[Архитектура/Шина событий|Шина событий]]
- [[Архитектура/DI и LifetimeScope|DI и LifetimeScope]]
- [[Контекст/Контекст рефакторинга аудио|Контекст рефакторинга]]
