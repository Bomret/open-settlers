// Draws a terrain triangle from RGBA pixels (ADR 0007, ADR 0014). A vertex carries the pixel inside its mask in UV and,
// in COLOR, the mask's layer in red and the texture's layer in green, each as layer / 255. A covered pixel of the
// mask takes the texture's color at the same place, the texture repeating down every 20 rows
// (docs/rules/graphics-images.md, "Textured fill"). The graphics load has applied the palette.
shader_type canvas_item;

uniform sampler2DArray masks : filter_nearest;
uniform sampler2DArray textures : filter_nearest;

varying flat vec2 layers;

void vertex() {
	layers = COLOR.rg;
}

void fragment() {
	ivec2 pixel = ivec2(floor(UV));
	int mask_layer = int(round(layers.x * 255.0));
	int texture_layer = int(round(layers.y * 255.0));
	if (texelFetch(masks, ivec3(pixel, mask_layer), 0).r < 0.5) {
		discard;
	}
	COLOR = vec4(texelFetch(textures, ivec3(pixel.x, pixel.y % 20, texture_layer), 0).rgb, 1.0);
}
