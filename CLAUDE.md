# CLAUDE.md — как работать в репозитории 1079

Этот файл для агента (Claude Code и др.) и для людей, которые вместе пишут игру. Прочитай его целиком до первой правки. Обзор игры — в [README.md](README.md).

## Команда и язык

- Репозиторий `github.com/dsmarkin/1079-unity`, ветка `main` общая. Владелец — Дон (`dsmarkin`), соавтор — `kvadrantul`. У обоих (и у их агентов) есть права на push.
- Общаемся и пишем документацию **по-русски**. Тексты в игре (HUD, протокол) тоже по-русски.
- Код, идентификаторы, комментарии и сообщения коммитов пишем **по-английски**.

## 1. Что поставить, чтобы тестировать локально

| Что | Зачем | Как |
|---|---|---|
| **Unity 6000.0.84f1** (ровно эта версия, см. `ProjectSettings/ProjectVersion.txt`) | редактор, тесты, сборки | Unity Hub → Installs → Install Editor → Archive → 6000.0.84f1. Модули: *Mac Build Support (Mono)* на Mac, *Windows Build Support (Mono)* на Windows. Нужен бесплатный Unity ID (Personal-лицензия), войти в Hub |
| **git** | работа с репозиторием | обычный |
| **.NET 8 SDK** | тесты ядра без Unity (секунды) | https://dotnet.microsoft.com/download |
| Python 3 + numpy + scipy | *только* для пересборки данных рельефа (`Tools/terrain`) | обычно не нужно, готовые данные лежат в `Assets/Data/World` |

Git LFS не нужен: бинарные исходники (рельеф, текстуры, модели, всего ~95 МБ) лежат в git обычными файлами.

### Первый запуск

1. `git clone https://github.com/dsmarkin/1079-unity.git && cd 1079-unity`
2. Сгенерируй проект одним из двух способов.
   - **Через редактор:** открой папку в Unity Hub (Add → Add project from disk). При первом открытии `ProjectSetup` (с `[InitializeOnLoad]`) сам скачает пакеты, построит мир и создаст сцену. Это займёт 3–10 минут.
   - **Без окна** (редактор с этим проектом должен быть закрыт, иначе Unity откажет из-за блокировки проекта):
     - macOS: `Tools/mac/check.command`;
     - Windows: `powershell -ExecutionPolicy Bypass -File Tools\win\check.ps1`.

     Скрипт скомпилирует код, сгенерирует мир и прогонит EditMode-тесты. Результат смотри в `unity-check.log` (строка `total=… passed=… failed=…`).
3. Сыграй:
   - в редакторе: `Assets/Scenes/Main.unity` → Play → «Создать ночь»;
   - в сборке: `Tools/mac/build.command` → `Tools/mac/run.command` (Windows: `build.ps1` → `run.ps1`).

   Для игры вдвоём запусти две копии: одна «Создать ночь», вторая «Присоединиться» с адресом `127.0.0.1` или IP хоста (порт 7777).

`Assets/Resources/` и `Assets/Scenes/` генерируются и лежат в `.gitignore`. **Не коммить их и не правь руками**: при следующей генерации изменения пропадут.

## 2. Команды

```bash
# быстрые тесты ядра (без Unity, ~10 с)
cd Tools/CoreTests && dotnet run            # ждём: "# pass N fail 0"

# полная проверка: компиляция + генерация + EditMode-тесты (Unity закрыт)
Tools/mac/check.command                      # macOS → unity-check.log, unity-tests.xml
powershell -ExecutionPolicy Bypass -File Tools\win\check.ps1   # Windows

# сборка и запуск
Tools/mac/build.command && Tools/mac/run.command               # Builds/mac/1079.app, логи build.log / player.log
powershell -ExecutionPolicy Bypass -File Tools\win\build.ps1   # Builds\windows\1079.exe, лог build-win.log
powershell -ExecutionPolicy Bypass -File Tools\win\run.ps1
```

Методы для batch-режима, если вызываешь Unity сам:

- `-executeMethod Height1079.EditorTools.Builds.Mac`;
- `-executeMethod Height1079.EditorTools.Builds.Windows`;
- тесты: `-runTests -testPlatform EditMode`.

Сборка сама вызывает `ProjectSetup.EnsureAll`, отдельно генерировать мир перед ней не нужно.

Пункты меню редактора: **1079 → Rebuild world (terrain, trees, sites)** и **1079 → Generate prefabs and scene**.

Скрипты для Windows написаны по образцу мак-версий и ещё не запускались на Windows. Если что-то не так, почини и закоммить.

## 3. Архитектура

