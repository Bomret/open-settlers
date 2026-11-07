// Draws an object sprite from palette indices (ADR 0007). A vertex carries the pixel inside its sprite in UV and,
// in COLOR, the sprite's layer as SpriteLayer encodes it: the low byte in red, the high byte in green. Blue picks the
// array: 0 the sprites (indices, coverage), 1 the shadows (shadow_indices, shadow_coverage). The coverage is its own array texture: a pixel is
// transparent where its coverage is 0 and never because its palette index is 0, which is a real color
// (docs/rules/map-objects.md). The palette texture turns the index into a color.
shader_type canvas_item;

uniform sampler2DArray indices : filter_nearest;
uniform sampler2DArray coverage : filter_nearest;
uniform sampler2DArray shadow_indices : filter_nearest;
uniform sampler2DArray shadow_coverage : filter_nearest;
uniform sampler2D palette : filter_nearest;

varying flat float layer;
varying flat float shadow;

void vertex() {
	// The low byte of the layer in red, the high byte in green (SpriteLayer).
	layer = round(COLOR.r * 255.0) + 256.0 * round(COLOR.g * 255.0);
	shadow = COLOR.b;
}

void fragment() {
	ivec2 pixel = ivec2(floor(UV));
	ivec3 texel = ivec3(pixel.x, pixel.y, int(layer));
	bool from_shadows = shadow > 0.5;
	float covered = from_shadows ? texelFetch(shadow_coverage, texel, 0).r : texelFetch(coverage, texel, 0).r;
	if (covered < 0.5) {
		discard;
	}
	float index = from_shadows ? texelFetch(shadow_indices, texel, 0).r : texelFetch(indices, texel, 0).r;
	COLOR = vec4(texelFetch(palette, ivec2(int(round(index * 255.0)), 0), 0).rgb, 1.0);
}
