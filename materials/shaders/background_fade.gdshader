// Background fade shader.
// Renders a texture unshaded with a smooth vertical alpha falloff at the
// top and bottom UV edges. Section backgrounds stretched beyond their
// section bounds use this to crossfade with neighbors.

shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;

uniform sampler2D albedo_texture : source_color, filter_linear;
// Size of the fade band as a fraction of the mesh. Keep this ≈ 2× the
// per-side overscan ratio so neighbors meet at alpha ≈ 0.5 at the seam and
// full-opacity content only shrinks inside each section's original area.
uniform float fade_edge : hint_range(0.0, 0.5) = 0.13;

void fragment() {
	vec4 tex = texture(albedo_texture, UV);
	float top = smoothstep(0.0, fade_edge, UV.y);
	float bot = smoothstep(0.0, fade_edge, 1.0 - UV.y);
	ALBEDO = tex.rgb;
	ALPHA = tex.a * top * bot;
}
