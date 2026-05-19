using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WebGPU;
using static WebGPU.WebGPU;

namespace EchoUI.Render.WebGPU.Internal;

/// <summary>
/// 封装 WebGPU 设备/Surface 的创建、Resize 与 RenderFrame。
/// 仅支持 Win32 (HWND) 表面，跨平台留待后续扩展。
/// </summary>
public sealed unsafe class WebGpuContext : IDisposable
{
    public WGPUInstance Instance;
    public WGPUAdapter Adapter;
    public WGPUDevice Device;
    public WGPUQueue Queue;
    public WGPUSurface Surface;
    public WGPUTextureFormat SwapChainFormat;
    public WGPUCompositeAlphaMode AlphaMode;

    public uint Width { get; private set; }
    public uint Height { get; private set; }

    private bool _surfaceConfigured;

    public void Initialize(nint hwnd, nint hinstance, uint width, uint height)
    {
        WGPUInstanceDescriptor instanceDescriptor = new()
        {
            nextInChain = null,
        };
        Instance = wgpuCreateInstance(&instanceDescriptor);
        if (Instance.IsNull)
            throw new InvalidOperationException("Failed to create WGPU instance.");

        Surface = CreateWin32Surface(Instance, hwnd, hinstance);
        if (Surface.IsNull)
            throw new InvalidOperationException("Failed to create WGPU surface from HWND.");

        WGPURequestAdapterOptions adapterOptions = new()
        {
            nextInChain = null,
            compatibleSurface = Surface,
            powerPreference = WGPUPowerPreference.HighPerformance
        };

        WGPUAdapter adapter = WGPUAdapter.Null;
        wgpuInstanceRequestAdapter(
            Instance,
            &adapterOptions,
            new WGPURequestAdapterCallbackInfo()
            {
                callback = &OnAdapterRequestEnded,
                userdata1 = &adapter,
                userdata2 = null
            });

        if (adapter.IsNull)
            throw new InvalidOperationException("Failed to request WGPU adapter.");
        Adapter = adapter;

        ReadOnlySpan<byte> deviceLabel = "EchoUI WebGPU Device"u8;
        fixed (byte* pDeviceLabel = deviceLabel)
        {
            WGPUDeviceDescriptor deviceDesc = new()
            {
                nextInChain = null,
                label = new WGPUStringView(pDeviceLabel, deviceLabel.Length),
                requiredFeatureCount = 0,
                requiredLimits = null,
                uncapturedErrorCallbackInfo = new WGPUUncapturedErrorCallbackInfo()
                {
                    callback = &HandleUncapturedErrorCallback,
                    userdata1 = null,
                    userdata2 = null
                }
            };

            WGPUDevice device = WGPUDevice.Null;
            wgpuAdapterRequestDevice(
                Adapter,
                &deviceDesc,
                new WGPURequestDeviceCallbackInfo()
                {
                    callback = &OnDeviceRequestEnded,
                    userdata1 = &device,
                    userdata2 = null
                });

            if (device.IsNull)
                throw new InvalidOperationException("Failed to request WGPU device.");
            Device = device;
        }

        Queue = wgpuDeviceGetQueue(Device);
        Resize(width, height);
    }

    public void Resize(uint width, uint height)
    {
        if (width == 0 || height == 0)
            return;
        Width = width;
        Height = height;

        wgpuSurfaceGetCapabilities(Surface, Adapter, out WGPUSurfaceCapabilities capabilities);

        // 选择非 sRGB 的 8bit unorm 格式，避免 shader 输出（已是 sRGB 字节值）被再次做 linear→sRGB 转换，
        // 导致中间灰阶被推向白色，呈现整体“白色蒙版”效果。
        SwapChainFormat = WGPUTextureFormat.Undefined;
        for (nuint i = 0; i < capabilities.formatCount; i++)
        {
            var f = capabilities.formats[i];
            if (f == WGPUTextureFormat.BGRA8Unorm || f == WGPUTextureFormat.RGBA8Unorm)
            {
                SwapChainFormat = f;
                break;
            }
        }
        if (SwapChainFormat == WGPUTextureFormat.Undefined && capabilities.formatCount > 0)
            SwapChainFormat = capabilities.formats[0];

        // 优先选择 Opaque，避免半透明窗口与桌面合成产生“蒙版”效果。
        AlphaMode = WGPUCompositeAlphaMode.Auto;
        bool haveOpaque = false;
        for (nuint i = 0; i < capabilities.alphaModeCount; i++)
        {
            if (capabilities.alphaModes[i] == WGPUCompositeAlphaMode.Opaque)
            {
                AlphaMode = WGPUCompositeAlphaMode.Opaque;
                haveOpaque = true;
                break;
            }
        }
        if (!haveOpaque && capabilities.alphaModeCount > 0)
            AlphaMode = capabilities.alphaModes[0];
        Debug.Assert(SwapChainFormat != WGPUTextureFormat.Undefined);

        WGPUSurfaceConfiguration surfaceConfiguration = new()
        {
            device = Device,
            format = SwapChainFormat,
            usage = WGPUTextureUsage.RenderAttachment,
            alphaMode = AlphaMode,
            width = width,
            height = height,
            presentMode = WGPUPresentMode.Fifo,
        };
        wgpuSurfaceConfigure(Surface, &surfaceConfiguration);
        _surfaceConfigured = true;
    }

