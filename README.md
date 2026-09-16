# 1079 · Высота — Unity-версия

Второй клиент той же игры: Unity 6 (6000.0 LTS), C#, физика PhysX, Netcode for GameObjects. Логика ночи (`Assets/Scripts/Core`) — прямой порт `public/survival.js`, `server/run.js`, `public/world.js`, `server/terrain.js` из браузерной версии и не зависит от UnityEngine, поэтому её тесты гоняются и в Unity Test Runner, и обычным `dotnet`.

## Что уже есть

- Склон 1:1 из того же DEM (`Assets/Data/terrain.png` → Unity Terrain 257×257), кострище, палатка-укрытие, архивные метки, лес ниже 755 м.
- Персонаж на Rigidbody: ходьба/бег, скольжение на склоне круче 42°, жёсткое приземление — спотыкание (1,2 с без управления, наклон, минус тепло и ясность, запись в протокол), первый/третий вид (V), пауза (Esc).
- Снег частицами вокруг камеры (в метель гуще и косее) и синтезированный звук без сэмплов: ветер с фильтром, шаги, треск костра — порт `sound.js`.
- Ночь считает хост (`NightSession` → `NightRun`): часы, метель, тепло/руки/ясность, розжиг по удержанию E, исходы и итог пары, grace 120 с. Клиенты получают снимки и протокол.
- HUD и полевой протокол построены из кода (uGUI), сцена и префабы генерируются редактором при первом открытии (`Assets/Resources`, `Assets/Scenes` в .gitignore).
- Модель путника — тот же `hiker-v1.glb` (Assets/Data → Resources/hiker.bytes), грузится glTFast в рантайме с пятью legacy-клипами Idle/Walk/Run/Cold/Kindle; пока модель не загрузилась, стоит капсула-заглушка.
- Сеть: Unity Transport, порт 7777 (хост/клиент по IP). Steam-лобби подготовлено в `Assets/Scripts/Steam` и включается сборкой (см. ниже).

## Как открыть

1. Unity Hub → Add → папка `unity/`. Версия 6000.0.x LTS (Hub предложит поставить нужную). Модули: macOS Build Support (Mono), Windows Build Support (Mono) — для сборок под обе платформы с Mac.
2. Первое открытие: пакеты подтянутся из манифеста, `ProjectSetup` создаст `Assets/Resources/{Hiker,NightSession}.prefab`, `terrain.bytes` и `Assets/Scenes/Main.unity`. Если что-то не появилось — меню **1079 → Generate prefabs and scene**.
3. Play. В меню: имя, «Создать ночь» (хост) или «Присоединиться» по адресу хоста. Второй экземпляр — через сборку или ParrelSync.

## Пролог

Сюжет, локации и участники миссий от Свердловска до палатки на склоне — в [docs/PROLOGUE.md](docs/PROLOGUE.md); посценный сценарий с локациями, референсами и репликами — в [docs/SCRIPT.md](docs/SCRIPT.md). Данные ростера и миссий — `Assets/Scripts/Core/Campaign.cs` (с тестами хронологии). Реальные имена — только в прологе и архиве; ночь на склоне остаётся с вымышленной парой.

## Тесты

- В редакторе: Window → General → Test Runner → EditMode (11 тестов ядра).
- Без Unity: `dotnet run --project unity/Tools/CoreTests` (те же исходники, NUnit-шим).

## Сборки из командной строки

```
/Applications/Unity/Hub/Editor/6000.0.58f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath unity -executeMethod Height1079.EditorTools.Builds.Mac -logFile -
/Applications/Unity/Hub/Editor/6000.0.58f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath unity -executeMethod Height1079.EditorTools.Builds.Windows -logFile -
```

Результат — `unity/Builds/mac/1079.app` и `unity/Builds/windows/1079.exe`.

## Steam

1. В `Packages/manifest.json` добавить транспорт сообщества (он несёт в себе Facepunch.Steamworks 2.3.2 и steam_api для Mac/Win/Linux):
   `"com.community.netcode.transport.facepunch": "https://github.com/Unity-Technologies/multiplayer-community-contributions.git?path=/Transports/com.community.netcode.transport.facepunch"`
2. Project Settings → Player → Scripting Define Symbols → добавить `STEAM_FACEPUNCH` (для Mac и Windows). Сборка `Height1079.Steam` компилируется только с этим символом, без него проект не зависит от Steam.
3. Рядом с исполняемым файлом (и в корне проекта для Play в редакторе) положить `steam_appid.txt` с `480` (тестовый AppID Valve) или своим AppID после регистрации в Steamworks.
4. Steam-клиент должен быть запущен. В меню появятся «Ночь через Steam» (лобби только для друзей + хост через Steam Relay) и «Пригласить друзей» (оверлей). Друг заходит через приглашение или «Присоединиться к игре» в списке друзей.

## Честные границы

Прототип вертикального среза, написанный без запуска редактора (ожидаются правки при первом открытии). Нет ragdoll (падение пока только штраф), нет снега-частиц и звука, Steam-слой не проверен вживую, позиции игроков доверяются владельцу (как в браузерной версии). Исторические точки — архивные данные; персонажи и исходы вымышлены.
