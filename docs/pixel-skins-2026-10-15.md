# Піксельні скіни та майстерня — 15.10.2026

27 спрайтів у `crust-to-crust/Textures/PixelSkins/`. Джерело — вбудований imagegen,
п’ять аркушів з прозорістю. Перші чотири — схвалені прев’ю з попереднього запиту.
Оригінали ArtRefresh залишені як джерела, каталог гри використовує PixelSkins.

## Набір промптів

Спільні вимоги до перших чотирьох аркушів: style-transfer на наданих PNG,
зберегти форму, матеріал, колір і головний візерунок кожного скіна. Front-facing,
orthographic. Authentic chunky pixel art, approximately 48 logical pixels on the
longest edge, sharp nearest-neighbor enlargement, stepped silhouettes, deliberate
pixel clusters, dark outlines, limited flat colors, 2–3 shade levels. No photographic
grain, smooth gradients, antialiasing, blur, soft external glow, shadows, text, UI,
faces, limbs or new accessories. Separate sprites, generous transparent padding.

1. П’ять квадратів: cracked dark basalt; basalt with icy cyan veins; black obsidian
   with thin golden ember seams; copper mechanism with brass gears, cyan core and
   dark corner plates; purple amethyst with large embedded crystal facets.
2. Magma basalt square with broad orange fissures; black obsidian circle with
   turquoise crystal veins; copper/brass gear circle with cyan core and rim insets;
   pale moon opal circle with blue/lavender diagonal bands and silver seams.
3. Upright triangular basalt with cyan veins; triangular black rock with amber
   crystal insets; triangular icy blue rock; wide copper mechanism bar with cyan
   core; wide jade bar with gold seams. Preserve bar proportions.
4. Tall narrow cobalt diamond with dark silver frame and corner ornaments; red
   garnet diamond with black frame; flat-top bronze hexagon with turquoise insets
   and radial ribs; flat-top malachite hexagon with concentric green bands and
   copper seams.
5. New nine-sprite sheet, 3×3: row one upright convex pentagons (basalt/cyan veins,
   amethyst/lavender veins, copper/brass mechanism/cyan core); row two octagons
   (obsidian/magma, icy quartz/silver rim, pale opal/lavender/cyan bands); row three
   wide short trapezoids, short top/wide base (basalt/gold cracks, jade/gold veins,
   copper mechanism/cyan horizontal core). Same pixel style and constraints.
   Existing generated sheets supplied as style references, not edit targets.

П’ятий аркуш пройшов додатковий imagegen background-extraction:
“Remove ONLY ALL the opaque dark background and all the colored glow halos outside
the nine geometric objects, replacing every background pixel with genuine full
alpha transparency. Keep the nine sprites themselves intact, exact same 3x3 layout,
size, shape, material color, pixel art design, black outer contour and markings.”

## Підготовка до гри

`Tools/PreparePixelSkins.cjs` розділяє PNG за альфа-каналом, обрізає прозорі поля,
експортує до 48 px по довшій стороні через nearest-neighbor, зводить палітру до
32 кольорів без dithering, нормалізує альфу до 0/255 і додає 1 px прозорого поля.
Для трьох трикутників, що торкаються основами на аркуші, задані окремі області.
Використано Node.js/sharp з комплектного runtime, без нових залежностей гри.
Godot імпортує без втрат і mipmaps; персонаж, меню й магазин використовують Nearest.

ID 0–17 та ціни на попередні скіни збережені. Нові ID 18–26. П’ятикутник відкривається
за 1300, восьмикутник за 1600, трапеція за 1200 монет; варіанти коштують 450/650.
Маска володіння лишається сумісною з попередніми збереженнями й вміщує всі 27 ID.
Нові колізії — опуклі багатокутники з 5, 8 і 4 вершинами; contact/body мають спільну форму.

Магазин: той самий поділ на форми, спорядження та сліди, кодові кам’яні панелі,
мідна рамка, бірюзові акценти вибору, стенд прев’ю, ціни й стани володіння на картках.
Декор малюється `WorkshopBackdrop`, тема обмежена магазином.

## Перевірки

`PixelShopSmoke`: міграція старих покупок, блокування варіантів до відкриття форми,
купівля й повторний вибір, збереження всіх 27 ID, розміри текстур, нові колізії,
контакт зі снарядами, фільтр форм, закриті стани та доступність покупки й відкриття
у вузькому вікні. Знімки — `crust-to-crust/artifacts/pixel-*.png`.
Також: ShopSmoke, MenuSmoke, PolishSmoke, FeatureSmoke, EquipmentSmoke, PresentationSmoke.
