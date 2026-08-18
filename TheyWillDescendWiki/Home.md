# They Will Descend — Wiki

Обсидиан-хранилище дизайн- и технической документации полной игры.

Прототип джема (референс): репозиторий **gmtk_2026**. Здесь — Frostpunk-like развитие той же фантазии: поселение, пирамида, боги, отсчёт.

## Game Design Document

- [[GDD/00 Overview|00 Overview]] — vision, жанр, референсы, отличия от джема
- [[GDD/01 Design Pillars|01 Design Pillars]] — столпы дизайна и non-goals
- [[GDD/02 Gameplay Loop|02 Gameplay Loop]] — микро- и макро-цикл
- [[GDD/03 City & People|03 City & People]] — жители, роли, мораль, надежда / недовольство
- [[GDD/04 Economy & Heat|04 Economy & Heat]] — ресурсы, здания, тепло / жизнеобеспечение
- [[GDD/05 Gods & Timeline|05 Gods & Timeline]] — пирамида, дань, фазы, кризисы
- [[GDD/06 Laws & Choices|06 Laws & Choices]] — эдикты, жёсткие решения, цена выживания
- [[GDD/07 Win Lose|07 Win Lose]] — победа, поражение, концовки
- [[GDD/08 UI & Visual|08 UI & Visual]] — камера, стиль, HUD
- [[GDD/09 Narrative & Onboarding|09 Narrative & Onboarding]] — лор, кампания, обучение
- [[GDD/10 Roadmap|10 Roadmap]] — вертикальный срез и этапы

## Balance

- [[Balance/Balance|Balance]] — кривые сложности, рычаги, заметки по тюнингу

## Architecture

- [[Architecture/Index|Index]] — индекс техдоков
- [[Architecture/00 Overview|00 Overview]] — стек и слои
- [[Architecture/01 Folder Structure|01 Folder Structure]] — `Assets/_Project`
- [[Architecture/02 Scenes & Lifetime|02 Scenes & Lifetime]] — Root/Game, SubScene, SimGate
- [[Architecture/03 Core Systems|03 Core Systems]] — AppFlow, Director, Time, GameLog
- [[Architecture/04 Simulation|04 Simulation]] — город, люди, экономика, тепло
- [[Architecture/05 Content Pipeline|05 Content Pipeline]] — Authoring/Baker, баланс
- [[Architecture/06 FMOD Audio|06 FMOD Audio]] — FMOD Studio 2.03
- [[Architecture/07 Mentorship & Learning|07 Mentorship & Learning]] — ментор, уроки, прогресс
- [[Architecture/08 Production ECS|08 Production ECS]] — production ECS, расширение
- [[Architecture/09 App Shell|09 App Shell]] — Shell FSM, DI, Frostpunk-поток
- [[Architecture/10 Vertical Slice — Shell + ECS Walkers|10 Vertical Slice]] — меню → ECS ходьба
- [[Architecture/11 Camera & Presentation Scenes|11 Camera]] — одна Main Camera на Root, VCam
- [[Architecture/12 Radial City Grid|12 Radial City Grid]] — полярная сетка, placement
- [[Architecture/13 Time HUD and Save|13 Time HUD & Save]] — часы/скорость и слот save/load
