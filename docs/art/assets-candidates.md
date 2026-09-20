# Кандидаты ассетов для стилизованного low-poly (PEAK / R.E.P.O. / Content Warning)

Дата проверки: 20.09.2026. Все URL открывались (WebFetch или публичный JSON-API Sketchfab); файлы не скачивались.
Размеры архивов Kenney взяты из HTTP-заголовка `Content-Length` (HEAD-запрос, без скачивания).

## Легенда лицензий и что можно коммитить в публичный репозиторий

| Лицензия | Можно коммитить? | Условие |
|---|---|---|
| CC0 | да | ничего не требуется (авторов всё равно пишем в `SOURCES.json`) |
| CC BY 3.0 / 4.0 | да | автор + ссылка + лицензия в `SOURCES.json` и в титрах |
| CC BY-NC, CC BY-NC-SA | **нет** | коммерческое использование запрещено |
| Sketchfab «Free Standard» | **нет** | нельзя перераспространять исходник — только локально |
| Unity Asset Store EULA (в т. ч. KayKit for Unity за $11.99) | **нет** | только локально |
| Quaternius «QAL License» (Bestiary) | **неясно** | текст лицензии не найден на странице — не считать CC |
| Meshy Free-план | да, с оговоркой | вывод принадлежит Meshy, лицензирован CC BY 4.0 → указывать «Model created with Meshy – CC BY 4.0» |
| Meshy платный / Tripo платный / Hyper3D Rodin | да | права у пользователя, лицензию (CC0/CC-BY) объявляет команда сама |
| Tripo Free-план | **нет** | по ToS 5.2.1 все права на вывод остаются у Tripo |

Общие замечания по источникам:
- **Kenney** — аккаунт не нужен, при скачивании показывается предложение донатить (можно пропустить). Форматы на страницах не перечислены; база знаний Kenney: «glTF распространяется как GLB», FBX/OBJ также есть в комплектах (на itch для Survival Kit прямо перечислены OBJ, FBX, glTF).
- **Quaternius** — прямая ссылка на quaternius.com, itch или Poly Pizza; аккаунт не нужен. «Source»-версии (Blend с исходниками) — через Patreon/Discord. Характерно: «Textured: ✗», т. е. цвет задан материалами, не текстурой.
- **KayKit** — itch «name your own price» (0 руб. доступно), GitHub-репозитории с `LICENSE.txt` CC0 1.0. Одна градиентная текстура-атлас 1024² (можно ужать до 128²): перекраска = правка одного PNG. Это ровно то, что нужно для палитры.
- **Poly Pizza** — CC0 или CC BY 3.0 у каждой модели (указано на странице), форматы FBX/OBJ + GLTF. Текст на странице поддержки: «No account required»; прямая проверка скачивания не делалась. Число треугольников в извлечённом тексте страниц не показывается.
- **Sketchfab** — для скачивания нужен бесплатный аккаунт (поведение сайта, повторно не проверялось). Фильтр лицензий в публичном API (`license=cc0`) возвращал пустой ответ, поэтому лицензия читалась из карточки каждой модели.

---

## A. Базовый человек (риг + скин)

