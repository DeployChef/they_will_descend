# 08 UI & Visual

← [[07 Win Lose]] | [[Home]] | Далее → [[09 Narrative & Onboarding]]

## Направление

- Читаемое поселение с сильным силуэтом пирамиды
- HUD кризисов: что горит прямо сейчас
- Таймлайн / фазы богов всегда в поле зрения
- Стиль: доколумбовый миф + суровый survival, не милый city-builder

## Камера и взаимодействие

**Канон:** одна Main Camera на Bootstrap (Root) + Cinemachine VCam на MainMenu/Game.  
Детали: [[../Architecture/11 Camera & Presentation Scenes|Architecture/11]].

Орбита / RTS-стиль кадра — отдельное решение геймплея; не путать с наличием второй Main Camera.


## Тулза времени (срез)

Верх HUD на Game, как во Frostpunk:

- пауза, x1, x2, x3
- текущие сутки и часы (`Day N` + `HH:MM` из `GameTime`)
- простые кнопки Save / Load (один слот)

Канон и границы слоёв: [[../Architecture/13 Time HUD and Save|Architecture/13]].

## Обязательные сигналы

- дань / дедлайн
- состояние людей (надежда, голод, болезнь)
- запасы критических ресурсов
- активные законы и их цена

---

Связанные разделы: [[02 Gameplay Loop]] · [[05 Gods & Timeline]]
