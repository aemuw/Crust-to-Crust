# Momentum / cave hazards Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Зберігати темп між прольотами, додати тематичних стрільців і персонажів, оновити меню та прибрати неправильні тіні й стики фону.

**Architecture:** Зберегти Godot-сцени, директор зустрічей і пули. Винести накопичення швидкості, каталог персонажів і стрільців у невеликі окремі компоненти; генератор керує їхнім життєвим циклом. Використати неперервні координати фону та контури тіней за силуетами.

**Tech Stack:** Godot 4.7.2 .NET, C#, .NET 8, canvas shaders, наявні smoke-сцени.

**Spec:** `docs/superpowers/specs/2026-10-14-momentum-hazards-design.md`

## Global Constraints

- Наступний старт: `max(максимум прольоту / коефіцієнт, попередня стартова швидкість + 60)`.
- Початковий кандидат коефіцієнта — 2.4; порівняти з 2, 3 і 4 на однакових маршрутах.
- У межах активного прольоту швидкість не падає. Анімація розвороту має окремий стан.
- При новому забігу скинути накопичення; при паузі й переході до нового кола зберігати його.
- Старі покупки, вибір і рекорди зберігаються.
- Генератор не поєднує залп із перекриттям усіх доступних шляхів.
- Перенесення секції пулу не змінює фазу зображення.
- Ігровий текст українською; меню й магазин працюють у вузькому вікні та з геймпадом.

## Review Focus

1. Повторний старт після низького максимуму або анімації з нульовою швидкістю: приріст старту не губиться (Task 1).
2. Висока швидкість і великий крок кадру: маршрут досяжний, снаряд не проходить крізь персонажа (Tasks 1, 4).
3. Старе збереження або невідомий ID скіна: безпечний базовий вибір без втрати покупок (Task 3).
4. Переповнений пул, пауза, ядро та виліт: немає прихованих снарядів і зсуву випадкових рішень (Task 4).
5. Перенесення секцій і перехід біому: немає спалаху фону, шва чи розриву тіні (Task 2).

## Порядок і файли

1. `RunMomentum.cs`: швидкість і наступний старт; інтеграція в `LevelGenerator.cs`, `RunDirector.cs`.
2. `CaveLighting.cs`, `CaveRock.gdshader`, `CoreBackdrop.gdshader`, `Obstacle.cs`: координати фону й контури тіней.
3. `CharacterCatalog.cs`, `Player.cs`, `SaveData.cs`, `SafePassage.cs`, нові PNG: персонажі та колізії.
4. `WallShooter.cs`, `HazardProjectile.cs`, `HazardEncounterRules.cs`, `LevelGenerator.cs`, `Obstacle.cs`, `RunDirector.cs`, `GameAssets.cs`, нові PNG: стріляючі зустрічі й рух перешкод.
5. `Hud.cs`: меню, налаштування, магазин і завершення.
6. `Tools/*Smoke.cs`, `Tools/*Smoke.tscn`, `README.md`, `ART_ASSETS.md`: інтеграційні перевірки й результати.

### Task 1: Накопичення швидкості

**Files:** Create `crust-to-crust/Scripts/RunMomentum.cs`; modify `Scripts/LevelGenerator.cs`, `Scripts/RunDirector.cs`, `Tools/ProgressionSmoke.cs`; create `Tools/MomentumSmoke.cs`, `Tools/MomentumSmoke.tscn` (усі шляхи від `crust-to-crust/`).

**Interfaces:**
- `RunMomentum(float initialSpeed, float baseLimit, float divisor)`; `float StartSpeed`, `float PeakSpeed`, `float SpeedLimit`.
- `void Observe(float speed)`, `float NextTraversal()`, `void Reset()`.
- `LevelGenerator`: read-only `TraversalStartSpeed`, `TraversalPeakSpeed`; exported `ReturnSpeedDivisor = 2.4f`.
- `RunDirector.LaneAt(float distance, float wavelengthScale = 1f)`: плавний маршрут; накопичене масштабування координати змінюється без стрибка при зміні швидкості.

