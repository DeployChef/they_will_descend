---
tags:
  - audio
  - architecture
  - manager
aliases:
  - Звук
  - Audio
  - Аудио система
---

# Аудио система

← [[Home|Главная]] | [[Архитектура/Индекс архитектуры|Архитектура]] | [[Архитектура/DI и LifetimeScope|DI]]

> [!date] Обновлено
<<<<<<< HEAD
> **12.07.2026** — трек паузы (`_pauseSource`), раздельный fade in/out, `stopOnPause`, точный таймер fade.
=======
> **08.07.2026** — `AudioManager`, конфиг на менеджере, музыка на `OnField`, fade pause/resume, комбо и баффы.
>>>>>>> origin/develop

---

## Краткая выжимка

Геймплей **не вызывает звук напрямую**. `AudioService` слушает `IGameEventBus` и передаёт **Sound Id** в `IAudioManager`. UI и кнопки могут вызывать `IAudioManager.Play(...)` напрямую.

```
BallMotion → bus.Publish(BallContactEvent)
                    ↓
              AudioService (App scope) → Play("BallHitMan")
                    ↓
              AudioManager (Root scene, config: AudioCatalog)
                    ↓
              Music / SfxPool / UiPool → Mixer
```

---

## Слои

| Слой | Класс | Где | Задача |
|------|-------|-----|--------|
| CMS | `AudioCatalog` + `SoundDefinition` | asset в Inspector менеджера | Клипы, микшер, priority, cooldown, pitch |
| Маппинг | `AudioService` | App scope (DI) | Подписки на шину → Sound Id |
| Unity | `AudioManager` | Root.unity | Конфиг, cooldown, overlap, пул, музыка, fade |

**Один менеджер** на всю игру. Контекст (меню / пауза / поле) — через `NavigationChangedEvent`.

---

## Каналы воспроизведения

| Канал | AudioSource | Для чего |
|-------|-------------|----------|
| `Music` | 1 источник | Loop, fade in/out, fade pause/resume |
<<<<<<< HEAD
| `Pause` | 1 источник | Трек паузы (`UiPauseOpen`), fade in при входе, pause при выходе |
=======
>>>>>>> origin/develop
| `GameplaySfx` | Пул (8, `sfxPoolSize`) | Удары, голы, комбо, баффы |
| `UiSfx` | Пул (3, `uiPoolSize`) | UI |

Размер пулов задаётся **только** в Inspector `AudioManager`, не в `AudioCatalog`.

---

## Музыка матча

| Когда | Действие |
|-------|----------|
| Вход в `OnField` (`NavigationChanged`) | `Play(MusicMatch)` — случайный трек из каталога |
| `MatchStartedEvent` (подача мяча) | только свисток `MatchStart`, **без музыки** |
<<<<<<< HEAD
| `OnField → Pause` / `OnField → MainMenu` / `Pause → MainMenu` | fade out музыки + `Pause()` + `PlayPauseSound` (fade in трека паузы) |
| `Pause → OnField` / возврат из меню с паузой матча | `Pause()` трека паузы + `UnPause()` музыки + fade in музыки |
| `MatchEndedEvent` | `MatchEnd` + `StopMusic` (fade out) |
| `PitchResetRequestedEvent` на поле | `StopMusic` + `StopPauseSound` + новый `MusicMatch` (рестарт турнира) |
| `PitchResetRequestedEvent` не на поле | только `StopMusic`; старт при следующем `OnField` |

`MusicMatch` в каталоге: `loop: true` — выбранный трек зацикливается. Автосмена на другой трек после окончания **не реализована**.

### Трек паузы

| Параметр | Значение |
|----------|----------|
| Sound ID | `UiPauseOpen` (настраивается через `pauseSoundId`) |
| AudioSource | `_pauseSource` (отдельный от музыки) |
| Fade in | `fadeDuration` при входе в паузу |
| Fade out | `fadeOutDuration` при полном выходе из паузы на поле |
| Пауза | `Pause()` / `UnPause()` — синхронно с музыкой |
| Loop | `true` — зацикливается при входе в паузу |

> [!note] **Важно:** Трек паузы запускается **только** через `_pauseSource` из `PauseMusic()`. Прямой вызов `Play("UiPauseOpen")` из `AudioService` **убран** во избежание дублирования.
=======
| `OnField → Pause` / `OnField → MainMenu` / `Pause → MainMenu` | fade out + `Pause()` — позиция в треке сохраняется |
| `Pause → OnField` / возврат из меню с паузой матча | fade in + `UnPause()` — **с того же места** |
| `MatchEndedEvent` | `MatchEnd` + `StopMusic` (fade out) |
| `PitchResetRequestedEvent` на поле | `StopMusic` + новый `MusicMatch` (рестарт турнира) |
| `PitchResetRequestedEvent` не на поле | только `StopMusic`; старт при следующем `OnField` |

`MusicMatch` в каталоге: `loop: true` — выбранный трек зацикливается. Автосмена на другой трек после окончания **не реализована**.
>>>>>>> origin/develop

