# Нові ассети

## Core Journey — 14 жовтня 2026

У цьому оновленні нові PNG не генерувалися. Використані існуючі базальт,
магмова порода, фон провалля, жеоди й три кубики; переглянуто їхній фактичний
рендер у Godot. Відмінність зон забезпечують щільність уже наявних кристалів,
плавна прозорість зі збереженням відповідності колізіям, колір і світло.
Shader CaveCrystal зберігає індивідуальну прозорість спрайтів через vertex tint
і враховує налаштування спалахів. Нові декоративні сліди малюються існуючим
WindTrails із трьома додатковими палітрами без растрових ресурсів.

Знак ривка — текстовий UI без затемнення, прив'язаний до видимого краю екрана.
Написи HUD та меню залишаються живим текстом. Скриншоти перевірки —
`crust-to-crust/artifacts/zone-*.png`, `edge-warning.png`, `shop-trails.png`
та `narrow-menu.png`; це результати рендера, а не нові ігрові ассети.

Інструмент: вбудований imagegen. PNG скопійовані до `crust-to-crust/Textures/Generated/`.
Використано alpha генератора, без ручного домальовування чи процедурних текстур.

- `hero_ice.png`: cosmetic variant of the referenced base cube, front-facing square stone with subtle bevel, frosted cyan quartz veins, silver mineral inclusions, dark blue stone, identical silhouette/framing, transparent padding, no face/limbs/text.
- `hero_ember.png`: cosmetic variant of the referenced base cube, charcoal volcanic obsidian with warm amber gold fissures and bronze corners, matching silhouette/framing, transparent background, no face/limbs/text.
- `flashlight_cone.png`: downward monochrome soft light mask, apex at exact square center, transparent top half, smoothly feathered sides, distance fade, no circular halo or torch. Refined with imagegen to soften rough edges.
- `cave_magma_painted.png`: edit of the referenced dark basalt wall, preserving rock formations and framing, dark burgundy hot stone with orange-red magma inside existing cracks; charcoal rock remains dominant, no objects/text.

Попередні намальовані ассети: `crystal_cyan_painted.png`, `crystal_amber_painted.png`,
`light_halo_painted.png`, `flame_painted.png`, `cave_basalt_painted.png`.
Промпти попереднього листа: painterly dark subterranean sprite sheet, cyan and amber
quartz clusters on basalt, soft light halo, orange flame, four equal cells, transparent background.
Промпт базальту: hand-painted front-on dark basalt surface, recessed cracks,
blue-teal reflected light and painterly shadows, no objects/text.

Колір статичних кристалів змінюється шейдером без заміни силуету. Основний вогонь
формується процедурним шейдером `Shaders/Burning.gdshader`; це окремо запитана зміна.
`Audio/wind.wav` — синтезований періодичний шум із частотним фільтром, без зовнішніх семплів.

## Core Rush / Relics — imagegen

Усі файли у `crust-to-crust/Textures/Generated/`, справжня прозорість PNG збережена.
- `crystal_geode_v4.png`: asymmetrical luminous turquoise fractured quartz geode,
  hollow black basalt heart, curved branching mineral blades, bright vein network,
  painterly detailed isolated cave sprite, transparent background.
- `waterfall_v4.png`: portrait 1:2 hand-painted blue vertical water stream,
  small basalt source on upper left, foam highlights, soft fading mist, no atlas seams,
  transparent background; рух відблисків додає шейдер.
- `shield_obsidian_v4.png`: hollow circular frame of six floating obsidian armor plates,
  turquoise quartz seams, transparent empty center, mineral aesthetic, no text.
- `magma_wheel_v4.png`: irregular spiky fractured obsidian wheel with molten core,
  eight asymmetric dangerous blades, orange-red cracks, transparent background.
- `core_chasm_v4.png`: hot front-on volcanic chasm background with distant red rock,
  molten orange-yellow rivers, embers, dark readable center, no objects or text.

Інструмент створення — вбудований imagegen. Ассети не генеруються процедурно під час гри.

## ArtRefresh — 14.10.2026
Текстури з чату «Створи промпт для нової графіки» (01a13970-7bc1-7f23-8133-648a32f580ed) підключено через GameAssets, SaveData та сцени. Папка: crust-to-crust/Textures/ArtRefresh. Структура збережень і колізії скінів не змінені.
Стіни cave_basalt/cave_magma, фони crystal_chasm/core_chasm, скіни hero_basalt/ice/ember, кристали crystal_cyan, уламки basalt_rock/obsidian_shard, magma_wheel, shield_obsidian, waterfall, light_halo, flashlight_cone та flame використовуються у грі. Монети Gold_1–3 утворюють цикл 1–2–3–2. crystal_amber залишається запасним варіантом.

thermal_capsule.png — новий PNG 1024×1536 зі справжньою прозорістю. Термокапсула старого бурового обладнання із сухим порошковим струменем, для магмових зон та ядра. Масштаб у грі: 225×338 px, віддзеркалення біля правої стіни. Пул Water лишається для сумісності; змінюється спрайт, а охолодження та захист діють як раніше. Порошок рухає шейдер WaterfallV4 у powder_mode. Текст інтерфейсу не запечений.
Промпт imagegen: “One orthographic painted 2D wall-mounted abandoned drilling thermal extinguisher capsule, dark ceramic insulated canister, chipped charcoal metal braces, amber status light, downward nozzle, long narrow dry pale ivory ceramic powder and inert grey gas plume, transparent background, 1024x1536. Readable at225px width. No liquid, water, ice, text, UI, background or ground.”
Логіка світу: герметичний теплоізольований картридж має обмежений запас поглинача тепла та негорючого порошку; контакт гасить полум’я й тимчасово ізолює кубик від жару. Це художнє припущення світу гри, а не природний водоспад у ядрі.
