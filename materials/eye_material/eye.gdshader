shader_type spatial;

instance uniform vec4 color : source_color;
instance uniform float border = 2.5;
instance uniform bool active = true;

void fragment() {
	if (active)
	{
		float value = 1.0 - distance(UV, vec2(0.5));
		value = pow(value, border * border);
		ALBEDO = vec3(color.r, color.g, color.b) * value;
	}
	else
	{
		ALBEDO = vec3(0.0);
	}
}