| Название | Источник / автор | Лицензия | Форматы | Скелет / текстура | Треугольники | Архив | Аккаунт | Страница | Превью |
|---|---|---|---|---|---|---|---|---|---|
| KayKit — Character Pack: Adventurers | Kay Lousberg (itch; GitHub-зеркало 1.0) | CC0 1.0 (itch: «Creative Commons Zero v1.0 Universal»; GitHub: `LICENSE.txt` CC0) | FBX, GLTF | Полностью ригнуты и анимированы. Риг собственный KayKit «Rig_Medium»; по devlog автора — «made as humanoid rigs», в Unity ставить Humanoid (или Copy Avatar from `Rig_Medium_Avatar`). Имена костей на страницах не указаны (не Mixamo-именование, судя по отсутствию упоминаний). Одна градиентная текстура-атлас 1024² → 128². Одежда фэнтези (Knight, Barbarian, Rogue, Mage, Ranger; +Engineer, Druid, Barbarian Large за $7.95). Зимней нет, но силуэт «Ranger/Rogue» с капюшоном ближе всего | не указано | Free 12 MB; Extra 18 MB; Source 23 MB | нет (itch «name your own price»); GitHub — нет | https://kaylousberg.itch.io/kaykit-adventurers ; https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0 | https://img.itch.zone/aW1nLzIzODA4Mjg4LnBuZw==/original/WI%2FL4z.png |
| Quaternius — Ultimate Modular Men Pack | Quaternius | CC0 (quaternius.com, ссылка на CC0 1.0; Poly Pizza: «Public Domain (CC0)») | FBX, OBJ, Blend, glTF | 11 персонажей × 4 сменных части, 24 анимации. Список: Adventurer, King, Farmer, Hoodie, Beach, Casual, Worker, Punk, SWAT, Business Man, Astronaut. Без текстур (цвет материалами). Тип рига на странице Men не указан; у парного Women Pack «a humanoid rig version is also included» — предположительно то же | не указано | не указано | нет | https://quaternius.com/packs/ultimatemodularcharacters.html ; https://poly.pizza/bundle/Ultimate-Modular-Men-Pack-ZiH8muWqwQ | https://quaternius.com/assets/images/fullres/modularcharacters.jpg |
| Quaternius — Ultimate Modular Women Pack | Quaternius | CC0 | FBX, OBJ, Blend, glTF | 10 персонажей × 4 части, 24 анимации, «humanoid rig version included». Без текстур | не указано | не указано | нет | https://quaternius.com/packs/ultimatemodularwomen.html ; https://poly.pizza/bundle/Ultimate-Modular-Women-Pack-aCBDXDdTNN | https://quaternius.com/assets/images/fullres/modularwomen.jpg |
| Quaternius — Animated Men Pack / Animated Women Pack (2019) | Quaternius | CC0 | FBX, OBJ, Blend | По 4 модели, «many animations», без текстур; тип рига не указан. Одежда не описана | не указано | не указано | нет | https://quaternius.com/packs/animatedmen.html ; https://quaternius.com/packs/animatedwomen.html | https://quaternius.com/assets/images/fullres/animatedmen.jpg |
| Quaternius — Universal Base Characters | Quaternius (itch/сайт) | CC0 («Creative Commons Zero v1.0 Universal») | FBX, OBJ, Blend, glTF | 6 базовых тел (Superhero/Regular/Teen, м/ж), Humanoid-риг для ретаргета, совместим с Universal Animation Library; 20 причёсок; одежды нет (чёрное бельё). **Average 13k tris — выше лимита 10k** | ~13 000 в среднем | Standard 122 MB; Source 600 MB ($19.99) | нет для Standard (бесплатно 2 тела + 5 причёсок; полный Standard — «name your own price») | https://quaternius.com/packs/universalbasecharacters.html ; https://quaternius.itch.io/universal-base-characters | https://quaternius.com/assets/images/fullres/universalbasecharacters.jpg |
| Quaternius — Ultimate Animated Character Pack | Quaternius | CC0 | FBX, OBJ, Blend | 52 анимированных персонажа, без текстур; список и риг на странице не раскрыты | не указано | не указано | нет | https://quaternius.com/packs/ultimatedanimatedcharacter.html | https://quaternius.com/assets/images/fullres/ultimateanimatedcharacter.jpg |
| Kenney — Animated Characters Survivors (2019) / Protagonists (2020) / Retro (2022) | Kenney | CC0 | FBX (по базе знаний Kenney; на странице форматы не перечислены) | По OGA-зеркалу: «animated and rigged model including 4 skins and 3 animations» (idle, jump, run). Риг — Unity Humanoid (база знаний Kenney: Animation Type → Humanoid), анимации отдельными FBX, скины — PNG. Survivors: теги zombie/survivor. Одежда не перечислена | не указано | Survivors 0.7 MB; Protagonists 0.58 MB; Retro 0.7 MB | нет | https://kenney.nl/assets/animated-characters-survivors ; https://kenney.nl/assets/animated-characters-protagonists ; https://kenney.nl/assets/animated-characters-retro | https://kenney.nl/media/pages/assets/animated-characters-survivors/de34934a09-1674931680/preview.png |
| Kenney — Blocky Characters 2.0 (2025) | Kenney | CC0 | не указаны на странице (GLB — стандарт Kenney) | Флаг «Animation», 20 файлов; риг/кости не описаны. Кубический силуэт — грубее, чем PEAK | не указано | 2.1 MB | нет | https://kenney.nl/assets/blocky-characters | https://kenney.nl/media/pages/assets/blocky-characters/4ace046751-1749547441/preview.png |
| Kenney — Mini Characters (2024) | Kenney | CC0 | не указаны (GLB — стандарт Kenney) | Флаг «Animation», 25 файлов; риг не описан; чиби-пропорции | не указано | 2.4 MB | нет | https://kenney.nl/assets/mini-characters | https://kenney.nl/media/pages/assets/mini-characters/357d1a9452-1721210569/preview.png |
| Kenney — Modular Characters (2014) | Kenney | CC0 | не указаны | 425 файлов модульных частей; анимация/риг не заявлены | не указано | не указано | нет | https://kenney.nl/assets/modular-characters | https://kenney.nl/media/pages/assets/modular-characters/61170f5a1e-1677670330/preview.png |
| Adventurer (одиночная модель из Modular Men) | Quaternius на Poly Pizza | «Public Domain (CC0)» | FBX, GLTF | тег Animated; 33 тыс. скачиваний | не показано | не показано | нет | https://poly.pizza/m/5EGWBMpuXq | https://static.poly.pizza/bbe369ee-a686-42c7-adad-14356f5f2f15.webp |
| Adventurer | Polygonal Mind на Poly Pizza | CC0 | FBX, GLTF | риг на странице не указан | не показано | не показано | нет | https://poly.pizza/m/BCMT02FrVE | https://static.poly.pizza/a30d8f3e-c8b4-4286-a195-93d79849f687.webp |
| Lost Explorer | jeremy на Poly Pizza | CC BY 3.0 | OBJ, GLTF | риг не указан | не показано | не показано | нет | https://poly.pizza/m/2PKqCJM6dFp | https://static.poly.pizza/5e8e7321-874d-417b-868e-93994fda2a08.webp |

Не подходят / только локально: KayKit Adventurers for Unity ($11.99, Asset Store EULA — нельзя коммитить; тот же контент бесплатно на itch/GitHub под CC0). Kenney Character Assets (itch) — CC0, но помечен «currently unavailable».

**Top pick:** KayKit Adventurers (CC0, риг Humanoid-совместимый, одна текстура-палитра → перекраска одним PNG, есть GitHub-зеркало с лицензией; одежду 1959 года придётся моделировать поверх). Запасной: Quaternius Ultimate Modular Men (CC0, 11 комплектов одежды с Hoodie/Worker/Adventurer, 24 анимации, но цвет материалами, а не палитрой).

---

## B. Реквизит

### B1. Фонарь-трубка (1950-е)

