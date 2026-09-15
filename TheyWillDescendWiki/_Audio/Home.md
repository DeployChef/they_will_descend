# _Audio — документация аудио-системы

← [[../Home|Главная вики]]

Раздел описывает **фактическое** состояние звука в They Will Descend: каждый класс,
как он подключается, за что отвечает, какие FMOD-банки и ивенты задействованы,
что предстоит сделать.

Всё, что здесь написано про «есть в FMOD», сверено по метаданным проекта
`Audio_FMOD/they will desend/Metadata/` и по коду. Если раздел и код расходятся —
**истина в коде**, раздел подлежит правке.

## Документы

| Файл | О чём |
| --- | --- |
| [[00 Обзор системы]] | карта системы, кто кого вызывает, что реально работает |
| [[01 AudioZoneManager]] | хост 50 аудио-зон, пересборка, hot reload банков |
| [[02 AudioZone]] | одна зона = один FMOD-инстанс, RTPC, 3D-позиция |
| [[03 AudioZoneSettings]] | единственный ассет настроек (геометрия, ивенты, LOD, SFX) |
| [[04 AudioVisibilityChecker]] | горизонтальный конус камеры = слышимость |
| [[05 BuildingAudioSource]] | компонент постройки, источник плотности зоны |
| [[06 ZoneAmbienceSfxScheduler]] | «живчик города»: одноразовые звуки по зонам |
| [[07 GlobalAmbienceManager]] | глобальный амбиент (ветер), шина, Distance RTPC |
| [[08 FmodBankLoader]] | загрузка/выгрузка банков, поиск банков ивента |
| [[09 GameAudio]] | хост музыки на Bootstrap |
| [[10 Банки и ивенты FMOD]] | какие банки есть, какие планируются |
| [[11 Дорожки и параметры]] | ТЗ в FMOD Studio: дорожки и RTPC под них |
| [[12 Диагностика и грабли]] | известные ошибки FMOD API и поведения |
| [[13 Дорожная карта]] | план действий по порядку |

## Три вещи, которые надо знать до чтения остального

1. **Аудио-зоны не совпадают с ячейками сетки города.** Носитель звука — зона
   (10 угловых секторов × 5 радиальных полос = 50 зон), а не ячейка. В одной зоне
   живёт сразу много построек разных типов. Понятия `cellID` в аудио-коде нет.
2. **Параметры плотности в FMOD сейчас отсутствуют.** Unity честно считает
   `Cell_Activity` / `Has_Houses` / `Has_Workshops` / `Has_Market` и вызывает
   `setParameterByName`, но ни одного из этих четырёх параметров в FMOD-проекте
   нет — вызовы молча ничего не делают. Работает только `Distance`.
   Подробности: [[11 Дорожки и параметры]].
3. **Один `AudioZoneManager`, и он на Bootstrap.** Не десять. Все остальные
   аудио-классы находят его через `FindFirstObjectByType`, поэтому лишние копии
   компонента останутся пустыми.

## Где что лежит

```
Audio_FMOD/they will desend/                         ← проект FMOD Studio (.fspro)
  Metadata/Event/*.xml                               ← ивенты: путь, длина, параметры
  Metadata/ParameterPreset/*.xml                     ← глобальные параметры: имена, типы
  Metadata/Group/*.xml                               ← группы/шины микшера
  Metadata/Bank/*.xml                                ← банки
  Build/Desktop/*.bank                               ← результат Ctrl+B
TheyWillDescend/Assets/StreamingAssets/Desktop/      ← банки, которые видит Unity
TheyWillDescend/Assets/_Project/Scripts/Presentation/Audio/   ← аудио-логика
TheyWillDescend/Assets/_Project/Scripts/Presentation/City/    ← AudioZoneManager,
    AudioVisibilityChecker, BuildingAudioSource, BuildingViewBoard
```

## Связанная документация

- [[../Architecture/06 FMOD Audio|Architecture/06 FMOD Audio]] — API-справка
  FMOD 2.03, workflow звукорежиссёра, правила именования.
- [[../Architecture/12 Radial City Grid|Architecture/12 Radial City Grid]] —
  полярная сетка, чью геометрию аудио-зоны повторяют 1 в 1.
- [[../Architecture/11 Camera & Presentation Scenes|Architecture/11 Camera]] —
  одна Main Camera на Bootstrap; из неё считается конус слышимости.
- `AGENTS.md` в корне репозитория — правила работы и известные ошибки API.
- `TheyWillDescend Аудио супер дупер аудио/` (вне вики) — исторические заметки
  диагностики и журнал сессий. **Местами устарели**: говорят про 120 зон, сейчас 50.
