// Lava vein overlay: replicates the original veins_lava.tres (albedo + emission,
// matte) but with `specular_disabled` so the highlights no longer slide across
// the mesh as the camera moves. Lighting is kept enabled so the scene's
// DirectionalLight3D still picks out the vein bumps from the recessed
// surrounding geometry — the "area around the veins" stays dark, not tinted.
// A world-Y alpha falloff fades the whole overlay near the top/bottom of the
// owning section so neighbor sections don't seam.

shader_type spatial;
render_mode blend_mix, depth_draw_never, specular_disabled;

uniform vec4 albedo_color : source_color = vec4(0.634834, 0.108211, 0.0, 1.0);
uniform vec4 emission_color : source_color = vec4(0.609048, 0.10211, 0.0, 1.0);
uniform float world_y_center = 0.0;
uniform float section_half_height = 12.5;
uniform float fade_band = 3.0;

varying float v_world_y;

void vertex() {
	v_world_y = (MODEL_MATRIX * vec4(VERTEX, 1.0)).y;
}

void fragment() {
	float dy = abs(v_world_y - world_y_center);
	float fade = 1.0 - smoothstep(section_half_height - fade_band, section_half_height, dy);
	ALBEDO = albedo_color.rgb;
	EMISSION = emission_color.rgb;
	ROUGHNESS = 1.0;
	METALLIC = 0.0;
	ALPHA = fade;
}
