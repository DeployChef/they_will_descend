# Architecture — Index

Техническая архитектура **They Will Descend**. Опирается на [[../GDD/00 Overview|GDD]].

> Эти заметки — контракт, сверенный с кодом. Сцены / префабы / `.meta` собирает человек в Unity. Пошаговый гайд по зданиям, ресурсам и сценарию — [[05 Content Pipeline]]. Агент = ментор ([[07 Mentorship & Learning]] + `.cursor/rules/`).

Прототип **gmtk_2026** — референс сеттинга. Не card/DnD и не write model. Shell: [[09 App Shell]].

## Документы

- [[00 Overview]] — стек и слои
- [[01 Folder Structure]] — `Assets/_Project`, четыре сборки
- [[02 Scenes & Lifetime]] — Bootstrap, MainMenu, Loading, Game, SubScene
- [[03 Core Systems]] — AppFlow, GameSession, SimControl, GameLog
- [[04 Simulation]] — город, люди, экономика (что есть / что ещё нет)
- [[05 Content Pipeline]] — здания, ресурсы, сценарий: как заводить
- [[06 FMOD Audio]] — FMOD Studio 2.03 + хост `GameAudio`
- [[07 Mentorship & Learning]] — роль ментора, формат уроков
- [[08 Production ECS]] — production-архитектура и точки расширения
- [[09 App Shell]] — две машины, FSM без Tick, хосты Bootstrap
- [[10 Vertical Slice — Shell + ECS Walkers]] — срез: меню → город → стройка/назначение
- [[11 Camera & Presentation Scenes]] — одна Main Camera на Bootstrap
- [[12 Radial City Grid]] — полярный cluster underlay; occupy в ECS
- [[13 Time HUD and Save]] — пауза/x1/x2/x3, часы дня, однослотовый save/load
- [[14 Sim Presentation Bridge]] — `SimCommands.TryPost`, pull видов, Playback

## Связь с GDD

| GDD | Архитектура |
| --- | --- |
| [[../GDD/02 Gameplay Loop\|Gameplay Loop]] | [[03 Core Systems]] · [[09 App Shell]] |
| [[../GDD/03 City & People\|City & People]] | [[04 Simulation]] |
| [[../GDD/04 Economy & Heat\|Economy & Heat]] | [[04 Simulation]] · [[05 Content Pipeline]] |
| [[../GDD/05 Gods & Timeline\|Gods & Timeline]] | срез 1 в коде; хуки — [[08 Production ECS]] |
| [[../GDD/06 Laws & Choices\|Laws]] | content + simulation hooks, позже |
