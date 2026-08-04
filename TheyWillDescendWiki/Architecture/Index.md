# Architecture — Index

Техническая архитектура **They Will Descend**. Опирается на [[../GDD/00 Overview|GDD]].

> **Правило:** эти заметки — план и контракт. Сцены / префабы / `.meta` — человек в Unity; пошаговые инструкции по Editor — в чате, не в вики. Код — агент после согласования.

Прототипные решения можно подглядывать в **gmtk_2026/wiki/Architecture**, но не копировать слепо: scope полной игры другой.

## Документы

- [[00 Overview]] — обзор стека и слоёв
- [[01 Folder Structure]] — `Assets/_Project`
- [[02 Scenes & Lifetime]] — сцены и lifetime / DI
- [[03 Core Systems]] — director, event bus, время сессии
- [[04 Simulation]] — город, люди, экономика, тепло/жизнь
- [[05 Content Pipeline]] — ScriptableObjects, баланс, события
- [[06 FMOD Audio]] — FMOD Studio 2.03: структура, API, правила именования

## Связь с GDD

| GDD | Архитектура |
| --- | --- |
| [[../GDD/02 Gameplay Loop\|Gameplay Loop]] | [[03 Core Systems]] |
| [[../GDD/03 City & People\|City & People]] | [[04 Simulation]] |
| [[../GDD/04 Economy & Heat\|Economy & Heat]] | [[04 Simulation]] · [[05 Content Pipeline]] |
| [[../GDD/05 Gods & Timeline\|Gods & Timeline]] | [[03 Core Systems]] · события |
| [[../GDD/06 Laws & Choices\|Laws]] | content + simulation hooks |