| Название | Источник / автор | Лицензия | Форматы | Текстура | Треугольники | Архив | Аккаунт | Страница | Превью |
|---|---|---|---|---|---|---|---|---|---|
| Torch (Survival Pack) — по составу набора (есть отдельный «Wooden Torch») это ручной фонарь, а не факел; форму проверить глазами | Quaternius | CC0 | FBX, GLTF | материалы, без текстур | не показано | набор 53 модели (сайт) / 32 на Poly Pizza | нет | https://poly.pizza/m/WGsvr4KOZd ; набор https://quaternius.com/packs/survival.html | https://static.poly.pizza/listimg/cqt7crBOBvN1Mjp1odv2.webp (превью набора) |
| Flashlight («Low Poly style material only based flashlight») | Robert Ramsay, Poly Pizza | CC BY 3.0 | OBJ, GLTF | только материалы | не показано | — | нет | https://poly.pizza/m/bJaT8R5j3uD | https://static.poly.pizza/7db871f3-e36d-4ab7-9c39-2a3fb134a314.webp |
| Vintage flashlight («Old flashlight. Not based on any particular manufacturer») | Tuuttipingu, Sketchfab | CC BY («Author must be credited. Commercial use is allowed») | скачиваемая (glTF/исходник — Sketchfab) | текстурирован (dieselpunk), не flat | 1 710 faces | не показано | да (Sketchfab) | https://sketchfab.com/3d-models/vintage-flashlight-c72a51c719d64bc591ac9a4e169cc321 | https://media.sketchfab.com/models/c72a51c719d64bc591ac9a4e169cc321/thumbnails/d0caa5e088f545c7ab91b0bb25294166/709f74686f9348a5a08adaec62728df1.jpeg |
| Old flashlight (уже известен команде, CLAUDE.md) | imarcos (Marek Picheta), Sketchfab | CC BY | «Only .obj download for now» | PBR-запечённый, фотореалистичный — не в стиль | 1 126 faces | — | да | https://sketchfab.com/3d-models/old-flashlight-a24209587f42466199b6f40757d21c18 | https://media.sketchfab.com/models/a24209587f42466199b6f40757d21c18/thumbnails/375a4e6b2b2e462f853e43363e57ed0d/4a7d57297e264ade981787f721899f60.jpeg |
| Old Flashlight (rusty, apocalypse) | haphazrd, Sketchfab | CC BY | скачиваемая | PBR (Substance), ржавый — не в стиль | 1 174 faces | — | да | https://sketchfab.com/3d-models/old-flashlight-30dfe87c1c384781ad5630aba46a8cfc | https://media.sketchfab.com/models/30dfe87c1c384781ad5630aba46a8cfc/thumbnails/3657a09d7b1b49c69d955922770edb7d/b0362020bbff4364bebdd69ebb2413f3.jpeg |

Отброшено: «Vintage Soviet Military Flashlight» (GameDevNick, CC BY, 17 062 faces — тяжёлый), «Old soviet railroad flashlight» (CC BY-NC — нельзя), «Soviet flashlight» gradislav23 (124k faces).

**Top pick:** Quaternius «Torch» (CC0, плоские материалы, тот же набор даёт палатку/рюкзак/банки). Если форма не «трубка» — Sketchfab Vintage flashlight (CC BY, 1.7k) с заменой текстуры на плоский цвет.

### B2. Топорик

| Название | Источник / автор | Лицензия | Форматы | Текстура | Треугольники | Архив | Аккаунт | Страница | Превью |
|---|---|---|---|---|---|---|---|---|---|
| Tool Axe (Survival Kit) | Kenney | CC0 | OBJ, GLTF (Poly Pizza); в наборе OBJ/FBX/glTF | одна цветовая карта Kenney | не показано | набор 1.9 MB | нет | https://poly.pizza/m/GPE2JUkbfW ; https://kenney.nl/assets/survival-kit | https://static.poly.pizza/e7720b84-0769-4387-904b-cbb6ea32a354.webp |
| Axe Small | Quaternius | CC0 | FBX, GLTF | материалы | не показано | — | нет | https://poly.pizza/m/o54NXjRI4V | https://static.poly.pizza/654d4ee5-e217-4c23-b8e1-92e16226b21a.webp |
| Hatchet | Poly by Google | CC BY 3.0 | OBJ, GLTF | плоские цвета (Google Poly) | не показано | — | нет | https://poly.pizza/m/2DrO3g2y_g2 | https://static.poly.pizza/0c6ed1e5-469f-4555-8985-2d5112cc656a.webp |
| Hatchet [Low-Poly] | Frayseur, Sketchfab | CC BY | скачиваемая | Substance, «realistic» | 464 faces | — | да | https://sketchfab.com/3d-models/hatchet-low-poly-e5fe8edb1e674e16acdf47da7c792e86 | https://media.sketchfab.com/models/e5fe8edb1e674e16acdf47da7c792e86/thumbnails/f3888cb9be8a42a0b411cd0a3ee5cedb/e272fcb858294b4d9b4bbe573b4f7588.jpeg |

**Top pick:** Kenney Tool Axe (CC0, стиль Kenney = плоский).

### B3. Палатка-домик (брезент)

| Название | Источник / автор | Лицензия | Форматы | Текстура | Треугольники | Архив | Аккаунт | Страница | Превью |
|---|---|---|---|---|---|---|---|---|---|
| Tent / Tent Half / Tent Frame (Survival Kit) | Kenney | CC0 | OBJ, GLTF; набор OBJ/FBX/glTF | цветовая карта | не показано | набор 1.9 MB | нет | https://poly.pizza/m/LrTs3hVGXv ; https://poly.pizza/m/eUcofH38Cq ; https://poly.pizza/m/NBUHcJckRV | https://static.poly.pizza/559b06c1-dd7f-46ef-ad2a-c7a6b845b4ef.webp |
| Tent (Survival Pack) | Quaternius | CC0 | FBX, GLTF | материалы | не показано | — | нет | https://poly.pizza/m/5Q7qIrfDxA | https://static.poly.pizza/fc8d560d-91b5-439c-88bf-4cbddb7eadbb.webp |
| Tent (теги «collapsible shelter, tepee») | Jarlan Perez, Poly Pizza | CC BY 3.0 | OBJ, GLTF | плоские цвета | не показано | — | нет | https://poly.pizza/m/2tvQrMLf_tP | https://static.poly.pizza/11fa1c86-570c-4839-97a2-f95f4e83c2fe.webp |
| Stylized Tent | taczi.csaba, Sketchfab | CC BY | скачиваемая | стилизованная текстура | 5 190 faces | — | да | https://sketchfab.com/3d-models/stylized-tent-e5a32b9c07a941f78411bd1145f85fdf | https://media.sketchfab.com/models/e5a32b9c07a941f78411bd1145f85fdf/thumbnails/b0916db19bfe478ca8832e8d5b7a61b3/673e9a78eebf4dd49809a237498d8776.jpeg |

