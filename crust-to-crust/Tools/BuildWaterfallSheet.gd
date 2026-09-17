extends SceneTree

const SOURCE_PATH := "res://Textures/Generated/waterfall_sheet_v2.png"
const OUTPUT_PATH := "res://Textures/Generated/waterfall_sheet_v3.png"
const FRAME_SIZE := Vector2i(256, 512)
const SOURCE_ORIGIN := Vector2i(48, 0)
const FRAME_COUNT := 8


func _initialize() -> void:
	var source := Image.load_from_file(SOURCE_PATH)
	if source == null or source.is_empty():
		push_error("Could not load waterfall source: " + SOURCE_PATH)
		quit(1)
		return

	source.convert(Image.FORMAT_RGBA8)
	# The generated source did not respect its nominal 256 px cell boundary.
	# Crop the complete first waterfall by its real bounds before making frames.
	var base_frame := source.get_region(Rect2i(SOURCE_ORIGIN, FRAME_SIZE))
	var sheet := Image.create_empty(1024, 1024, false, Image.FORMAT_RGBA8)
	sheet.fill(Color(0, 0, 0, 0))

	for frame_index in FRAME_COUNT:
		var frame := base_frame.duplicate()
		_scroll_water_highlights(base_frame, frame, frame_index * 6)
		var destination := Vector2i(
			(frame_index % 4) * FRAME_SIZE.x,
			(frame_index / 4) * FRAME_SIZE.y
		)
		sheet.blit_rect(frame, Rect2i(Vector2i.ZERO, FRAME_SIZE), destination)

	var result := sheet.save_png(OUTPUT_PATH)
	if result != OK:
		push_error("Could not save waterfall sheet: " + str(result))
		quit(1)
		return

	print("Built stable waterfall animation: " + OUTPUT_PATH)
	quit()


func _scroll_water_highlights(base_frame: Image, frame: Image, phase: int) -> void:
	for x in FRAME_SIZE.x:
		var water_rows := PackedInt32Array()
		for y in FRAME_SIZE.y:
			if _is_water_pixel(base_frame.get_pixel(x, y), x, y):
				water_rows.append(y)

		var count := water_rows.size()
		if count < 2:
			continue

		for destination_index in count:
			var destination_y := water_rows[destination_index]
			var source_index := posmod(destination_index - phase, count)
			var source_y := water_rows[source_index]
			var moving_color := base_frame.get_pixel(x, source_y)
			# Alpha always comes from the fixed silhouette, so the outline cannot jump.
			moving_color.a = base_frame.get_pixel(x, destination_y).a
			frame.set_pixel(x, destination_y, moving_color)


func _is_water_pixel(color: Color, x: int, y: int) -> bool:
	# The complete rock outlet and the curved lip remain fixed. Motion begins
	# below them, where only the long vertical stream and its foam are present.
	var inside_stream := y >= 235
	return inside_stream \
		and color.a > 0.05 \
		and color.b > 0.24 \
		and color.b > color.r * 1.14 \
		and color.g > color.r * 1.06
