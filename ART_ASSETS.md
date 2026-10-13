# Нові ассети

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