---

## Маппинг событий → звуки

**Полный справочник для звукаря:** [[Каталог событий и звуков]].

| Событие | Условие | Sound ID / действие |
|---------|---------|---------------------|
| `BallContactEvent` | `Wall` | `BallHit` |
| `BallContactEvent` | `PlayerKeeper` / `Defender` | `BallHitMan` |
| `GoalScoredEvent` | `IsPlayerGoal` | `GoalScored` / `GoalConceded` |
| `MatchStartedEvent` | — | `MatchStart` |
| `MatchEndedEvent` | — | `MatchEnd` + stop music |
| `PitchResetRequestedEvent` | на поле | stop + `MusicMatch` |
| `PitchResetRequestedEvent` | не на поле | stop music |
| `MatchTimeAdjustedEvent` | Δt &gt; 0 / &lt; 0 | `TimeBonus` / `TimePenalty` |
| `DefenderHitEvent` | — | `DefenderHit` |
| `DefenderDestroyedEvent` | — | `DefenderDestroyed` |
| `DefenderPromotionStartedEvent` | — | `PromotionStarted` |
| `DefenderPromotionCompletedEvent` | — | `PromotionCompleted` |
| `DefenderReturnedHomeEvent` | — | `DefenderReturned` |
| `DefenderRoleChangedEvent` | `IsGoalkeeper` | `DefenderRoleChanged` |
| `PerkPickedEvent` | — | `PerkPick` |
| `RunProgressionUpdatedEvent` | уровень вырос | `LevelUp` |
| `PitchPhaseChangedEvent` | `Reshuffle` / `BonusPick` | `ReshuffleStart` / `BonusPickOpen` |
| `ComboScoreChangedEvent` | множитель вырос | `ComboMultiplierUp` |
| `ComboScoreChangedEvent` | множитель упал на ≥ 2 | `ComboMultiplierDown` |
| `ComboScoreChangedEvent` | `DeltaPoints > 0` | `ScorePoints` |
| `StatusEffectAppliedEvent` | бафф / дебафф | `BuffApplied` / `DebuffApplied` |
| `StatusEffectRemovedEvent` | `Consumed` | `BuffConsumed` |
| `NavigationChangedEvent` | вход в `OnField` | `MusicMatch` (если не resume) |
| `NavigationChangedEvent` | пауза / меню | fade pause music |
| `NavigationChangedEvent` | возврат на поле | fade resume music |
| `NavigationChangedEvent` | MainMenu / Pause / Tournament | `UiMenuOpen` / `UiPauseOpen` / `UiTournamentOpen` |

Константы id: `AudioCatalog.Ids` в `AudioCatalog.cs`.

---

## Прямой вызов (UI, кнопки)

```csharp
[Inject] private IAudioManager _audio;

_audio.Play(AudioCatalog.Ids.UiMenuOpen);
_audio.Play(AudioCatalog.Ids.BallHit, pitch: 1.1f, pitchRandomRange: 0.05f);
```

Если `pitch` / `pitchRandomRange` не переданы — берутся из `SoundDefinition` в каталоге.

---

## Файлы

| Файл | Путь |
|------|------|
| `AudioCatalog.cs` | `Futboloid.Core/Audio/` |
| `SoundDefinition.cs` | `Futboloid.Core/Audio/` |
| `AudioService.cs` | `Futboloid.Core/Audio/` |
| `IAudioManager.cs` | `Futboloid.Core/Audio/` |
| `AudioManager.cs` | `Futboloid.Main/Audio/` |

---

## DI

```csharp
// RootScopeExtensions
builder.RegisterComponentInHierarchy<AudioManager>().As<IAudioManager>();

// AppScopeExtensions
builder.Register<AudioService>(Lifetime.Singleton);
```

`AudioCatalog` **не** регистрируется в App DI — только ссылка в Inspector `AudioManager`.

`AudioService` резолвится при старте App scope. При `AppGameState.Exit` — `Dispose`, `StopAll`.

---

## Настройка в Unity

Подробно: [[Инструкция по настройке|Инструкция по настройке]].

1. **Root.unity** — объект `Audio` + компонент `AudioManager`, поле **Config** → `AudioCatalog.asset`
2. **AudioCatalog** — `Assets/_Projects/Resources/Data/Settings/AudioCatalog`
3. **Game.unity** — старый `AudioManager` / `Speaker_*` удалены

---

## Связанные заметки

- [[Каталог событий и звуков]]
- [[Инструкция по настройке]]
- [[Система приоритетов и наложения]]
- [[Архитектура/Шина событий|Шина событий]]
- [[Архитектура/DI и LifetimeScope|DI и LifetimeScope]]
- [[Контекст/Контекст чата 05.07.2026 аудио рефакторинг|Контекст рефакторинга]]
- [[Контекст/Контекст чата 08.07.2026 аудио доработка|Контекст доработки 08.07]]
