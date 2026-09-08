shader_type canvas_item;

// Напівпрозора чиста темна вода
uniform vec4 base_water_color : source_color = vec4(0.02, 0.28, 0.24, 0.40);
uniform vec4 wave_tint : source_color = vec4(0.08, 0.52, 0.45, 0.55);

void fragment() {
	vec2 pos = VERTEX;

	// Плавні безперервні горизонтальні хвилі без кружечків/точок
	float w1 = sin(pos.y * 0.015 - TIME * 3.5);
	float w2 = sin(pos.y * 0.035 - TIME * 5.0 + pos.x * 0.01);
	float ripples = (w1 + w2) * 0.5;

	// М'який градієнт води з хвилями
	float wave_factor = clamp(ripples * 0.5 + 0.5, 0.0, 1.0);
	vec4 final_color = mix(base_water_color, wave_tint, wave_factor * 0.4);

	// Тонкі світлові лінії хвиль на поверхні води
	if (ripples > 0.70) {
		float crest = (ripples - 0.70) / 0.30;
		final_color = mix(final_color, vec4(0.35, 0.85, 0.75, 0.65), crest * 0.5);
	}

	COLOR = final_color;
}