- [x] Написати `MomentumSmoke`: 340→peak800→start400 при ÷2; 340→peak800→start≈333.33 при ÷2.4 має дати 400 через гарантію +60; peak1200 після старту400→start500. Peak500 після старту500→start560. `Reset()` повертає340. Перевірити коефіцієнти 2/2.4/3/4, нечислові/недодатні значення й збереження максимуму перед cinematic.
- [x] Запустити сцену до реалізації; очікується помилка відсутнього `RunMomentum` або невиконана умова нового старту.
- [x] Реалізувати накопичення; зберегти peak перед `BeginEmergence()`, прибрати обидва старі присвоєння старту при rebasing, використовувати один перехід для шляхів із Player і без нього. Ліміт: `max(baseLimit, StartSpeed * divisor + 120)` із поточним множником складності. Перевіряти скінченність усіх параметрів.
- [x] Обмежити швидкість руху центра проходу до 60% `Player.HorizontalSpeed`, збільшуючи довжину хвилі через неперервний накопичувач координати. Не використовувати `distance / поточний scale`, що перескочить при зміні scale.
- [x] `dotnet build crust-to-crust/Crust-to-Crust.csproj`; `MomentumSmoke`, `ProgressionSmoke`, `SafePassageSmoke`: всі PASS, без ERROR. Зберегти таблицю швидкостей для кожного коефіцієнта й трьох seed; відділити автоматичні виміри від людської оцінки.
- [x] Зафіксувати перевірену зміну окремим комітом, якщо Git-запис доступний.

### Task 2: Неперервний фон і тіні

**Files:** Modify `crust-to-crust/Scripts/CaveLighting.cs`, `Scripts/LevelGenerator.cs`, `Scripts/Obstacle.cs`, `Shaders/CaveRock.gdshader`, `Shaders/CoreBackdrop.gdshader`, `Tools/VisualSmoke.cs`; create `Scripts/HazardSilhouettes.cs`.

**Interfaces:**
- `HazardSilhouettes.Outline(int variant) -> Vector2[]`: контур у локальних ігрових координатах варіанта, під фактичну текстуру.
- Shader uniforms: `texture_origin` (vec2), `texture_period` (vec2), `warmth` (float); origin обчислюється з логічної позиції секції, а не її індекса пулу.

- [x] Додати перевірки `VisualSmoke`: суміжні секції вибирають однакові UV на спільній границі; перенесення секції на 3600 не змінює видимий фрагмент. Capture холодного/гарячого фону перед і після перенесення. Контури варіантів 0/1/3/4/5 різні, не перевищують видимого силуету.
- [x] Запустити перевірку на старому коді й зафіксувати різні UV та квадратний occluder.
- [x] Перевести фон і стіни на спільну координату за відстанню; змішувати неперіодичні краї текстури в обох осях. Просторово плавно обчислювати warmth без незалежних стрибків секцій. Зберегти краї проходу й освітлення Godot.
- [x] Створити контури за реальними текстурами, синхронізувати rotation/scale; переглянути статичні виступи. Не обрізати фізично природну довжину тіні. При пулінгу скидати старий контур.
- [x] Зібрати проєкт; запустити `VisualSmoke`, `CaveSmoke`; переглянути знімки ліхтаря під різними кутами й стики під час прокрутки. Очікується неперервність текстури й відповідність тіней силуетам.
- [x] Зафіксувати перевірену зміну окремим комітом, якщо Git-запис доступний.

### Task 3: Каталог персонажів і колізії

**Files:** Create `crust-to-crust/Scripts/CharacterCatalog.cs`; modify `Scripts/Player.cs`, `Scripts/SaveData.cs`, `Scripts/SafePassage.cs`, `Scripts/LevelGenerator.cs`, `Tools/ShopSmoke.cs`, `Tools/SafePassageSmoke.cs`; create `Textures/ArtRefresh/hero_miner.png`, `hero_drillbot.png`, `hero_golem.png`.