Неопределённость: форма (двускатный «домик» или купол) по тексту страниц не видна — проверить по превью. Kenney Nature Kit тоже содержит палатки («camping equipment (tent, canoe, paddle)» по OGA).

**Top pick:** Kenney Survival Kit Tent (+ Tent Frame для «раскрытой» палатки); палатка 1959 года из 4 м брезента всё равно потребует своей геометрии — использовать как заготовку.

### B4. Рюкзак (бескаркасный, брезент)

| Название | Источник / автор | Лицензия | Форматы | Текстура | Треугольники | Архив | Аккаунт | Страница | Превью |
|---|---|---|---|---|---|---|---|---|---|
| Backpack (Survival Pack) | Quaternius | CC0 | FBX, GLTF | материалы | не показано | — | нет | https://poly.pizza/m/vF7TuXCPDH ; вариант https://poly.pizza/m/2g9Jm7kvIU | https://static.poly.pizza/2eb5178b-0c98-4a0d-b0ea-ad8925525d1e.webp |
| Hiking Backpack | Voxel_dev, Poly Pizza | CC0 | FBX, GLTF | не указано | не показано | — | нет | https://poly.pizza/m/pVuHdEBRUs | https://static.poly.pizza/5fc5fe4a-d702-4d06-86c5-9bddce41cb6b.webp |
| Free Low Poly Backpack | warcool, Sketchfab | CC BY | скачиваемая | 1 текстура, 1 материал | 574 faces | — | да | https://sketchfab.com/3d-models/free-low-poly-backpack-efa2e60520404168ac83913383daccf6 | https://media.sketchfab.com/models/efa2e60520404168ac83913383daccf6/thumbnails/f0eaf1b16efe483ba4b7a2b9c2b9932f/7b12e1b758e444129e83d8be335886a2.jpeg |
| Rucksack_LowPoly_677Verts | gruffy.wright, Sketchfab | CC BY 4.0 | скачиваемая | low-poly art style | 1 260 faces | — | да | https://sketchfab.com/3d-models/rucksack-lowpoly-677verts-e4e0f970950a4aaea1d43253985f52b7 | https://media.sketchfab.com/models/e4e0f970950a4aaea1d43253985f52b7/thumbnails/62b8c684775643fcae7c68912bea9895/4a7fad2abaf04e5ebc59aa8142dbcea0.jpeg |
| Backpack & Flashlight | Don Carson, Poly Pizza | CC BY 3.0 | OBJ, GLTF | плоские цвета | не показано | — | нет | https://poly.pizza/m/aVCjXvxs6sP | https://static.poly.pizza/1558fdcf-8685-48ed-a2de-5712ed1f166f.webp |

**Top pick:** Quaternius Backpack (CC0, тот же набор). Ни у одного кандидата нет советского «абалаковского» силуэта — форму править.

### B5. Эмалированная кружка

| Название | Источник / автор | Лицензия | Форматы | Текстура | Треугольники | Архив | Аккаунт | Страница | Превью |
|---|---|---|---|---|---|---|---|---|---|
| Mug (Food Kit) / Cup | Kenney | CC0 | OBJ, GLTF; набор 4.6 MB | цветовая карта | не показано | 4.6 MB | нет | https://poly.pizza/m/fis2ugeLbn ; https://poly.pizza/m/aSF8ANEIsX ; https://kenney.nl/assets/food-kit | https://static.poly.pizza/5600ffdc-21d2-4e62-93e2-1b255738e43d.webp |
| Enamel metal mug | tab1bit0, Sketchfab | CC BY | скачиваемая | **0 текстур, 3 материала** — уже flat | 1 000 faces | — | да | https://sketchfab.com/3d-models/enamel-metal-mug-669f3a1e56a94be69ba155d6bc08c762 | https://media.sketchfab.com/models/669f3a1e56a94be69ba155d6bc08c762/thumbnails/f58977044a2b45dbbbd9b7b380db84f6/cf45b7a554bb4a5a9ac745d754970a96.jpeg |
| Soviet Enamel Mug (послевоенная, изогнутая ручка) | dustyrusty, Sketchfab | CC BY | скачиваемая | текстурирована | 3 824 faces | — | да | https://sketchfab.com/3d-models/soviet-enamel-mug-15418a900807467495ea46f574effd20 | https://media.sketchfab.com/models/15418a900807467495ea46f574effd20/thumbnails/e85b2dcf4c49490fbd0177af3fa883c0/f41df5c6590544c796a41417c31bd825.jpeg |
| Mug | Poly by Google | CC BY 3.0 | OBJ, GLTF | плоские цвета | не показано | — | нет | https://poly.pizza/m/2jVUdnj4mVP | https://static.poly.pizza/dec20800-65b1-4a52-b3fb-3ce03243bf29.webp |

**Top pick:** Enamel metal mug (tab1bit0, CC BY, 1k, без текстур — готовый flat) или Kenney Mug (CC0) с синим ободком материалом.

### B6. Деревянные лыжи и бамбуковые палки

