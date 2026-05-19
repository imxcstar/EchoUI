using System.Runtime.InteropServices;
using WebGPU;
using static WebGPU.WebGPU;

namespace EchoUI.Render.WebGPU.Internal;

/// <summary>
/// 创建并持有 UI Uber Render Pipeline、Uniform buffer、Sampler、Bind group layouts。
/// </summary>
internal sealed unsafe class UiPipeline : IDisposable
{
    public WGPURenderPipeline Pipeline;
    public WGPUPipelineLayout PipelineLayout;
    public WGPUBindGroupLayout GlobalsBindGroupLayout;
    public WGPUBuffer GlobalsUniformBuffer;
    public WGPUSampler LinearSampler;
    public WGPUSampler PointSampler;

    private WGPUTextureFormat _swapFormat;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct GlobalsUniform
    {
        public float ViewportW;
        public float ViewportH;
        public float _pad0;
        public float _pad1;
    }

    public void Initialize(WGPUDevice device, WGPUTextureFormat swapFormat, string wgslSource)
    {
        _swapFormat = swapFormat;

        // 1. Bind group layout (group 0): uniform + sampler + texture
        WGPUBindGroupLayoutEntry* bglEntries = stackalloc WGPUBindGroupLayoutEntry[3];
        bglEntries[0] = new WGPUBindGroupLayoutEntry
        {
            binding = 0,
            visibility = WGPUShaderStage.Vertex | WGPUShaderStage.Fragment,
            buffer = new WGPUBufferBindingLayout
            {
                type = WGPUBufferBindingType.Uniform,
                hasDynamicOffset = false,
                minBindingSize = (ulong)sizeof(GlobalsUniform)
            }
        };
        bglEntries[1] = new WGPUBindGroupLayoutEntry
        {
            binding = 1,
            visibility = WGPUShaderStage.Fragment,
            sampler = new WGPUSamplerBindingLayout
            {
                type = WGPUSamplerBindingType.Filtering
            }
        };
        bglEntries[2] = new WGPUBindGroupLayoutEntry
        {
            binding = 2,
            visibility = WGPUShaderStage.Fragment,
            texture = new WGPUTextureBindingLayout
            {
                sampleType = WGPUTextureSampleType.Float,
                viewDimension = WGPUTextureViewDimension._2D,
                multisampled = false
            }
        };

        WGPUBindGroupLayoutDescriptor bglDesc = new()
        {
            entryCount = 3,
            entries = bglEntries
        };
        GlobalsBindGroupLayout = wgpuDeviceCreateBindGroupLayout(device, &bglDesc);

        WGPUBindGroupLayout bgl = GlobalsBindGroupLayout;
        WGPUPipelineLayoutDescriptor plDesc = new()
        {
            bindGroupLayoutCount = 1,
            bindGroupLayouts = &bgl
        };
        PipelineLayout = wgpuDeviceCreatePipelineLayout(device, &plDesc);

        // 2. Shader module
        WGPUShaderModule shaderModule = wgpuDeviceCreateShaderModule(device, wgslSource);

        // 3. Vertex layout: 7 attributes
        WGPUVertexAttribute* attrs = stackalloc WGPUVertexAttribute[7];
        uint loc = 0;
        ulong off = 0;
        attrs[0] = new WGPUVertexAttribute(WGPUVertexFormat.Float32x2, off, loc++); off += 8;  // position
        attrs[1] = new WGPUVertexAttribute(WGPUVertexFormat.Float32x2, off, loc++); off += 8;  // uv
        attrs[2] = new WGPUVertexAttribute(WGPUVertexFormat.Float32x2, off, loc++); off += 8;  // localUV
        attrs[3] = new WGPUVertexAttribute(WGPUVertexFormat.Float32x4, off, loc++); off += 16; // color
        attrs[4] = new WGPUVertexAttribute(WGPUVertexFormat.Float32x4, off, loc++); off += 16; // borderColor
        attrs[5] = new WGPUVertexAttribute(WGPUVertexFormat.Float32x2, off, loc++); off += 8;  // rectSize
        attrs[6] = new WGPUVertexAttribute(WGPUVertexFormat.Float32x4, off, loc++); off += 16; // params

        WGPUVertexBufferLayout vbLayout = new()
        {
            attributeCount = 7,
            attributes = attrs,
            arrayStride = (ulong)UiVertex.SizeInBytes,
            stepMode = WGPUVertexStepMode.Vertex
        };

        ReadOnlySpan<byte> vsEntry = "vs_main"u8;
        ReadOnlySpan<byte> fsEntry = "fs_main"u8;

        fixed (byte* pVsEntry = vsEntry)
        fixed (byte* pFsEntry = fsEntry)
        {
            WGPUBlendState blend = new();
            blend.color.srcFactor = WGPUBlendFactor.SrcAlpha;
            blend.color.dstFactor = WGPUBlendFactor.OneMinusSrcAlpha;
            blend.color.operation = WGPUBlendOperation.Add;
            blend.alpha.srcFactor = WGPUBlendFactor.One;
            blend.alpha.dstFactor = WGPUBlendFactor.OneMinusSrcAlpha;
            blend.alpha.operation = WGPUBlendOperation.Add;

            WGPUColorTargetState colorTarget = new()
            {
                format = _swapFormat,
                blend = &blend,
                writeMask = WGPUColorWriteMask.All
            };

            WGPUFragmentState fragment = new()
            {
                module = shaderModule,
                entryPoint = new WGPUStringView(pFsEntry, fsEntry.Length),
                targetCount = 1,
                targets = &colorTarget
            };

            WGPURenderPipelineDescriptor pipelineDesc = new();
            pipelineDesc.layout = PipelineLayout;
            pipelineDesc.vertex.module = shaderModule;
            pipelineDesc.vertex.entryPoint = new WGPUStringView(pVsEntry, vsEntry.Length);
            pipelineDesc.vertex.bufferCount = 1;
            pipelineDesc.vertex.buffers = &vbLayout;

            pipelineDesc.primitive.topology = WGPUPrimitiveTopology.TriangleList;
            pipelineDesc.primitive.stripIndexFormat = WGPUIndexFormat.Undefined;
            pipelineDesc.primitive.frontFace = WGPUFrontFace.CCW;
            pipelineDesc.primitive.cullMode = WGPUCullMode.None;

            pipelineDesc.fragment = &fragment;
            pipelineDesc.depthStencil = null;
            pipelineDesc.multisample.count = 1;
            pipelineDesc.multisample.mask = ~0u;
            pipelineDesc.multisample.alphaToCoverageEnabled = false;

            Pipeline = wgpuDeviceCreateRenderPipeline(device, &pipelineDesc);
        }

        wgpuShaderModuleRelease(shaderModule);

        // 4. Uniform buffer
        GlobalsUniformBuffer = wgpuDeviceCreateBuffer(device,
            WGPUBufferUsage.Uniform | WGPUBufferUsage.CopyDst,
            (ulong)sizeof(GlobalsUniform));

        // 5. Samplers
        WGPUSamplerDescriptor linearDesc = new()
        {
            addressModeU = WGPUAddressMode.ClampToEdge,
            addressModeV = WGPUAddressMode.ClampToEdge,
            addressModeW = WGPUAddressMode.ClampToEdge,
            magFilter = WGPUFilterMode.Linear,
            minFilter = WGPUFilterMode.Linear,
            mipmapFilter = WGPUMipmapFilterMode.Nearest,
            lodMinClamp = 0,
            lodMaxClamp = 1,
            maxAnisotropy = 1
        };
        LinearSampler = wgpuDeviceCreateSampler(device, &linearDesc);

        WGPUSamplerDescriptor pointDesc = linearDesc;
        pointDesc.magFilter = WGPUFilterMode.Nearest;
        pointDesc.minFilter = WGPUFilterMode.Nearest;
        PointSampler = wgpuDeviceCreateSampler(device, &pointDesc);
    }

