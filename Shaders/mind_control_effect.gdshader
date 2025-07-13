shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_disabled, diffuse_burley, specular_schlick_ggx, unshaded;

uniform float pull_strength : hint_range(0.0, 5.0) = 1.0;
uniform float wobble_frequency : hint_range(0.1, 10.0) = 2.0;
uniform float wobble_amplitude : hint_range(0.0, 0.5) = 0.1;
uniform vec4 color_tint : source_color = vec4(0.8, 0.3, 0.9, 0.6);
uniform float distortion_scale : hint_range(0.0, 1.0) = 0.15;
uniform float time_speed : hint_range(0.1, 5.0) = 1.0;
uniform float custom_time : hint_range(0.0, 1000.0) = 0.0;

varying vec3 world_position;
varying vec3 world_normal;
varying vec3 local_position;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = (MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz;
    local_position = VERTEX;
    
    // Use custom time for controlled animation speed
    float time = custom_time * time_speed;
    vec3 distorted_vertex = VERTEX;
    
    // Create reality "tear" effect - vertices stretch and twist
    float distance_from_center = length(VERTEX);
    float tear_factor = sin(time * 3.0 + distance_from_center * 8.0) * 0.5 + 0.5;
    
    // Dimensional rift warping - creates tears in space
    float rift_angle = atan(VERTEX.z, VERTEX.x) + time * 0.5;
    float rift_intensity = sin(rift_angle * 6.0 + time * 2.0) * pull_strength * 0.1;
    
    // Apply twisting motion like reality is being pulled apart
    float twist = distance_from_center * pull_strength * 0.2;
    float cos_twist = cos(twist + time);
    float sin_twist = sin(twist + time);
    
    // Rotate vertices to create twisting dimensional effect
    distorted_vertex.x = VERTEX.x * cos_twist - VERTEX.z * sin_twist;
    distorted_vertex.z = VERTEX.x * sin_twist + VERTEX.z * cos_twist;
    
    // Add chaotic dimensional tears
    distorted_vertex += NORMAL * rift_intensity * tear_factor;
    
    VERTEX = distorted_vertex;
}

// Hash function for pseudo-random numbers
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// Fractal noise for chaotic dimensional effects
float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

float fbm(vec2 p) {
    float value = 0.0;
    float amplitude = 0.5;
    for(int i = 0; i < 4; i++) {
        value += amplitude * noise(p);
        p *= 2.0;
        amplitude *= 0.5;
    }
    return value;
}

void fragment() {
    float time = custom_time * time_speed;
    
    // Get distance from center for radial effects
    vec2 center = vec2(0.5);
    vec2 uv = UV;
    float distance_from_center = length(uv - center);
    
    // Create dimensional rift pattern
    vec2 rift_uv = uv - center;
    float rift_angle = atan(rift_uv.y, rift_uv.x);
    
    // Multiple dimensional tears radiating from center
    float tears = 0.0;
    for(float i = 0.0; i < 6.0; i++) {
        float tear_angle = (i / 6.0) * 6.28318 + time * 0.3;
        float tear_diff = abs(rift_angle - tear_angle);
        tear_diff = min(tear_diff, 6.28318 - tear_diff); // Wrap around
        tears += 1.0 / (1.0 + tear_diff * 20.0) * (0.5 + 0.5 * sin(time * 4.0 + i));
    }
    
    // Chaotic dimensional energy using fractal noise
    vec2 noise_uv = uv * 8.0 + time * 0.5;
    float chaos = fbm(noise_uv) * fbm(noise_uv + vec2(time * 0.7, time * 0.3));
    
    // Void center - darker area that seems to pull reality inward
    float void_intensity = 1.0 - smoothstep(0.0, 0.3, distance_from_center);
    void_intensity *= (0.7 + 0.3 * sin(time * 6.0));
    
    // Reality distortion rings - concentric circles that pulse outward
    float rings = sin(distance_from_center * 25.0 - time * 8.0) * 0.5 + 0.5;
    rings *= sin(distance_from_center * 40.0 + time * 5.0) * 0.5 + 0.5;
    rings = pow(rings, 2.0);
    
    // Energy lightning - crackling energy effects
    float lightning = 0.0;
    vec2 lightning_uv = uv * 15.0;
    for(float i = 0.0; i < 3.0; i++) {
        vec2 offset = vec2(sin(time * 8.0 + i * 2.0), cos(time * 6.0 + i * 1.5)) * 0.1;
        float bolt = abs(sin(lightning_uv.x + offset.x + time * 10.0)) * 
                     abs(sin(lightning_uv.y + offset.y + time * 8.0));
        lightning += pow(bolt, 8.0) * (0.5 + 0.5 * sin(time * 12.0 + i));
    }
    
    // Color shifting based on dimensional energy
    vec3 base_color = color_tint.rgb;
    
    // Shift colors based on dimensional chaos
    vec3 rift_color1 = vec3(0.9, 0.2, 0.7); // Magenta
    vec3 rift_color2 = vec3(0.2, 0.8, 0.9); // Cyan
    vec3 rift_color3 = vec3(0.8, 0.9, 0.2); // Electric yellow
    
    // Blend colors based on different effects
    vec3 final_color = base_color;
    final_color = mix(final_color, rift_color1, tears * 0.6);
    final_color = mix(final_color, rift_color2, chaos * 0.4);
    final_color = mix(final_color, rift_color3, lightning * 0.8);
    
    // Add void effect - darker center
    final_color = mix(final_color, vec3(0.1, 0.05, 0.2), void_intensity * 0.7);
    
    // Combine all effects
    float total_intensity = tears + chaos * 0.5 + rings * 0.3 + lightning + void_intensity;
    total_intensity *= pull_strength;
    
    // Edge fade for smooth blending
    float edge_fade = 1.0 - smoothstep(0.6, 1.0, distance_from_center);
    
    // Pulsing intensity
    float pulse = 0.8 + 0.2 * sin(time * 4.0);
    
    // Final calculations
    float final_alpha = color_tint.a * total_intensity * edge_fade * pulse;
    final_alpha = clamp(final_alpha, 0.0, 1.0);
    
    // Set material properties
    ALBEDO = final_color;
    ALPHA = final_alpha;
    METALLIC = 0.1;
    ROUGHNESS = 0.6;
    EMISSION = final_color * (0.5 + lightning * 0.5); // Extra glow on lightning
}

