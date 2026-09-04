// ============================================================================
// FMOD STUDIO: Событие Cell_Ambience
// ============================================================================
//
// Путь: event:/audio/city/Cell_Ambience
// Банк: AudioCity
//
// СТРУКТУРА СОБЫТИЯ (10 слоёв/дорожек):
//
// 1. GENERAL_AMBIENCE     — общий городской фон (шум города, ветер)
// 2. HOUSE_CHATTER         — разговоры жителей
// 3. WORKSHOP_BANG         — стук мастерских (кузницы, гончарки)
// 4. MARKET_HAGGLE         — торговля на рынке (крики торговцев)
// 5. FIRE_CRACKLE          — потрескивание костров
// 6. BIRD_CHIRP            — пение птиц (уличные)
// 7. CHILD_LAUGH           — детский смех (жилой район)
// 8. DOG_BARK              — лай собак (ночь/вечер)
// 9. INSECT_HUM            — жужжание насекомых (день/лето)
// 10. WIND_HOWL            — вой ветра (открытые зоны)
//
// ===== RTPC-ПАРМЕТРЫ (входят в событие): =====
//
// 1. Cell_Activity         (0 → 1)
//    — Общая активность ячейки. Зависит от количества и типа построек.
//    — Влияет на громкость всех слоёв.
//
// 2. Has_Houses            (0 → 1)
//    — Есть ли жилые дома в ячейке.
//    — Влияет на: HOUSE_CHATTER, CHILD_LAUGH, INSECT_HUM
//
// 3. Has_Workshops         (0 → 1)
//    — Есть ли мастерские в ячейке.
//    — Влияет на: WORKSHOP_BANG, FIRE_CRACKLE
//
// 4. Has_Market            (0 → 1)
//    — Есть ли рынок в ячейке.
//    — Влияет на: MARKET_HAGGLE, BIRD_CHIRP
//
// 5. Has_Infrastructure    (0 → 1)
//    — Есть ли инфраструктура (храмы, склады).
//    — Влияет на: WIND_HOWL (тише с инфраструктурой = защита)
//
// 6. Day_Night             (0 → 1)
//    — 0 = день, 1 = ночь.
//    — Влияет на: DOG_BARK (ночью чаще), CHILD_LAUGH (днём чаще)
//    — Примечание: передаётся из GameAudio через RuntimeManager.GetEvent()
//
// 7. Season                (0 → 3)
//    — 0 = весна, 1 = лето, 2 = осень, 3 = зима.
//    — Влияет на: INSECT_HUM (лето), WIND_HOWL (зима)
//
// 8. RainIntensity         (0 → 1)
//    — Интенсивность дождя.
//    — Влияет на: WIND_HOWL, FIRE_CRACKLE (тише под дождём)
//
// 9. PopulationDensity     (0 → 1)
//    — Плотность населения в районе.
//    — Влияет на: HOUSE_CHATTER, DOG_BARK
//
// 10. EventDistance        (0 → 1)
//     — Дистанция до ячейки (автоматически от FMOD 3D).
//     — Влияет на громкость через 3D-атрибуты FMOD.
//
// ===== НАСТРОЙКА СЛОЁВ В FMOD STUDIO =====
//
// Для каждого слоя используйте Volume Envelope или Parameter Controller:
//
// Пример для HOUSE_CHATTER:
//   — Parameter Controller → Parameter: Has_Houses → Min: 0, Max: 1
//   — Parameter Controller → Parameter: Cell_Activity → Min: 0, Max: 1
//   — Volume: от -∞ dB (Has_Houses=0) до -6 dB (Has_Houses=1, Cell_Activity=1)
//
// Пример для FIRE_CRACKLE:
//   — Parameter Controller → Parameter: Has_Workshops → Min: 0, Max: 1
//   — Parameter Controller → Parameter: Day_Night → Min: 0, Max: 1
//   — Volume: от -∞ dB (Has_Workshops=0) до -12 dB (Has_Workshops=1, Night)
//
// ===== ПРИМЕР КОДА ДЛЯ Day_Night (из GameAudio) =====
//
//   var event = RuntimeManager.GetEvent("event:/audio/city/Cell_Ambience");
//   event.setParameter("Day_Night", dayNightValue); // 0.0 = день, 1.0 = ночь
//   event.setParameter("Season", seasonValue);       // 0-3
//   event.setParameter("RainIntensity", rainValue);   // 0-1
//
// ===== BANK =====
//
// После создания события:
//   1. Перетащите событие на шину AudioCity в FMOD Studio
//   2. Ctrl+B (Build) — скомпилируйте банки
//   3. Скопируйте .bank файл в StreamingAssets/Desktop/
//
// Шина: bus:/AudioCity
// ============================================================================