**Interfaces:**
- `CharacterDefinition`: `int Id`, `string Name`, `string TexturePath`, `int Price`, `float DisplayHeight`, `float EnvelopeRadius`; `Shape2D CreateBodyShape()`.
- `CharacterCatalog.Count`, `CharacterCatalog.Get(int id)` з fallback на ID0; ID0/1/2 збережені, нові ID3/4/5.
- `Player.CollisionEnvelopeRadius` враховує rotation й максимальне анімоване розтягнення. `ApplySkin()` застосовує однакову геометрію до тіла та Hitbox.
- `SafePassage.TryBounds(..., float playerRadius = PlayerRadius)` дозволяє наявним callers працювати; генератор передає фактичний радіус персонажа.

- [x] Додати перевірки: старі ID і ціни0/24/36 незмінні; ID3/4/5 підтримують покупку/вибір/reload; невідомий ID повертає базовий скін. Для кожного персонажа тіло й Hitbox узгоджені, їхні крайні точки при повороті вкладаються в заявлений envelope.
- [x] Запустити тести до реалізації: нові ID недоступні, форми відсутні.
- [x] Згенерувати три намальовані прозорі PNG через imagegen із єдиним стилем існуючої печери; переглянути їх до інтеграції. Задати ціни нових персонажів 48/60/72 монети; силуети шахтаря/робота — капсули, голема — опуклий багатокутник, під фактичний малюнок. Вибір не змінює здібності.
- [x] Реалізувати каталог і читання старого save; оновити Player та passage. Обертання й ефекти не повинні лишати колізію далеко від видимого тіла; декоративні руки не розширюють hitbox несподівано.
- [x] Збірка; `ShopSmoke`, `SafePassageSmoke`, `GameplaySmoke`: PASS. Переглянути контакти кожного силуету біля стін, у повороті та під щитом.
- [x] Зафіксувати перевірену зміну окремим комітом, якщо Git-запис доступний.

### Task 4: Стрільці та активніші перешкоди

**Files:** Create `crust-to-crust/Scripts/WallShooter.cs`, `Scripts/HazardProjectile.cs`, `Scripts/HazardEncounterRules.cs`, `Tools/ShooterSmoke.cs`, `Tools/ShooterSmoke.tscn`; modify `Scripts/LevelGenerator.cs`, `Scripts/RunDirector.cs`, `Scripts/Obstacle.cs`, `Scripts/GameAssets.cs`; create `Textures/ArtRefresh/shooter_drill.png`, `shooter_shell.png`, `projectile_bolt.png`, `projectile_ember.png`.

**Interfaces:**
- `WallShooter.Activate(LevelGenerator level, int variant, int side, float y)`, `void Deactivate()`; прив'язка X до `GetRockBounds(y)`.
- `HazardProjectile.Activate(Vector2 position, Vector2 velocity, int variant, LevelGenerator level)`, `void Deactivate()`; Area2D у групі Obstacle, collision_layer2.
- `HazardEncounterRules.CanFire(...)`: перевіряє можливість досягти вільного проміжку з фактичним envelope й горизонтальною швидкістю. Нездійсненний залп відкладається або пропускається.
- LevelGenerator створює пули 6 стрільців і 24 снарядів до забігу; не створює ресурси під час появи. Снаряди рухаються як власна velocity мінус вертикальна прокрутка світу.

