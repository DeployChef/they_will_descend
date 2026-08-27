# They Will Descend — Wiki

Обсидиан-хранилище дизайн- и технической документации.

Это **полная AA city-survival** (Maya / пирамида / боги), не джем и не продолжение card-архитектуры GMTK. Прототип джема — репозиторий **gmtk_2026**: сеттинг и фантазия, не write model и не UI.

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
- [[Architecture/02 Scenes & Lifetime|02 Scenes & Lifetime]] — Bootstrap / MainMenu / Loading / Game
- [[Architecture/03 Core Systems|03 Core Systems]] — Shell FSM, session, часы ECS
- [[Architecture/04 Simulation|04 Simulation]] — город, люди, экономика, тепло
- [[Architecture/05 Content Pipeline|05 Content Pipeline]] — здания, ресурсы, сценарий (гайд)
- [[Architecture/06 FMOD Audio|06 FMOD Audio]] — FMOD Studio 2.03
- [[Architecture/07 Mentorship & Learning|07 Mentorship & Learning]] — ментор, уроки, прогресс
- [[Architecture/08 Production ECS|08 Production ECS]] — production ECS, расширение
- [[Architecture/09 App Shell|09 App Shell]] — две машины, FSM, хосты Bootstrap
- [[Architecture/10 Vertical Slice — Shell + ECS Walkers|10 Vertical Slice]] — что уже в срезе
- [[Architecture/11 Camera & Presentation Scenes|11 Camera]] — одна Main Camera на Bootstrap
- [[Architecture/12 Radial City Grid|12 Radial City Grid]] — полярная сетка, occupancy в ECS
- [[Architecture/13 Time HUD and Save|13 Time HUD & Save]] — часы, скорость, слот save/load
- [[Architecture/14 Sim Presentation Bridge|14 Bridge]] — команды в ECS, pull видов