```
Assets/
  Scripts/
    Core/      Height1079.Core     — правила без UnityEngine: NightRun (ночь, исходы), SurvivalRules, WorldData (кадр мира, высоты,
                                     места событий), Sites (исторические константы), Campaign/Prologue (пролог). Гоняется в dotnet.
    Runtime/   Height1079.Runtime  — игра: Bootstrap (строит всё из кода при старте), HikerController (игрок, камера),
                                     Equipment (компас, фонари), NightSession (сеть, хост считает ночь), HudController (uGUI из кода),
                                     SnowFx/SnowTrail (снег, следы), Weather (ветер, пурга), Ambience + Synth (синтез звука),
                                     NightFilm (виньетка/зерно), MenkBrain (ИИ Менка, сервер) + MenkView (модель, анимация, звук),
                                     WorldDressing (архивный слой F2, осмотр F3), TerrainBuilder, CampSmoke.
    Editor/    Height1079.Editor   — генерация: ProjectSetup (префабы, сцена, сборки, настройки проекта),
                                     World/WorldImporter (пайплайн мира, PipelineVersion), TreeFactory, SiteFactory*.cs (реконструкции
                                     мест), ItemsFactory (предметы), CreatureFactory (Менк), PropKit/MeshBuilder/Materials/PropTextures.
    Snow/      Height1079.Snow     — снег под ногами, общий для игры и песочницы: SnowPrints (отпечатки, утоптанный снег,
                                     облачка, карта троп и правило глубины), SnowPrintArt (картинки отпечатков). Только UnityEngine.
    Night/     Height1079.Night    — ночь, общая для игры и песочницы: Weather (ветер, метель, позёмка; кто владеет ночью, задаёт
                                     Want/Live — в игре это Runtime/WeatherDriver), Synth (синтез звука), NightFilm (виньетка, зерно),
                                     CampSmoke (пламя и дым костра), MenkPuppet (скелет Менка и его позы), WindSound (ветер в ушах). Только UnityEngine.
    Puppet/    Height1079.Puppet   — новое тело по образцу PEAK (капсула на пружине ног, фигура, одежда), пока живёт в песочнице.
    Torchlight/ Height1079.Torchlight — луч фонаря (TorchBeam: свет, кука рефлектора, батарея и мерцание), общий для Equipment
                                     игры и фонарика песочницы. Только UnityEngine.
    Sandbox/   Height1079.Sandbox  — песочница: полигон, снежное поле, камера и звук шагов для Puppet; ночная площадка
                                     (SandboxHunt*, SandboxNight — docs/SANDBOX.md) на правилах Core/Hunt.cs. Видит Core, Snow,
                                     Puppet, Night; не видит Runtime.
    Steam/     Height1079.Steam    — лобби Steam, компилируется только с define STEAM_FACEPUNCH.
  Tests/EditMode/                  — NUnit. CoreTests/CampaignTests/PrologueTests — только Core; MenkTests — Runtime (только в Unity).
  Data/                            — рельеф, полог, деревья, камни, ручьи (Tools/terrain), elbrus/ — рельеф и маски Эльбруса,
                                     модель путника hiker-v1.glb.
  Art/Shaders/                     — TreeWind.shader (качание деревьев).
  Art/ThirdParty/PolyHaven/        — сканы и текстуры CC0 + SOURCES.json.
Tools/  mac/ win/ — скрипты проверки и сборки; CoreTests/ — dotnet-проект для Core; terrain/ — Python-пайплайн данных.
docs/   — дизайн и данные (см. README).
```

Главные принципы:

