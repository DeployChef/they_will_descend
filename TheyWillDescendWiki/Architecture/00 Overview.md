# 00 Overview

← [[Index]] | [[../Home|Home]]

## Стек (черновик)

| Слой | Выбор |
| --- | --- |
| Engine | Unity 6, URP |
| DI / lifetime | TBD (в джеме был VContainer + Root LifetimeScope) |
| Асинхронность / UI | TBD |
| Данные контента | ScriptableObjects + таблицы баланса |

## Слои

```
Presentation (UI, камера, VFX)
        ↓
Session / Director (ран, фазы, win/lose)
        ↓
Simulation (город, люди, экономика, боги)
        ↓
Content (definitions, события, законы)
```

## Принципы

- Симуляция не знает про конкретные кнопки UI
- События — явные сообщения, не скрытые синглтон-хаки без контракта
- Баланс крутится данными, не магическими константами в коде

---

Далее → [[01 Folder Structure]]
