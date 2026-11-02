// Draws a terrain triangle from palette indices (ADR 0007). A vertex carries the pixel inside its mask in UV and,
// in COLOR, the mask's layer in red and the texture's layer in green, each as layer / 255. A covered pixel of the
// mask takes the texture's index at the same place, the texture repeating down every 20 rows
// (docs/rules/graphics-images.md, "Textured fill"), and the palette texture turns the index into a color.
shader_type canvas_item;

uniform sampler2DArray masks : filter_nearest;
uniform sampler2DArray textures : filter_nearest;
uniform sampler2D palette : filter_nearest;

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
	float index = texelFetch(textures, ivec3(pixel.x, pixel.y % 20, texture_layer), 0).r;
	COLOR = vec4(texelFetch(palette, ivec2(int(round(index * 255.0)), 0), 0).rgb, 1.0);
}