    public void WriteGlobals(WGPUQueue queue, float viewportW, float viewportH)
    {
        GlobalsUniform u = new() { ViewportW = viewportW, ViewportH = viewportH };
        wgpuQueueWriteBuffer(queue, GlobalsUniformBuffer, ref u, 0, (nuint)sizeof(GlobalsUniform));
    }

    public WGPUBindGroup CreateBindGroup(WGPUDevice device, WGPUTextureView textureView, WGPUSampler sampler)
    {
        WGPUBindGroupEntry* entries = stackalloc WGPUBindGroupEntry[3];
        entries[0] = new WGPUBindGroupEntry
        {
            binding = 0,
            buffer = GlobalsUniformBuffer,
            offset = 0,
            size = (ulong)sizeof(GlobalsUniform)
        };
        entries[1] = new WGPUBindGroupEntry
        {
            binding = 1,
            sampler = sampler
        };
        entries[2] = new WGPUBindGroupEntry
        {
            binding = 2,
            textureView = textureView
        };

        WGPUBindGroupDescriptor desc = new()
        {
            layout = GlobalsBindGroupLayout,
            entryCount = 3,
            entries = entries
        };
        return wgpuDeviceCreateBindGroup(device, &desc);
    }

    public void Dispose()
    {
        if (PointSampler.IsNotNull) wgpuSamplerRelease(PointSampler);
        if (LinearSampler.IsNotNull) wgpuSamplerRelease(LinearSampler);
        if (GlobalsUniformBuffer.IsNotNull) { wgpuBufferDestroy(GlobalsUniformBuffer); wgpuBufferRelease(GlobalsUniformBuffer); }
        if (Pipeline.IsNotNull) wgpuRenderPipelineRelease(Pipeline);
        if (PipelineLayout.IsNotNull) wgpuPipelineLayoutRelease(PipelineLayout);
        if (GlobalsBindGroupLayout.IsNotNull) wgpuBindGroupLayoutRelease(GlobalsBindGroupLayout);
    }
}
