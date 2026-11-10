// Draws a ripple image only where the screen under it shows the surface uniform, the color the graphics source names
// (RippleImage.Surface; docs/rules/graphics-images.md, "Sprites drawn over color 8"). The screen is the copy under the
// image, so a pixel counts as surface when its color is the surface uniform. Otherwise as Objects.gdshader: the layer in
// COLOR.r and COLOR.g, the pixel inside the image in UV.
shader_type canvas_item;

uniform sampler2DArray sprites : filter_nearest;
uniform vec4 surface;
uniform sampler2D screen : hint_screen_texture, filter_nearest;

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
	vec3 under = texture(screen, SCREEN_UV).rgb;
	if (distance(under, surface.rgb) > 0.002) {
		discard;
	}
	COLOR = vec4(color.rgb, 1.0);
}