- [x] `ShooterSmoke`: фіксація цілі після0.9с; одиночний болт і віяло−22°/0°/+22°; проміжок для ухиляння з усіма активними hazards; переповнення пулу без allocation/зсуву RNG; пауза заморожує заряд і снаряд; ядро/виліт/смерть очищують пули; щит поглинає попадання. Перевірити segment sweep на високій швидкості, а не лише overlap кінцевої точки.
- [x] Запустити до реалізації й зафіксувати відсутній shooter-сценарій.
- [x] Згенерувати PNG у стилі гри з видимими кріпленнями/корінням, переглянути. Прогріти текстури й колізії в GameAssets. Реалізувати поведінку й попередження у видимій частині екрана.
- [x] Додати зустріч зі стрільцем до seeded director; перший не раніше18с активної гри. Блокувати появу в навчанні, перепочинку біля ядра й охолодженні; не вести вогонь за межами видимості. Небезпечні комбінації відкидати до активації.
- [x] Посилити рух у наявних варіантів: маятникові уламки, відскоки коліс, повторний ривок лише з новим попередженням. Зберегти `ProtectPassage` після кожного руху й час на реакцію зі зростанням темпу.
- [x] Збірка; `ShooterSmoke`, `CoreBonusSmoke`, `SafePassageSmoke`, `JourneySmoke` seed17/42/12345: PASS. Переглянути кріплення, попередження, проміжки між пулями й кілька комбінованих зустрічей.
- [x] Зафіксувати перевірену зміну окремим комітом, якщо Git-запис доступний.

### Task 5: Лаконічні меню

**Files:** Modify `crust-to-crust/Scripts/Hud.cs`, `Tools/ShopSmoke.cs`, `Tools/VisualSmoke.cs`.

**Interfaces:** Зберегти `StartRun()`, `ShowGameOver(DeathCause)`, `Control/StartMenuPanel/StartButton`, `Control/GameOverPanel/RestartButton` для існуючих smoke-сцен. `_skinItems` має `CharacterCatalog.Count`, не3. Окрема панель налаштувань відкривається з меню й паузи та повертає focus попередньому екрану.

- [x] Додати UI-перевірки: усі6 персонажів доступні в каталозі; focus проходить кнопки; на вузькому viewport кнопки не поза екраном, магазин прокручується; закриття налаштувань із паузи не відновлює гру.
- [x] Запустити на старому коді: каталог обмежений3, окремої панелі налаштувань немає.
- [x] Головний екран: назва, персонаж на краю печери, «Грати», «Персонажі», «Налаштування», рекорд; прибрати слогани та chapter. Seed/навчання/керування перенести в додаткові параметри. Пауза й завершення за погодженим spec. Каталог і спорядження — різні вкладки, один великий preview, одна дія вибору/купівлі.
- [x] Збірка; `ShopSmoke`, `GameplaySmoke`, `VisualSmoke`: PASS. Переглянути головний екран, магазин, налаштування, паузу й завершення в стандартному та вузькому вікні.
- [x] Зафіксувати перевірену зміну окремим комітом, якщо Git-запис доступний.

### Task 6: Перевірка всього оновлення

**Files:** Modify `README.md`, `ART_ASSETS.md`; update smoke-сцени лише коли старе очікування суперечить погодженому дизайну.

- [x] Визначити доступний Godot .NET та виконати `dotnet build crust-to-crust/Crust-to-Crust.csproj`.
- [x] Запустити через `crust-to-crust/Tools/Run-Smoke.ps1 -Godot <verified-path> -Scenes MomentumSmoke,ShooterSmoke,GameplaySmoke,CaveSmoke,ProgressionSmoke,ShopSmoke,SafePassageSmoke,CoreBonusSmoke,VisualSmoke,JourneySmoke`; runner має відновити реальне збереження. Headless використовувати для логіки; графічні перевірки виконувати з рендером. Всі сцени завершуються0 й без ERROR.
- [x] Перевірити мінімум три повернення на кожному seed17/42/12345; записати старти й максимуми, коефіцієнт і обмеження автоматичного керування. Переглянути отримані PNG; виправити виявлені шви, невідповідності тіней і UI.
- [x] Оновити README фактичною поведінкою та результатами. ART_ASSETS містить нові файли й промпти. Не заявляти людський playtest, фізичний геймпад або FPS-профілювання, якщо їх не було.
- [x] Провести незалежний огляд інтеграції погодженим способом; виправити підтверджені проблеми та повторити тільки відповідні перевірки.
- [x] Надати користувачеві зміни, фактичні результати тестів, коефіцієнт і матеріальні обмеження. Публікація, push і PR — лише за окремим запитом.