| Название | Источник / автор | Лицензия | Форматы | Текстура | Треугольники | Архив | Аккаунт | Страница | Превью |
|---|---|---|---|---|---|---|---|---|---|
| Skis («basic skis») | Jared Justus, Poly Pizza | CC BY 3.0 | OBJ, GLTF | плоские цвета | не показано | — | нет | https://poly.pizza/m/cj0LhUMPLbp | https://static.poly.pizza/f029ddf9-9230-4822-ad73-624de1ad0222.webp |
| skis (два варианта) | apelab, Poly Pizza | CC BY 3.0 | OBJ, GLTF | плоские цвета | не показано | — | нет | https://poly.pizza/m/0lhfGJvD4dl ; https://poly.pizza/m/95I4gsdmurO | https://static.poly.pizza/f5dfe8c7-9a7f-467d-aaef-7186f63d79c7.webp |

CC0-лыж и палок не найдено ни на Poly Pizza, ни в Kenney/Quaternius/KayKit; Sketchfab выдаёт сканы музейных экспонатов (60–85k faces) и фигурки лыжников. Палки нигде отдельно не найдены.

**Top pick:** сделать самим (две доски с загнутым носком + две палки с кольцом — это 10 минут в `MeshBuilder`/Blender); как референс — Skis (Jared Justus, CC BY).

### B7. Консервная банка

| Название | Источник / автор | Лицензия | Форматы | Текстура | Треугольники | Архив | Аккаунт | Страница | Превью |
|---|---|---|---|---|---|---|---|---|---|
| Can Red / Can / Can Broken (Survival Pack) | Quaternius | CC0 | FBX, GLTF | материалы | не показано | — | нет | https://poly.pizza/m/IuoYedcdXQ (Can Red); Can и Can Broken — в бандле https://poly.pizza/bundle/Survival-Pack-XzvQPP0yWB | https://static.poly.pizza/listimg/cqt7crBOBvN1Mjp1odv2.webp |
| tin can | bobbeh, Poly Pizza | CC BY 3.0 | FBX, GLTF | не указано | не показано | — | нет | https://poly.pizza/m/onPuYPx0q7 | https://static.poly.pizza/fd443036-3eca-46e4-8342-06fd48f93e8b.webp |
| Tuna Can / Open Tuna Can | Jarlan Perez, Poly Pizza | CC BY 3.0 | OBJ, GLTF | плоские цвета | не показано | — | нет | https://poly.pizza/m/0YMEf4u04s5 ; https://poly.pizza/m/2nDIz62OirT | https://static.poly.pizza/ed83f5cf-b969-48ed-a3f1-fc614f871851.webp |
| Lowpoly tin can (red label, 512px textures + normal) | radioape, Sketchfab | CC BY | скачиваемая | текстуры 512² | 574 faces | — | да | https://sketchfab.com/3d-models/lowpoly-tin-can-82a27b5ebc9e4e72923c4b266b1f96ab | https://media.sketchfab.com/models/82a27b5ebc9e4e72923c4b266b1f96ab/thumbnails/4f806c2873f44a1d96935d08efdec21c/1f32e2f5238f4d249e7860b85e0c1843.jpeg |

**Top pick:** Quaternius Can / Can Red / Can Broken (CC0, три состояния в одном наборе).

### B8. Одеяло / спальный свёрток

| Название | Источник / автор | Лицензия | Форматы | Текстура | Треугольники | Архив | Аккаунт | Страница | Превью |
|---|---|---|---|---|---|---|---|---|---|
| Bedroll (Survival Kit, 2 варианта) | Kenney | CC0 | OBJ, GLTF; набор OBJ/FBX/glTF | цветовая карта | не показано | набор 1.9 MB | нет | https://poly.pizza/m/efwd5fjuMU ; https://poly.pizza/m/12PaVpBY04 | https://static.poly.pizza/3aa0d924-b63c-4168-a48b-d0cb603363a3.webp |
| Bed roll | Justin Randall, Poly Pizza | CC BY 3.0 | OBJ, GLTF | плоские цвета | не показано | — | нет | https://poly.pizza/m/3kczVpdqvGP | https://static.poly.pizza/b4858f11-1a86-44c0-a934-da43a1cc32c7.webp |
| Blanket and Pillows - Low Poly | antipovsoft, Sketchfab | CC BY | скачиваемая | не указано | 2 736 faces | — | да | https://sketchfab.com/3d-models/blanket-and-pillows-low-poly-65d0993e7b1741e9ab061d5f97504c31 | https://media.sketchfab.com/models/65d0993e7b1741e9ab061d5f97504c31/thumbnails/010863804b6c49b5914ac907b9ed87ed/d36ab4b7a164446f957d57d0867c9869.jpeg |

**Top pick:** Kenney Bedroll (CC0).

### B9. Валенки / высокие ботинки

| Название | Источник / автор | Лицензия | Форматы | Текстура | Треугольники | Архив | Аккаунт | Страница | Превью |
|---|---|---|---|---|---|---|---|---|---|
| Boots (жёлтые, теги Shoes/Footwear) | Isa Lousberg, Poly Pizza | CC0 | FBX, GLTF | материалы (перекрасить в серый фетр) | не показано | — | нет | https://poly.pizza/m/7XCvej7wZU | https://static.poly.pizza/8e56ff49-c0ff-40b3-bc30-9b7b2038db44.webp |
| Felt boots (Valenki) lowpoly ATOM RPG | kifir (KIFIR), Sketchfab | CC BY («Commercial use is allowed») | скачиваемая | текстурирована (игровой ассет) | 5 496 faces | — | да | https://sketchfab.com/3d-models/felt-boots-valenki-lowpoly-atom-rpg-08f0cce561aa44079cb8fa352a9daa76 | https://media.sketchfab.com/models/08f0cce561aa44079cb8fa352a9daa76/thumbnails/699fbb36232d4339bd21430261c62d8a/09e0169b01d7481882947357241a30b9.jpeg |
| Boots / Rubber boot | Poly by Google | CC BY 3.0 | OBJ, GLTF | плоские цвета | не показано | — | нет | https://poly.pizza/m/7HbqG8RwRcA ; https://poly.pizza/m/eFxqLZsis1O | https://static.poly.pizza/888317ad-20f0-4b0d-ba01-0bdd017adfd8.webp |

