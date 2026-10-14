#version 450
// Ships on the map (ShipBatchRenderer): one textured quad per instance, read from a storage buffer, 6 vertices made
// from gl_VertexIndex. A quad's corners are in metres from the eye (formed in double on the CPU, so nothing jitters
// at the ship band) and projected like the baked land (land3d.vert), depth included, so the far side of the globe
// fails the depth test against the masking sphere. Each corner also carries its ship-local position for the shadows,
// and the light in the quad's own frame (u, v, up = v x u: a turret's quad is turned to its bearing, so its light comes
// out in its own frame): the sun and the moon from the View uniform, and the eye per vertex, so glints slide a little
// as the map pans.
struct Quad
{
    vec4 o;      // xyz: corner uv (0, 0), metres from the eye; w: mode (0 sprite, 1 silhouette, 2 icon, 3 scorch)
    vec4 u;      // xyz: the quad's edge along uv.x; w: opacity
    vec4 v;      // xyz: the edge along uv.y; w: the turret's roof, m over the waterline (silhouettes, turrets)
    vec4 ship;   // xy: ship-local metres at corner o; zw: the u edge in ship metres
    vec4 shipV;  // xy: the v edge in ship metres; z: the height the hull covers a turret from (0: nothing does); w: unused
    vec4 sun;    // the shadows' light (the stronger of sun and moon): xy: unit vector toward it, ship-local; z: tan(elevation); w: shadow opacity
    vec4 march;  // x: step, m; y: steps; z: softness, m; w: the height map's mip level
    vec4 hmap;   // height map uv = ship metres * xy + zw
    vec4 tint;   // the icon's colour (mode 2); a turret's tint (mode 0, by its alpha); a scorch's colour (mode 3)
    vec4 sea;    // the sea's plane over the waterline, ship frame: z at the origin, slopes along x and y; w: on
};

layout(std430, set = 0, binding = 0) readonly buffer Quads { Quad quads[]; };

layout(set = 1, binding = 0) uniform View
{
    vec4 right;   // xyz: the camera's right; w: tan of half the horizontal field
    vec4 up;      // xyz: the camera's up; w: tan of half the vertical field
    vec4 fwd;     // xyz: the camera's forward
    vec4 depth;   // x: near, y: far, metres; z: the first quad of this draw
    vec4 sun;     // xyz: toward the sun, Earth-fixed (ShipLights.SunDir)
    vec4 moon;    // xyz: toward the moon
};

layout(location = 0) out vec2 fragUv;
layout(location = 1) out vec2 fragShip;
layout(location = 2) flat out vec4 fragMode;    // x mode, y opacity, z roof, w cover
layout(location = 3) flat out vec4 fragSun;
layout(location = 4) flat out vec4 fragMarch;
layout(location = 5) flat out vec4 fragHmap;
layout(location = 6) flat out vec4 fragTint;
layout(location = 7) flat out vec4 fragSea;
layout(location = 8) flat out vec3 fragLightSun;    // toward the sun, in the quad's frame
layout(location = 9) flat out vec3 fragLightMoon;
layout(location = 10) out vec3 fragEye;             // toward the eye, in the quad's frame (not unit)

void main()
{
    Quad q = quads[int(depth.z) + gl_InstanceIndex];
    vec2 corners[6] = vec2[](vec2(0, 0), vec2(1, 0), vec2(1, 1), vec2(0, 0), vec2(1, 1), vec2(0, 1));
    vec2 t = corners[gl_VertexIndex];
    vec3 r = q.o.xyz + t.x * q.u.xyz + t.y * q.v.xyz;
    float z = dot(r, fwd.xyz);
    gl_Position = vec4(dot(r, right.xyz) / right.w, dot(r, up.xyz) / up.w, depth.y * (z - depth.x) / (depth.y - depth.x), z);
    fragUv = t;
    fragShip = q.ship.xy + t.x * q.ship.zw + t.y * q.shipV.xy;
    fragMode = vec4(q.o.w, q.u.w, q.v.w, q.shipV.z);
    fragSun = q.sun;
    fragMarch = q.march;
    fragHmap = q.hmap;
    fragTint = q.tint;
    fragSea = q.sea;
    vec3 fu = normalize(q.u.xyz), fv = normalize(q.v.xyz), fz = normalize(cross(q.v.xyz, q.u.xyz));
    fragLightSun = vec3(dot(sun.xyz, fu), dot(sun.xyz, fv), dot(sun.xyz, fz));
    fragLightMoon = vec3(dot(moon.xyz, fu), dot(moon.xyz, fv), dot(moon.xyz, fz));
    fragEye = -vec3(dot(r, fu), dot(r, fv), dot(r, fz));
}
