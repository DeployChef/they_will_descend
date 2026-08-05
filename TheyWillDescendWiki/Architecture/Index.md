# Architecture — Index

Техническая архитектура **They Will Descend**. Опирается на [[../GDD/00 Overview|GDD]].

> **Правило:** эти заметки — план и контракт. Сцены / префабы / `.meta` — человек в Unity; пошаговые инструкции по Editor — в чате / менторстве, не обязательно в вики. Код — ученик (или агент по явной просьбе) после согласования архитектуры.

> **Обучение:** агент = ментор (см. [[07 Mentorship & Learning]] + `.cursor/rules/`). Цель — production ECS, не джем-копия.

Прототипные решения можно подглядывать в **gmtk_2026** (сеттинг: пирамида, дань), но **не** переносить card/DnD архитектуру.

## Документы

- [[00 Overview]] — обзор стека и слоёв
- [[01 Folder Structure]] — `Assets/_Project`
- [[02 Scenes & Lifetime]] — Bootstrap, SubScene, lifetime
- [[03 Core Systems]] — director, время, границы команд/событий
- [[04 Simulation]] — город, люди, экономика, тепло/жизнь
- [[05 Content Pipeline]] — Authoring/Baker, blobs, баланс
- [[06 FMOD Audio]] — FMOD Studio 2.03: структура, API, правила именования
- [[07 Mentorship & Learning]] — роль ментора, формат уроков, прогресс
- [[08 Production ECS]] — целевая production-архитектура и точки расширения

## Связь с GDD

| GDD | Архитектура |
| --- | --- |
| [[../GDD/02 Gameplay Loop\|Gameplay Loop]] | [[03 Core Systems]] |
| [[../GDD/03 City & People\|City & People]] | [[04 Simulation]] |
| [[../GDD/04 Economy & Heat\|Economy & Heat]] | [[04 Simulation]] · [[05 Content Pipeline]] |
| [[../GDD/05 Gods & Timeline\|Gods & Timeline]] | [[03 Core Systems]] · события |
| [[../GDD/06 Laws & Choices\|Laws]] | content + simulation hooks |
