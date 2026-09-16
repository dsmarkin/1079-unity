# 1079 · Высота — Unity-версия

Второй клиент той же игры: Unity 6 (6000.0 LTS), C#, физика PhysX, Netcode for GameObjects. Логика ночи (`Assets/Scripts/Core`) — прямой порт `public/survival.js`, `server/run.js`, `public/world.js`, `server/terrain.js` из браузерной версии и не зависит от UnityEngine, поэтому её тесты гоняются и в Unity Test Runner, и обычным `dotnet`.

## Что уже есть

- Склон 1:1 из того же DEM (`Assets/Data/terrain.png` → Unity Terrain 257×257), кострище, палатка-укрытие, архивные метки, лес ниже 755 м.
- Персонаж на Rigidbody: ходьба/бег, скольжение на склоне круче 42°, падение с высоты снимает тепло и ясность (запись в протокол), первый/третий вид (V), пауза (Esc).
- Ночь считает хост (`NightSession` → `NightRun`): часы, метель, тепло/руки/ясность, розжиг по удержанию E, исходы и итог пары, grace 120 с. Клиенты получают снимки и протокол.
- HUD и полевой протокол построены из кода (uGUI), сцена и префабы генерируются редактором при первом открытии (`Assets/Resources`, `Assets/Scenes` в .gitignore).
- Сеть: Unity Transport, порт 7777 (хост/клиент по IP). Steam-лобби — следующий этап (Facepunch.Steamworks + транспорт).

## Как открыть

1. Unity Hub → Add → папка `unity/`. Версия 6000.0.x LTS (Hub предложит поставить нужную). Модули: macOS Build Support (Mono), Windows Build Support (Mono) — для сборок под обе платформы с Mac.
2. Первое открытие: пакеты подтянутся из манифеста, `ProjectSetup` создаст `Assets/Resources/{Hiker,NightSession}.prefab`, `terrain.bytes` и `Assets/Scenes/Main.unity`. Если что-то не появилось — меню **1079 → Generate prefabs and scene**.
3. Play. В меню: имя, «Создать ночь» (хост) или «Присоединиться» по адресу хоста. Второй экземпляр — через сборку или ParrelSync.

## Тесты

- В редакторе: Window → General → Test Runner → EditMode (11 тестов ядра).
- Без Unity: `dotnet run --project unity/Tools/CoreTests` (те же исходники, NUnit-шим).

## Сборки из командной строки

```
/Applications/Unity/Hub/Editor/6000.0.58f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath unity -executeMethod Height1079.EditorTools.Builds.Mac -logFile -
/Applications/Unity/Hub/Editor/6000.0.58f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath unity -executeMethod Height1079.EditorTools.Builds.Windows -logFile -
```

Результат — `unity/Builds/mac/1079.app` и `unity/Builds/windows/1079.exe`.

## Честные границы

Прототип вертикального среза: нет модели путника из GLB (пока капсула с капюшоном и рюкзаком — импорт через glTFast следующим шагом), нет снега-частиц и звука, нет Steam, позиции игроков доверяются владельцу (как в браузерной версии). Исторические точки — архивные данные; персонажи и исходы вымышлены.