**Top pick:** Boots (Isa Lousberg, CC0) перекрасить; настоящие валенки — только Sketchfab (CC BY, 5.5k, текстура не в стиль).

### B10. Плёночная камера 35 мм («Зоркий»)

| Название | Источник / автор | Лицензия | Форматы | Текстура | Треугольники | Архив | Аккаунт | Страница | Превью |
|---|---|---|---|---|---|---|---|---|---|
| Leica M3 («work in progress», дальномерка — силуэт как у «Зоркого») | Damon Lam, Poly Pizza | CC BY 3.0 | OBJ, GLTF | плоские цвета | не показано | — | нет | https://poly.pizza/m/4mafzKwst8_ | https://static.poly.pizza/29d6e444-3a08-491d-957c-fa347f1e4989.webp |
| Camera (2 модели) | Poly by Google | CC BY 3.0 | OBJ, GLTF | плоские цвета | не показано | — | нет | https://poly.pizza/m/0nfSsetwy0Z ; https://poly.pizza/m/dp6b5ILj6At | https://static.poly.pizza/5816c144-82dc-4e1b-a0d9-7f3609d6887c.webp |
| Lowpoly pixelart camera | akihiko47, Sketchfab | CC BY | скачиваемая | пиксельная текстура | 1 706 faces | — | да | https://sketchfab.com/3d-models/lowpoly-pixelart-camera-a4695852d6064377a61e847fba297e67 | https://media.sketchfab.com/models/a4695852d6064377a61e847fba297e67/thumbnails/6bb80fce088144a79fe591a2077c8bb6/f94f9b208c094dfcac38594664214a21.jpeg |
| Camera Zorki-4 (референс, слишком тяжёлая) | Siromaha_Dmitry, Sketchfab | CC BY | скачиваемая | PBR | 50 080 faces | — | да | https://sketchfab.com/3d-models/camera-zorki-4-62829a25b4634d63a2ba2b0220335b3e | https://media.sketchfab.com/models/62829a25b4634d63a2ba2b0220335b3e/thumbnails/1015912c900f45da830607ce5f2d78d0/bca2fda398c24b1e8e59fce3c85b56af.jpeg |

**Top pick:** Leica M3 (Damon Lam, CC BY, плоские цвета, дальномерный силуэт); «Зоркий-4» со Sketchfab — только как референс формы.

---

## C. Зимний лес и камни

