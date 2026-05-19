// EchoUI WebGPU UI Uber Shader
// 单一管线同时处理：填充矩形、圆角、边框、纹理图片、字体 alpha mask 文本。

struct VsIn {
    @location(0) position: vec2<f32>,
    @location(1) uv: vec2<f32>,
    @location(2) localUV: vec2<f32>,    // 0..1 within the quad, used for SDF
    @location(3) color: vec4<f32>,
    @location(4) borderColor: vec4<f32>,
    @location(5) rectSize: vec2<f32>,    // pixels
    @location(6) params: vec4<f32>,      // x=borderRadius, y=borderWidth, z=isAlphaMask(0|1), w=hasTexture(0|1)
};

struct VsOut {
    @builtin(position) clipPos: vec4<f32>,
    @location(0) uv: vec2<f32>,
    @location(1) localUV: vec2<f32>,
    @location(2) color: vec4<f32>,
    @location(3) borderColor: vec4<f32>,
    @location(4) rectSize: vec2<f32>,
    @location(5) params: vec4<f32>,
};

struct Globals {
    viewport: vec2<f32>,
    _pad: vec2<f32>,
};

@group(0) @binding(0) var<uniform> u_globals: Globals;
@group(0) @binding(1) var u_sampler: sampler;
@group(0) @binding(2) var u_texture: texture_2d<f32>;

@vertex
fn vs_main(in: VsIn) -> VsOut {
    var out: VsOut;
    let ndc = vec2<f32>(
        (in.position.x / u_globals.viewport.x) * 2.0 - 1.0,
        1.0 - (in.position.y / u_globals.viewport.y) * 2.0
    );
    out.clipPos = vec4<f32>(ndc, 0.0, 1.0);
    out.uv = in.uv;
    out.localUV = in.localUV;
    out.color = in.color;
    out.borderColor = in.borderColor;
    out.rectSize = in.rectSize;
    out.params = in.params;
    return out;
}

fn sdRoundedBox(p: vec2<f32>, b: vec2<f32>, r: f32) -> f32 {
    let q = abs(p) - b + vec2<f32>(r, r);
    return min(max(q.x, q.y), 0.0) + length(max(q, vec2<f32>(0.0, 0.0))) - r;
}

@fragment
fn fs_main(in: VsOut) -> @location(0) vec4<f32> {
    let radius = in.params.x;
    let borderWidth = in.params.y;
    let isAlphaMask = in.params.z > 0.5;
    let hasTexture = in.params.w > 0.5;

    var baseColor: vec4<f32> = in.color;
    if (hasTexture) {
        let sampled = textureSample(u_texture, u_sampler, in.uv);
        if (isAlphaMask) {
            baseColor = vec4<f32>(in.color.rgb, in.color.a * sampled.r);
        } else {
            baseColor = sampled * in.color;
        }
    }

    // SDF position centered on rect
    let halfSize = in.rectSize * 0.5;
    let p = (in.localUV - vec2<f32>(0.5, 0.5)) * in.rectSize;
    let maxR = min(halfSize.x, halfSize.y);
    let r = clamp(radius, 0.0, maxR);
    let d = sdRoundedBox(p, halfSize, r);

    let aa = 1.0;
    let outsideAlpha = 1.0 - smoothstep(-aa, 0.0, d);

    var finalColor = baseColor;
    if (borderWidth > 0.0) {
        // Inside the rect, distance to the inside edge = d + borderWidth.
        let borderEdge = d + borderWidth;
        let borderFactor = smoothstep(-aa, 0.0, borderEdge);
        finalColor = mix(finalColor, in.borderColor, borderFactor);
    }

    finalColor.a *= outsideAlpha;
    if (finalColor.a <= 0.001) {
        discard;
    }
    return finalColor;
}
