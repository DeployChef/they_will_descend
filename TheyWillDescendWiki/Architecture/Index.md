# Architecture — Index

Техническая архитектура **They Will Descend**. Опирается на [[../GDD/00 Overview|GDD]].

> **Правило:** эти заметки — план и контракт. Сцены / префабы / `.meta` собирает человек в Unity. Пошаговый гайд по зданиям, ресурсам и сценарию — [[05 Content Pipeline]]. Код — ученик (или агент по явной просьбе) после согласования архитектуры.

> **Обучение:** агент = ментор (см. [[07 Mentorship & Learning]] + `.cursor/rules/`). Цель — production ECS, не джем-копия.

Прототипные решения: **gmtk_2026** — референс shell/DI сцен и сеттинга; **не** card/DnD и не write model. Shell-контракт: [[09 App Shell]].

## Документы

- [[00 Overview]] — обзор стека и слоёв
- [[01 Folder Structure]] — `Assets/_Project`
- [[02 Scenes & Lifetime]] — Root/Game, SubScene, SimGate
- [[03 Core Systems]] — AppFlow, Director, Time ECS, GameLog
- [[04 Simulation]] — город, люди, экономика, тепло/жизнь
- [[05 Content Pipeline]] — здания, ресурсы, сценарий: как заводить и где что висит
- [[06 FMOD Audio]] — FMOD Studio 2.03: структура, API, правила именования
- [[07 Mentorship & Learning]] — роль ментора, формат уроков, прогресс
- [[08 Production ECS]] — целевая production-архитектура и точки расширения
- [[09 App Shell]] — Shell FSM, DI vs ECS, Frostpunk-поток, наследие gmtk
- [[10 Vertical Slice — Shell + ECS Walkers]] — заходы A–D: меню → ECS ходьба → Frozen
- [[11 Camera & Presentation Scenes]] — одна Main Camera на Root, VCam, что на какой сцене
- [[12 Radial City Grid]] — полярный cluster underlay + placement (FP-like); ECS occupancy / smart align — позже
- [[13 Time HUD and Save]] — тулза времени (пауза/x1/x2/x3, часы дня) и однослотовый save/load
- [[14 Sim Presentation Bridge]] — команды в ECS, события наружу, виды без Transform в компонентах

## Связь с GDD

| GDD | Архитектура |
| --- | --- |
| [[../GDD/02 Gameplay Loop\|Gameplay Loop]] | [[03 Core Systems]] · [[09 App Shell]] |
| [[../GDD/03 City & People\|City & People]] | [[04 Simulation]] |
| [[../GDD/04 Economy & Heat\|Economy & Heat]] | [[04 Simulation]] · [[05 Content Pipeline]] |
| [[../GDD/05 Gods & Timeline\|Gods & Timeline]] | [[03 Core Systems]] · события |
| [[../GDD/06 Laws & Choices\|Laws]] | content + simulation hooks |