| Название | Источник / автор | Лицензия | Форматы | Состав / текстура | Треугольники | Архив | Аккаунт | Страница | Превью |
|---|---|---|---|---|---|---|---|---|---|
| Ultimate Nature Pack (150+ LowPoly Nature Models) | Quaternius | CC0 («Creative Commons Zero v1.0 Universal» на itch) | FBX, OBJ, Blend (+GLTF поштучно на Poly Pizza) | 150 моделей, теги itch: pine, tree, rock, **snowy**, autumn, palm, cactus. На Poly Pizza под CC0 от Quaternius есть именно зимний набор: Pine Tree with Snow (https://poly.pizza/m/17vQv2X5rh), Birch Tree with Snow (https://poly.pizza/m/R4NgnzZHcK), Birch Tree Dead (https://poly.pizza/m/RieYOsjDj8), Birch Tree Dead Snow (https://poly.pizza/m/kTL2WYKIPh), Dead Tree with Snow (https://poly.pizza/m/PILl2nbDNz), Willow Dead Snow (https://poly.pizza/m/CU2Oc9lzu9), Rock Snow (https://poly.pizza/m/eZRzCg5BcR), Bush Snow (https://poly.pizza/m/H4IEAwYl1z), Pine ×5 (https://poly.pizza/m/79gmlLnweB …). Принадлежность этих одиночек именно к Ultimate Nature Pack на страницах не указана (вероятно, но не подтверждено). Без текстур — материалы | не указано | 21 MB (itch) / 23 MB (OGA) | нет | https://quaternius.com/packs/ultimatenature.html ; https://quaternius.itch.io/150-lowpoly-nature-models | https://quaternius.com/assets/images/fullres/ultimatenature.jpg |
| Nature Kit | Kenney | CC0 | не перечислены на странице (GLB — стандарт Kenney; OGA упоминает OBJ) | 330 объектов: деревья (в т. ч. pine-варианты), камни, скалы, тропинки, водопады, «camping equipment (tent, canoe, paddle)». **Снежные варианты по тексту страниц не подтверждены** | не указано | 10.5 MB | нет | https://kenney.nl/assets/nature-kit | https://kenney.nl/media/pages/assets/nature-kit/2d81fc716e-1677698893/preview.png ; https://kenney.nl/media/pages/assets/nature-kit/656a90532f-1677698896/sample.png |
| Holiday Kit 2.0 | Kenney | CC0 | FBX, OBJ, GLTF (по OGA) | 100 моделей: «trees, a snow fort, toy train set, cabin elements…» — снежный форт подтверждён, снег на деревьях по тексту не подтверждён | не указано | 4.5 MB | нет | https://kenney.nl/assets/holiday-kit | https://kenney.nl/media/pages/assets/holiday-kit/9e62ea4066-1733923957/preview.png |
| Mini Forest (2026) | Kenney | CC0 | не перечислены | 20 файлов, теги forest/archer/tent/base; чиби-масштаб серии Mini | не указано | 1.1 MB | нет | https://kenney.nl/assets/mini-forest | https://kenney.nl/media/pages/assets/mini-forest/caeee17e96-1784024073/preview.png |
| KayKit — Forest Nature Pack | Kay Lousberg | CC0 | FBX, GLTF, OBJ | Free: 100+ моделей (деревья, кусты, камни, трава); Extra $9.99: 200+ уникальных / 1 588 с перекрасками + модульный рельеф; Source $14.99 (.blend). Одна градиентная текстура-атлас 1024². Виды деревьев не названы, **снежных вариантов нет** | не указано | Free 6.1 MB; Extra 81 MB; Source 94 MB | нет | https://kaylousberg.itch.io/kaykit-forest | https://img.itch.zone/aW1nLzIwNzIzMjUzLnBuZw==/original/n1fYX1.png |
| Ultimate Stylized Nature Pack | Quaternius | CC0 | FBX, OBJ, Blend, glTF | 63 модели, «seamless textures and normal maps» — текстурный, не плоский | не указано | не указано | нет | https://quaternius.com/packs/ultimatestylizednature.html | https://quaternius.com/assets/images/fullres/ultimatestylizednature.jpg |

**Top pick:** Quaternius Ultimate Nature Pack (CC0, единственный набор с ель/сосна + берёза + сухостой + снежные варианты в одном стиле), камни добрать из Kenney Nature Kit (CC0). KayKit Forest — если хочется единую текстуру-палитру с персонажем KayKit, но снег придётся делать самим.

---

## D. База для Менка (высокий гуманоид)

| Название | Источник / автор | Лицензия | Форматы | Скелет / текстура | Треугольники | Архив | Аккаунт | Страница | Превью |
|---|---|---|---|---|---|---|---|---|---|
| Giant | Quaternius, Poly Pizza (2023) | CC0 | FBX, GLTF | тег Animated; теги Monster/Character; исходный набор на странице не назван | не показано | — | нет | https://poly.pizza/m/BldaiPtyJa (рядом «Big arm» https://poly.pizza/m/KaVJET0WHx) | https://static.poly.pizza/260aff73-1409-4a45-9746-b078229d8cf3.webp |
| Ultimate Monsters (50 анимированных монстров: Yeti, Tribal, Orc, Blue Demon, Skeleton, Demon…) | Quaternius | CC0 | FBX, OBJ, Blend, glTF | attack/death/run/walk и др.; без текстур. Sketchfab-загрузка всего пака: 212.2k tris на 50 монстров ≈ **4.2k на монстра** (оценка) | ≈4 200 в среднем (оценка) | не указано | нет | https://quaternius.com/packs/ultimatemonsters.html ; Yeti https://poly.pizza/m/S1E7idPFhe ; Skeleton https://poly.pizza/m/DM4QScSmbS ; бандл https://poly.pizza/bundle/Ultimate-Monsters-Bundle-5oyGWAmOB6 | https://www.quaternius.com/assets/images/fullres/ultimatemonsters.jpg |
| KayKit — Character Pack: Skeletons | Kay Lousberg | CC0 (itch; GitHub `LICENSE` CC0 1.0) | FBX, GLTF | 4 скелета (Warrior, Rogue, Mage, Minion), полностью ригнуты, 90+ анимаций (GitHub 1.0), Humanoid-совместимый риг KayKit; градиентный атлас 1024². Худой гуманоид — хорошая основа под «сухостой» после растяжки | не указано | Free 7.7 MB; Extra 12 MB; Source 15 MB | нет | https://kaylousberg.itch.io/kaykit-skeletons ; https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0 | (на itch несколько кадров; прямой URL не извлечён) |
| Stylized Low-Poly Wendigo («emaciated, dark-skinned body», олений череп) | Phu.99 (PolyGoreStudio), Sketchfab | CC BY | скачиваемая | rigged, 1 анимация, стилизованная текстура | 3 674 faces | — | да | https://sketchfab.com/3d-models/stylized-low-poly-wendigo-e93de51dcccf4525babc4249e2c43d7c | https://media.sketchfab.com/models/e93de51dcccf4525babc4249e2c43d7c/thumbnails/3094d093bf6b429da14f2e7714156b19/837ecc29211e47d8bd08295c1c195728.jpeg |
| FREE Low-poly PSX/PS1 Wendigo/Demon (OBJ, FBX, BLEND, PNG в ZIP) | ultaragaultraultronus, Sketchfab | CC BY | OBJ, FBX, BLEND | armature + 11 анимаций | 1 326 faces | — | да | https://sketchfab.com/3d-models/free-low-poly-psxps1-wendigodemon-model-7c98d2c0a7984df2af2b5096c23e3044 | https://media.sketchfab.com/models/7c98d2c0a7984df2af2b5096c23e3044/thumbnails/4623b0136d6c457696f1973f1be73860/57377ce495204a5aa625238252db03da.jpeg |
| Troll («rigged and ready for animating») | Cristobal_Fermandois, Sketchfab | CC BY | скачиваемая | rigged, 1 анимация | 1 008 faces | — | да | https://sketchfab.com/3d-models/troll-93c68a096e5145439aaadc27669850e7 | https://media.sketchfab.com/models/93c68a096e5145439aaadc27669850e7/thumbnails/1861947f5f184118b83f2c0167e0e73f/7a4c047a409041ed9aa09d8b39e2f387.jpeg |
| Fallen Forest Guardian (treant) | AntijnvanderGun, Sketchfab | CC BY | скачиваемая | без анимации, hand-painted, 1 текстура | 5 828 faces | — | да | https://sketchfab.com/3d-models/fallen-forest-guardian-deccadeec9c445d9a4271de596fffb6f | https://media.sketchfab.com/models/deccadeec9c445d9a4271de596fffb6f/thumbnails/2ddae7508bdf443696ea495c49655ab0/c3486f4412514c4c8be66887d393097a.jpeg |

Не подходит: Quaternius «Bestiary – Dungeon Monsters Kit» (7 гуманоидов, Humanoid-риг, без анимаций) — лицензия «QAL License», текст не найден → не считать свободной. «Runescape Hill Giant» — Free Standard, нельзя коммитить.

**Top pick:** Quaternius Ultimate Monsters (CC0, готовые анимации, ~4k tris) — взять Yeti/Skeleton как риг и растянуть до 4.3 м; референс силуэта «сухой, тёмный, руки до земли» — Wendigo (Phu.99, CC BY). Учитывая, что у Менка уже есть свой скелет (`MenkPuppet`), реальнее всего — скелет KayKit/Quaternius как донор костей и анимаций, а тело своё.

---

## E. AI-генерация: Meshy, Tripo, Hyper3D (Rodin)

| Сервис | Free | Платные планы (месяц) | Low-poly / стилизация | Коммерческие права | API | Можно ли коммитить в публичный репозиторий | Ссылки |
|---|---|---|---|---|---|---|---|
| **Meshy** | $0, 100 кредитов/мес, очередь 1 задача, низкий приоритет; результат — CC BY 4.0, **правообладатель — Meshy** («Provider owns all right, title, and interest… and grants… CC BY 4.0»); нужно указывать «Model created with Meshy – CC BY 4.0» | Pro $20 (1 000 кр.), Premium $40 (3 000), Ultra $100 (8 000), Studio $70/место (пул 5 500), Enterprise. Годовые цены на странице не показаны (сторонние источники пишут о скидке — не проверено). Первый месяц −50 % | Режим Smart Topology / Low Poly (генерирует low-poly сразу), Target Polycount 100–15 000 или пресеты Low/Med/High/Ultra; Remesh до пресетов 3K/10K/30K/100K или custom от 100 faces, quad или tri | Free — CC BY 4.0 (коммерция с атрибуцией). Платные — «own their Customer Output» (приватно); публикация в Community переводит модель в CC0 | Help-центр: Free ✗, Pro и выше ✓; страница цен: «Limited API Playground access» для Free–Ultra, «Full» для Enterprise (формулировки расходятся). Документация docs.meshy.ai, REST: text/image-to-3D, remesh, rigging | Free: да, под CC BY 4.0 с атрибуцией Meshy (совместимо с политикой репо). Платный: да, команда сама объявляет CC0/CC-BY. Нельзя удалять водяные знаки/AI-метаданные (ToS) | https://www.meshy.ai/pricing ; https://help.meshy.ai/en/articles/12062933-meshy-pricing-plans-free-pro-studio-enterprise ; https://www.meshy.ai/terms-of-use ; https://help.meshy.ai/en/articles/10137554-what-is-the-ownership-of-the-generated-models ; https://www.meshy.ai/features/low-poly |
| **Tripo** (VAST) | $0, 200 кредитов/мес (≈13 моделей), 1 задача, «Public Models · Non-Commercial Use». ToS 5.2.1: **Tripo retains all rights** на Inputs и Outputs бесплатных пользователей | Pro $20 ($240/год) — 3 000 кр., private + commercial; Max $90 ($1 080/год) — 25 000 кр., Smart Low Poly; Team $110/место ($1 980/год) — 90 000 кр. (третьи стороны пишут $19.90/$89.90/$109.90 — те же цифры). Сайт www.tripo3d.ai блокирует прямой fetch (403) — цены сняты через текстовый прокси r.jina.ai | Параметры API: `smart_low_poly` («hand-crafted, clean topology style»), `quad`, `face_limit`, `geometry_quality`; в web — «Smart Low Poly» на Max. Отдельный «cartoon»-стиль на странице H-серии не указан | Free — некоммерческий, права у Tripo. Платные (ToS 5.2.2) — «Paid Users generally have all rights… publish… distribute… license»; help: «copyright of the generated models belongs entirely to you»; запрет обучать конкурирующие модели | Отдельный usage-based API: 1 кредит = $0.01; text-to-3D 10–20 кр., image-to-3D 20–30, Smart Low-poly +10, Quad +5, ретопология 10/30 | Free: **нет** (права у Tripo). Платный: да, команда объявляет лицензию сама | https://www.tripo3d.ai/pricing ; https://www.tripo3d.ai/terms ; https://www.tripo3d.ai/help/privacy-policy/can-i-use-models-commercially ; https://developers.tripo3d.ai/en/pricing ; http://developers.tripo3d.ai/en/docs/generation-text-to-model/standard |
| **Hyper3D / Rodin** | $0: «Generate for free before confirmation», оплата по результату (кредит $1.5), экспорт legacy-моделей | Creator $30 ($24/мес при годовой, $288) ≈ 60 моделей; Business $120 ($96/мес годовая, $1 152) ≈ 416 моделей, полный API; Enterprise; Education (верификация) | «Smart Low-Poly» и 21 стиль (Low Poly, Cartoon, Anime…) на Creator и Business | ToS (Rodin Output): «we will not limit your use of such Output», ограничения только по закону/третьим сторонам; сайт: «You own what you create». ChatAvatar бесплатно — только личное использование. Hyper3D не гарантирует охраноспособность вывода | Только Business и выше (120 RPM) | Да для Rodin-вывода (ограничений на распространение нет); явной открытой лицензии сервис не даёт — команда объявляет сама. Уточнить платные «broader export and usage rights» перед коммитом с бесплатного плана | https://hyper3d.ai/pricing ; https://hyper3d.ai/legal/terms |

Общая оговорка: ни один сервис не гарантирует авторско-правовую охрану сгенерированного (Hyper3D пишет это прямо). Для публичного репозитория безопасный путь — платный план (Meshy Pro $20 или Tripo Pro $20) и явная запись в `SOURCES.json`, что модель сгенерирована и выпущена командой под CC0.

**Top pick:** Meshy Pro ($20, 1 000 кредитов ≈ 50–100 моделей) — единственный, у кого есть настоящий low-poly-режим с заданным числом полигонов и quad-ретопологией под риг, а бесплатный план уже даёт коммитируемый CC BY 4.0. Tripo — второй (дешевле по API, но бесплатный вывод коммитить нельзя).
