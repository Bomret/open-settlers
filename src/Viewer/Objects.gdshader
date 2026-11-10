// Draws an object sprite from RGBA pixels (ADR 0007, ADR 0014). A vertex carries the pixel inside its sprite in UV and,
// in COLOR, the sprite's layer as SpriteLayer encodes it: the low byte in red, the high byte in green. Blue picks the
// array: 0 the sprites (sprites), 1 the shadows (shadow_sprites). The alpha is the coverage: a pixel is
// transparent where its alpha is 0 and never because its palette index is 0, which is a real color
// (docs/rules/map-objects.md). The graphics load has applied the palette. The surface uniform is the color of the
// water surface, which Water.gdshader reads and this shader leaves unused.
shader_type canvas_item;

uniform sampler2DArray sprites : filter_nearest;
uniform sampler2DArray shadow_sprites : filter_nearest;

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
	vec4 color = from_shadows ? texelFetch(shadow_sprites, texel, 0) : texelFetch(sprites, texel, 0);
	if (color.a < 0.5) {
		discard;
	}
	COLOR = vec4(color.rgb, 1.0);
}