- **Сцена пустая, всё строится кодом.** При старте игры `Bootstrap` создаёт рельеф, освещение, сеть, HUD, звук, погоду и Менка. Не добавляй объекты в сцену руками: пиши код в `Bootstrap` или в фабрику редактора.
- **Контент генерируется в редакторе.** Модели, материалы и префабы делают фабрики в `Editor/World`. Результат лежит в `Assets/Resources/World` и грузится через `Resources.Load`.
- **Меняешь то, что генерируется в редакторе (фабрики, материалы, префабы), — подними `WorldImporter.PipelineVersion` на 1.** Иначе у соавтора останется старый сгенерированный мир: пересборка запускается, только если версия в `Assets/Resources/World/pipeline.version` отличается. Правки в `Runtime` поднимать версию не требуют.
- **Авторитет у хоста.** `NightSession` (NetworkBehaviour) на сервере ведёт `NightRun` и `MenkBrain`, а клиентам отдаёт `NetworkVariable` и RPC. Клиентские эффекты (снег, звук, анимация) опираются только на эти значения. Игрок пишет свои переменные сам (owner-writable): что держит, фонарь, направление взгляда.
- **Core не зависит от движка.** В `Assets/Scripts/Core` нельзя `using UnityEngine`. Новые правила пиши здесь и покрывай тестами: они пойдут и в Unity, и в `dotnet`.
- **Кадр мира:** x — восток, **z — север**, y — метры над уровнем моря, 1 единица = 1 м. Высота земли — `World.Ground` / `TerrainBuilder.Height`.
- **Две локации.** `World.Current` (Core) выбирается в меню до старта сессии и больше не меняется: от неё зависят кадр, сетка высот, ассет террейна и сценарий `NightRun`. Холатчахль — 4,1 км на сетке 2 м, Эльбрус — 12,3 км на сетке 6 м (`docs/ELBRUS.md`). Новый код, который берёт `WorldData.*`, должен спросить себя, что он делает на Эльбрусе.
- **Что нужно и игре, и песочнице, кладём в общую сборку**, а не копируем. Песочница (`Sandbox`) намеренно не видит `Runtime` (сеть, мир, сессия), поэтому общий код — снег под ногами, погода и звук, тело — живёт в своих сборках (`Snow`, `Night`, `Puppet`), на которые ссылаются обе. Правила без движка — в `Core` (например, `Hunt.cs`: чувства Менка на площадке).
- **Звук синтезируется** (`Synth.cs`), сэмплов в репозитории нет. Новый звук добавляй функцией в `Synth`.
- **Сторонние ассеты** кладём в `Assets/Art/ThirdParty/<источник>/`, в `SOURCES.json` пишем автора, ссылку и лицензию. Только CC0 или CC-BY, авторов CC-BY указываем в титрах. Фабрика должна работать и без скана: у каждой модели есть процедурная замена (`PlaceScan` / `SketchfabModel` возвращают null).
- **История.** Исторические места и предметы берём из документов, источник пишем в `docs/`. Реальные имена звучат только в прологе и архивном слое. Ночь на склоне проходит вымышленная пара, исходы и Менк — вымысел, и игра это прямо говорит.

## 4. Известные ловушки

- **Туман в сборке.** Сцена создаётся без тумана, поэтому Unity вырезал его варианты шейдеров, и в сборке тумана не было. `ProjectSetup.EnsureFogVariants` оставляет их явно. Новые шейдеры проверяй в *сборке*, а не только в редакторе.
- **Ночь.** Всё, что не должно светиться в темноте (частицы, дым, снег), делай на освещаемых шейдерах (`Particles/Standard Surface`, Standard). Unlit-материалы ночью горят белым.
- **Шейдер попадает в сборку**, только если на него ссылается материал в `Resources`. `Shader.Find` в рантайме без такой ссылки вернёт null.
- **Тон слоя террейна.** `TerrainLayer.diffuseRemapMin/Max` встроенный шейдер `Nature/Terrain/Standard` не читает — это поле терраин-шейдера HDRP. Тон выглядит выставленным в инспекторе и не делает ничего ни в редакторе, ни в сборке. Перекрасить скан можно только запечённой копией текстуры: `TextureFactory.Tinted` (грузит исходный JPG с диска, поэтому Read/Write на импортёре не нужен).
- **Клавиатура.** Ввод через Input System по физическим клавишам, иначе при русской раскладке на macOS WASD не работают. Используй `Controls.cs`.
- **Кривые частиц.** У `velocityOverLifetime` x, y и z должны быть в одном режиме кривой (TwoConstants и т. п.), иначе Unity ругается.
- **Неоднозначность `Object`.** В файлах с `using System` пиши `UnityEngine.Object`.
- **Нет места на диске.** Если игра стартует с дефолтным небом без мира, а в `player.log` — `Mismatched serialization in the builtin class 'TextAsset'`, сначала посмотри в `build-editor.log` строку `No space left on device`: при полном диске Unity молча дописывает обрезанные файлы, а сборка при этом может отчитаться об успехе. Чистить: `Library/` проекта (кэш Unity, пересоздаётся), `Builds/`, корзину.
- **Пустой мир в сборке.** Первая сборка после правки скриптов приносила плеер, в котором большой `TextAsset` пуст: игра стартует с дефолтным небом, в `player.log` — `Mismatched serialization in the builtin class 'TextAsset'`. Перед сборкой `ProjectSetup.EnsureData` переимпортирует все `Assets/Resources/**/*.bytes` и сверяет размер с файлом на диске.
- **Сборка всегда полная** (`BuildOptions.CleanBuildCache`), это около полуминуты, и идёт в два прохода: первая сессия Unity (`ProjectSetup.Prepare`) генерирует мир, вторая собирает плеер.
- **Процесс Unity на проект один.** Пока редактор открыт, batch-скрипты не запустятся.
- **Имя сборки не должно совпадать с типом ядра.** Сборка `Height1079.Gear` ломала Runtime: там `Gear` — enum из `Core/Ascent.cs`, а пространство имён-сосед побеждает `using Height1079.Core`. Прежде чем заводить сборку, проверь `grep -rn "enum X\|class X" Assets/Scripts/Core`.
- **Тесты с UnityEngine в dotnet не собираются.** Новый тест, которому нужен Runtime или UnityEngine, добавь в `Exclude` в `Tools/CoreTests/CoreTests.csproj`.

