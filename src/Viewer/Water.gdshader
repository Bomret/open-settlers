// Draws a water sprite (entries 630 to 645) only where the surface under it shows palette index 8
// (docs/rules/graphics-images.md, "Sprites drawn over color 8"). The surface is the screen copy under the sprite, so
// a pixel counts as index 8 when its color is that of palette index 8. Otherwise as Objects.gdshader: the layer in
// COLOR.r and COLOR.g, the pixel inside the sprite in UV.
shader_type canvas_item;

uniform sampler2DArray indices : filter_nearest;
uniform sampler2DArray coverage : filter_nearest;
uniform sampler2D palette : filter_nearest;
uniform sampler2D screen : hint_screen_texture, filter_nearest;

varying flat float layer;

void vertex() {
	// The low byte of the layer in red, the high byte in green (SpriteLayer).
	layer = round(COLOR.r * 255.0) + 256.0 * round(COLOR.g * 255.0);
}

void fragment() {
	ivec2 pixel = ivec2(floor(UV));
	ivec3 texel = ivec3(pixel.x, pixel.y, int(layer));
	if (texelFetch(coverage, texel, 0).r < 0.5) {
		discard;
	}
	vec3 under = texture(screen, SCREEN_UV).rgb;
	vec3 eight = texelFetch(palette, ivec2(8, 0), 0).rgb;
	if (distance(under, eight) > 0.002) {
		discard;
	}
	float index = texelFetch(indices, texel, 0).r;
	COLOR = vec4(texelFetch(palette, ivec2(int(round(index * 255.0)), 0), 0).rgb, 1.0);
}