    /// <summary>
    /// 获取当前帧的 surface texture/view，调用 draw，最后 present。
    /// </summary>
    public void RenderFrame(Action<WGPUCommandEncoder, WGPUTextureView> draw,
        [CallerMemberName] string? frameName = null)
    {
        if (!_surfaceConfigured || Surface.IsNull || Width == 0 || Height == 0)
            return;

        WGPUSurfaceTexture surfaceTexture = default;
        wgpuSurfaceGetCurrentTexture(Surface, &surfaceTexture);

        switch (surfaceTexture.status)
        {
            case WGPUSurfaceGetCurrentTextureStatus.SuccessOptimal:
            case WGPUSurfaceGetCurrentTextureStatus.SuccessSuboptimal:
                break;
            case WGPUSurfaceGetCurrentTextureStatus.Timeout:
            case WGPUSurfaceGetCurrentTextureStatus.Outdated:
            case WGPUSurfaceGetCurrentTextureStatus.Lost:
                if (surfaceTexture.texture.IsNotNull)
                    wgpuTextureRelease(surfaceTexture.texture);
                Resize(Width, Height);
                return;
            default:
                if (surfaceTexture.texture.IsNotNull)
                    wgpuTextureRelease(surfaceTexture.texture);
                return;
        }

        if (surfaceTexture.texture.IsNull)
            return;

        WGPUTextureView textureView = wgpuTextureCreateView(surfaceTexture.texture, null);
        WGPUCommandEncoder encoder = wgpuDeviceCreateCommandEncoder(Device, frameName ?? "EchoUI Frame");

        try
        {
            draw(encoder, textureView);
        }
        finally
        {
            WGPUCommandBuffer cmd = wgpuCommandEncoderFinish(encoder, "EchoUI CmdBuf");
            wgpuQueueSubmit(Queue, cmd);
            wgpuSurfacePresent(Surface);

            wgpuCommandBufferRelease(cmd);
            wgpuCommandEncoderRelease(encoder);
            wgpuTextureViewRelease(textureView);
            wgpuTextureRelease(surfaceTexture.texture);
        }
    }

    private static WGPUSurface CreateWin32Surface(WGPUInstance instance, nint hwnd, nint hinstance)
    {
        WGPUSurfaceSourceWindowsHWND chain = new()
        {
            hwnd = (void*)hwnd,
            hinstance = (void*)hinstance,
            chain = new WGPUChainedStruct
            {
                sType = WGPUSType.SurfaceSourceWindowsHWND
            }
        };
        WGPUSurfaceDescriptor descriptor = new()
        {
            nextInChain = (WGPUChainedStruct*)&chain
        };
        return wgpuInstanceCreateSurface(instance, &descriptor);
    }

    [UnmanagedCallersOnly]
    private static void OnAdapterRequestEnded(WGPURequestAdapterStatus status, WGPUAdapter adapter, WGPUStringView message, void* pUserData1, void* pUserData2)
    {
        if (status == WGPURequestAdapterStatus.Success)
            *(WGPUAdapter*)pUserData1 = adapter;
    }

    [UnmanagedCallersOnly]
    private static void OnDeviceRequestEnded(WGPURequestDeviceStatus status, WGPUDevice device, WGPUStringView message, void* pUserData1, void* pUserData2)
    {
        if (status == WGPURequestDeviceStatus.Success)
            *(WGPUDevice*)pUserData1 = device;
    }

    [UnmanagedCallersOnly]
    private static void HandleUncapturedErrorCallback(WGPUDevice* device, WGPUErrorType type, WGPUStringView message, void* userData1, void* userData2)
    {
        Console.Error.WriteLine($"[EchoUI WGPU] Uncaptured device error: {type} ({message})");
    }

    public void Dispose()
    {
        if (Queue.IsNotNull) wgpuQueueRelease(Queue);
        if (Device.IsNotNull) wgpuDeviceRelease(Device);
        if (Adapter.IsNotNull) wgpuAdapterRelease(Adapter);
        if (Surface.IsNotNull) wgpuSurfaceRelease(Surface);
        if (Instance.IsNotNull) wgpuInstanceRelease(Instance);
    }
}