## 5. Как работаем вместе (коммиты)

1. Перед началом: `git pull --rebase origin main`.
2. **Небольшая правка** (опечатка, настройка, одна система): можно коммитить прямо в `main`. **Крупная фича**: ветка `feature/<коротко>` и Pull Request в `main`. Второй человек (или его агент) смотрит PR, влить может любой.
3. Перед push:
   - `cd Tools/CoreTests && dotnet run` — обязательно, `fail 0`;
   - `check`-скрипт — если трогал Unity-код, `failed="0"`;
   - сборка и игра — если менял то, что видно или слышно. Проверь в *сборке* и напиши в коммите, что проверил;
   - `git pull --rebase origin main`, затем `git push`. **Никогда не делай `push --force` в `main`.**
4. Сообщение коммита: по-английски, в повелительном наклонении, первая строка — что изменилось и зачем (до ~100 символов). Пример из истории: `Blizzard weather: swaying trees (wind shader), driving snow and ground drift, zero-visibility gusts`. Агент добавляет строку соавторства, как требует его окружение.
5. Не коммить:
   - `Library/`, `Temp/`, `Builds/`, `Logs/`, `UserSettings/`;
   - `Assets/Resources/`, `Assets/Scenes/`;
   - `*.log`, `unity-tests.xml`, файлы `*.command` в корне, `*.meta`;
   - токены и ключи.

   `.gitignore` это уже учитывает, проверяй `git status`.
6. **Документация — часть изменения.**
   - Новая механика или система — раздел в `docs/` (свет, звук, погода, существа — `docs/ATMOSPHERE.md`; предметы — `docs/ITEMS.md`; мир и места — `docs/MAP.md`; Эльбрус — `docs/ELBRUS.md`).
   - Новая клавиша — таблица управления в `README.md`.
   - Статусы задач правь в `docs/BACKLOG.md`.
7. **Конфликты.**
   - Самые «горячие» файлы: `Bootstrap.cs`, `HikerController.cs`, `NightSession.cs`, `WorldImporter.cs` (из-за `PipelineVersion`). Правь их маленькими кусками.
   - Если двое одновременно подняли `PipelineVersion`, при слиянии возьми большее значение +1.
   - **`.meta`-файлы в git не хранятся**, и это сделано намеренно. Всё сгенерированное лежит локально, код находит ассеты по пути или имени, а настройки импорта задаёт `WorldImporter` (AssetPostprocessor, например `_nor_gl` → normal map). Не коммить `.meta`. Если нужна особая настройка импорта, пропиши её в постпроцессоре.
8. Кто чем занят, договаривайтесь в PR или в `docs/BACKLOG.md`, чтобы не писать одно и то же.

## 6. Текущее состояние и открытые задачи (17.09.2026)

- Все 23 EditMode-теста проходят в Unity. В dotnet — 21 тест ядра. Сборка для Mac проверена вживую: ночь, пурга, фонари, Менк.
- Ждут входа в Sketchfab: CC-BY модели «Old flashlight» (imarcos, uid `a24209587f42466199b6f40757d21c18`), советского рюкзака, валенок, лыжного ботинка, «Зоркого», кружек. Готовые файлы класть в `Assets/Art/ThirdParty/Sketchfab/<id>/` (`ItemsFactory.SketchfabModel` найдёт glTF/GLB). Авторов указать в `SOURCES.json` и титрах.
- Менк: нужен обход стволов и склонов (сейчас прямая), анимация на скелете вместо иерархии суставов, баланс урона. Отладка: F4 и F5.
- Буквенные клавиши не доходят до игры при проверке через автоматизацию (доходят F-клавиши, Tab, цифры и стрелки). Поэтому у вида есть дубль **F7**, а всё, что делают G/R/X, продублировано кнопками в окне рюкзака.
- Не проверены: Steam-слой, сборка под Windows, скрипты `Tools/win`, а также генерация проекта с нуля из свежего клона: у Дона проект живёт в рабочей папке. Если при первом открытии что-то не сгенерировалось, запиши, что именно, и почини — это первая полезная задача.
- Бэклог механик (одна полоска сил, тропёжка, палатка вдвоём, дежурство) — `docs/BACKLOG.md`.
