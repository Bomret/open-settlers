// Draws an image from RGBA pixels (ADR 0007, ADR 0014). A vertex carries the pixel inside its image in UV and,
// in COLOR, the image's layer as SpriteLayer encodes it: the low byte in red, the high byte in green. The alpha is the
// coverage: a pixel is transparent where its alpha is 0 and never because its palette index is 0, which is a real color
// (docs/rules/map-objects.md). The graphics source has applied the palette.
shader_type canvas_item;

uniform sampler2DArray sprites : filter_nearest;

varying flat float layer;

void vertex() {
	// The low byte of the layer in red, the high byte in green (SpriteLayer).
	layer = round(COLOR.r * 255.0) + 256.0 * round(COLOR.g * 255.0);
}

void fragment() {
	ivec2 pixel = ivec2(floor(UV));
	ivec3 texel = ivec3(pixel.x, pixel.y, int(layer));
	vec4 color = texelFetch(sprites, texel, 0);
	if (color.a < 0.5) {
		discard;
	}
	COLOR = vec4(color.rgb, 1.0);
}
