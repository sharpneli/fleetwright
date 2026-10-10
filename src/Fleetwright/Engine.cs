using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using SDL;
using Fleetwright.Gpu;
#if TRACY
using TracyWrapper;
#endif
using static SDL.SDL3;
using static SDL.SDL3_image;

namespace Fleetwright;

/// <summary>
/// PBR (Physically Based Rendering) material handler.
/// Creates and manages pipelines for metallic-roughness workflow.
/// </summary>
public unsafe class PbrMaterial
{
    /// <summary>Pipeline for opaque PBR surfaces.</summary>
    public MaterialPipeline OpaquePipeline;

    /// <summary>Pipeline for transparent PBR surfaces.</summary>
    public MaterialPipeline TransparentPipeline;

    /// <summary>
    /// Builds the PBR pipelines using the engine's resources.
    /// </summary>
    public void BuildPipelines(Sdl3GpuEngine engine)
    {
        // Load PBR shaders
        SDL_GPUShader* vertexShader = engine.LoadShader(
            "Content/Shaders/Compiled/mesh.vert.spv",
            SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX,
            0, 0, 0, 1  // 1 uniform buffer for push constants
        );

        SDL_GPUShader* fragmentShader = engine.LoadShader(
            "Content/Shaders/Compiled/mesh.frag.spv",
            SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT,
            1, 0, 0, 0  // 1 sampler (color)
        );

        if (vertexShader == null || fragmentShader == null)
        {
            Console.WriteLine("PBR shaders not found, skipping PBR pipeline creation");
            if (vertexShader != null) SDL_ReleaseGPUShader(engine.Device, vertexShader);
            if (fragmentShader != null) SDL_ReleaseGPUShader(engine.Device, fragmentShader);
            return;
        }

        // Build opaque pipeline
        // Using LESS_OR_EQUAL for standard depth (near=0, far=1)
        GpuPipelineBuilder builder = new GpuPipelineBuilder();
        OpaquePipeline.Pipeline = builder
            .SetShaders(vertexShader, fragmentShader)
            .SetDefaultVertexLayout()
            .SetColorFormat(SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R16G16B16A16_FLOAT)
            .SetDepthFormat(SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D32_FLOAT)
            .EnableDepthTest(true, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL)
            .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_BACK)
            .SetMultisampling(SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_8)
            .DisableBlending()
            .Build(engine.Device);

        // Build transparent pipeline
        builder = new GpuPipelineBuilder();
        TransparentPipeline.Pipeline = builder
            .SetShaders(vertexShader, fragmentShader)
            .SetDefaultVertexLayout()
            .SetColorFormat(SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R16G16B16A16_FLOAT)
            .SetDepthFormat(SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D32_FLOAT)
            .EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL) // Read depth, don't write
            .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE) // Double-sided for transparency
            .SetMultisampling(SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_8)
            .EnableBlendingAlpha()
            .Build(engine.Device);

        // Release shaders (now embedded in pipelines)
        SDL_ReleaseGPUShader(engine.Device, vertexShader);
        SDL_ReleaseGPUShader(engine.Device, fragmentShader);

        Console.WriteLine("PBR pipelines created successfully");
    }

    /// <summary>
    /// Creates a material instance for rendering.
    /// </summary>
    public MaterialInstance CreateInstance(
        MaterialPass pass,
        GpuTexture colorTexture,
        GpuTexture metalRoughTexture,
        GpuSampler sampler,
        Vector4 colorFactors)
    {
        return new MaterialInstance
        {
            Pipeline = pass == MaterialPass.Transparent ? TransparentPipeline : OpaquePipeline,
            ColorTexture = colorTexture,
            MetalRoughTexture = metalRoughTexture,
            Sampler = sampler,
            PassType = pass,
            ColorFactors = colorFactors
        };
    }

    /// <summary>
    /// Creates a default opaque material instance.
    /// </summary>
    public MaterialInstance CreateDefaultOpaque(Sdl3GpuEngine engine)
    {
        return new MaterialInstance
        {
            Pipeline = OpaquePipeline,
            ColorTexture = engine.WhiteTexture,
            MetalRoughTexture = engine.WhiteTexture,
            Sampler = engine.DefaultSamplerLinear,
            PassType = MaterialPass.MainColor,
            ColorFactors = Vector4.One
        };
    }

    /// <summary>
    /// Disposes of pipeline resources.
    /// </summary>
    public void Dispose(Sdl3GpuEngine engine)
    {
        if (OpaquePipeline.IsValid)
        {
            SDL_ReleaseGPUGraphicsPipeline(engine.Device, OpaquePipeline.Pipeline);
            OpaquePipeline.Pipeline = null;
        }

        if (TransparentPipeline.IsValid)
        {
            SDL_ReleaseGPUGraphicsPipeline(engine.Device, TransparentPipeline.Pipeline);
            TransparentPipeline.Pipeline = null;
        }
    }
}

/// <summary>
/// Main SDL3 GPU rendering engine.
/// </summary>
public unsafe class Sdl3GpuEngine : IDisposable
{
    // Window settings
    public const int DefaultWindowWidth = 1920;
    public const int DefaultWindowHeight = 1080;
    public const string DefaultWindowTitle = "Fleetwright";

    // Core handles
    private SDL_GPUDevice* _device;
    private SDL_Window* _window;
    private uint _windowWidth = DefaultWindowWidth;
    private uint _windowHeight = DefaultWindowHeight;
    private SDL_GPUTextureFormat _swapchainFormat;

    // Render targets
    private RenderTarget _mainTarget = null!;   // HDR R16G16B16A16_FLOAT, 8x MSAA, D32; blitted to the swapchain

    // Scene
    private Camera _mainCamera;
    private GpuSceneData _sceneData;
    private DrawContext _drawContext;

    // Materials and pipelines
    private PbrMaterial _pbrMaterial;
    private SDL_GPUGraphicsPipeline* _blitPipeline;
    private GpuSampler _blitSampler;

    // Default resources
    private GpuTexture _whiteTexture;
    private GpuTexture _blackTexture;
    private GpuTexture _errorCheckerboardTexture;
    private GpuTexture _defaultNormalTexture;
    private GpuSampler _defaultSamplerLinear;
    private GpuSampler _defaultSamplerNearest;

    // Loaded scenes
    private readonly List<LoadedGltf> _loadedScenes = new();
    private readonly List<IRenderable> _sceneRenderables = new();

    // Stats
    public float FrameTime { get; private set; }
    public float DeltaTime { get; private set; }
    public int DrawCalls { get; private set; }
    public int TriangleCount { get; private set; }
    public ulong FrameNumber { get; private set; }
    public float DrawGeometryTimeMs { get; private set; }

    // Timing
    private readonly Stopwatch _frameTimer = new();
    private readonly Stopwatch _totalTimer = new();
    private readonly Stopwatch _drawGeometryTimer = new();

    // State
    private bool _isInitialized;
    private bool _isRunning;
    private bool _windowMinimized;

    // Screenshot
    private int _screenshotFrame = -1;
    private string _screenshotPath = "screenshot.png";
    private bool _screenshotTaken;
    private bool _screenshotUi;

    // The scene drawn into the main target (the ship viewer today), instead of the scene graph below
    public IScene? Scene { get; set; }

    /// <summary>The main render target: scenes draw into it, and it is blitted to the swapchain.</summary>
    public RenderTarget MainTarget => _mainTarget;

    // ImGui
    private ImGuiRenderer? _imguiRenderer;
    private bool _showDemoWindow = true;
    private bool _showStatsWindow;

    /// <summary>
    /// Gets the GPU device handle.
    /// </summary>
    public SDL_GPUDevice* Device => _device;

    /// <summary>
    /// Gets the window handle.
    /// </summary>
    public SDL_Window* Window => _window;

    /// <summary>
    /// Gets the main camera.
    /// </summary>
    public Camera MainCamera => _mainCamera;

    /// <summary>
    /// Gets the current window width.
    /// </summary>
    public uint WindowWidth => _windowWidth;

    /// <summary>
    /// Gets the current window height.
    /// </summary>
    public uint WindowHeight => _windowHeight;

    /// <summary>
    /// Gets the default white texture.
    /// </summary>
    public GpuTexture WhiteTexture => _whiteTexture;

    /// <summary>
    /// Gets the default black texture.
    /// </summary>
    public GpuTexture BlackTexture => _blackTexture;

    /// <summary>
    /// Gets the error checkerboard texture.
    /// </summary>
    public GpuTexture ErrorCheckerboardTexture => _errorCheckerboardTexture;

    /// <summary>
    /// Gets the default normal map texture.
    /// </summary>
    public GpuTexture DefaultNormalTexture => _defaultNormalTexture;

    /// <summary>
    /// Gets the default linear sampler.
    /// </summary>
    public GpuSampler DefaultSamplerLinear => _defaultSamplerLinear;

    /// <summary>
    /// Gets the default nearest sampler.
    /// </summary>
    public GpuSampler DefaultSamplerNearest => _defaultSamplerNearest;

    /// <summary>
    /// Gets the PBR material handler.
    /// </summary>
    public PbrMaterial PbrMaterial => _pbrMaterial;

    /// <summary>
    /// Gets the draw context for rendering.
    /// </summary>
    public DrawContext DrawContext => _drawContext;

    /// <summary>
    /// Gets the list of renderable objects in the scene.
    /// </summary>
    public List<IRenderable> SceneRenderables => _sceneRenderables;

    /// <summary>
    /// Configures screenshot capture.
    /// </summary>
    /// <param name="frameNumber">Frame number to capture (0-indexed).</param>
    /// <param name="outputPath">Output file path (PNG format).</param>
    /// <param name="withUi">The window as seen, ImGui on top; otherwise the scene alone.</param>
    public void SetScreenshotCapture(int frameNumber, string outputPath = "screenshot.png", bool withUi = false)
    {
        _screenshotFrame = frameNumber;
        _screenshotPath = outputPath;
        _screenshotUi = withUi;
        _screenshotTaken = false;
        // The capture shows the UI's own defaults: no saved layout read, the user's not overwritten
        if (_isInitialized)
            ImGui.GetIO().NativePtr->IniFilename = null;
        Console.WriteLine($"Screenshot will be taken at frame {frameNumber}{(withUi ? " with the UI" : "")}");
    }

    /// <summary>The window's size in pixels, before <see cref="Init"/>.</summary>
    public void SetWindowSize(uint width, uint height)
    {
        if (_isInitialized)
            throw new InvalidOperationException("SetWindowSize goes before Init");
        _windowWidth = width;
        _windowHeight = height;
    }

    public Sdl3GpuEngine()
    {
        _mainCamera = new Camera();
        _drawContext = new DrawContext();
        _sceneData = GpuSceneData.CreateDefault();
        _pbrMaterial = new PbrMaterial();
    }

    #region Initialization

    /// <summary>
    /// Initializes the engine, creating window, GPU device, and resources.
    /// </summary>
    public void Init()
    {
        if (_isInitialized)
        {
            Console.Error.WriteLine("Engine already initialized");
            return;
        }

        // Initialize Tracy profiler on main thread
        Profiler.InitThread("Main");

        InitSdl();
        InitGpuDevice();
        InitWindow();
        InitDrawTextures();
        InitDefaultData();
        InitPipelines();
        InitImGui();

        _isInitialized = true;
        Console.WriteLine("Engine initialized successfully");
    }

    private void InitImGui()
    {
        // Create ImGui context
        ImGui.CreateContext();
        ImGuiIOPtr io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;

        // Set style
        ImGui.StyleColorsDark();

        // Create renderer
        _imguiRenderer = new ImGuiRenderer(_device, _window, _swapchainFormat);

        Console.WriteLine("ImGui initialized successfully");
    }

    private void InitSdl()
    {
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
        {
            throw new Exception($"Failed to initialize SDL: {SDL_GetError()}");
        }
    }

    private void InitGpuDevice()
    {
        _device = SDL_CreateGPUDevice(
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
            false,  // debug mode
            (byte*)null
        );

        if (_device == null)
        {
            throw new Exception($"Failed to create GPU device: {SDL_GetError()}");
        }

        Console.WriteLine($"GPU Driver: {SDL_GetGPUDeviceDriver(_device)}");
    }

    private void InitWindow()
    {
        _window = SDL_CreateWindow(
            DefaultWindowTitle,
            (int)_windowWidth,
            (int)_windowHeight,
            SDL_WindowFlags.SDL_WINDOW_RESIZABLE
        );

        if (_window == null)
        {
            throw new Exception($"Failed to create window: {SDL_GetError()}");
        }

        if (!SDL_ClaimWindowForGPUDevice(_device, _window))
        {
            throw new Exception($"Failed to claim window for GPU: {SDL_GetError()}");
        }

        _swapchainFormat = SDL_GetGPUSwapchainTextureFormat(_device, _window);
    }

    private void InitDrawTextures()
    {
        // HDR colour, 8x MSAA resolved into a sampled texture, and depth
        _mainTarget = new RenderTarget(_device, _windowWidth, _windowHeight,
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R16G16B16A16_FLOAT, SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_8);
    }

    private void InitDefaultData()
    {
        // Create default textures
        _whiteTexture = CreateSingleColorTexture(0xFFFFFFFF); // White
        _blackTexture = CreateSingleColorTexture(0xFF000000); // Black (with alpha)
        _defaultNormalTexture = CreateSingleColorTexture(0xFFFF8080); // Default normal (0.5, 0.5, 1.0)
        _errorCheckerboardTexture = CreateCheckerboardTexture(16, 0xFFFF00FF, 0xFF000000); // Magenta/Black

        // Create default samplers
        SDL_GPUSamplerCreateInfo linearSamplerInfo = new SDL_GPUSamplerCreateInfo
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_LINEAR,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_REPEAT,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_REPEAT,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_REPEAT,
            max_anisotropy = 16.0f,
            enable_anisotropy = true
        };
        _defaultSamplerLinear = CreateSampler(linearSamplerInfo);

        SDL_GPUSamplerCreateInfo nearestSamplerInfo = new SDL_GPUSamplerCreateInfo
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_REPEAT,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_REPEAT,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_REPEAT
        };
        _defaultSamplerNearest = CreateSampler(nearestSamplerInfo);

        // Create blit sampler (linear, clamp)
        SDL_GPUSamplerCreateInfo blitSamplerInfo = new SDL_GPUSamplerCreateInfo
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE
        };
        _blitSampler = CreateSampler(blitSamplerInfo);
    }

    private void InitPipelines()
    {
        // Build PBR material pipelines
        _pbrMaterial.BuildPipelines(this);

        // Create blit pipeline for final output to swapchain
        CreateBlitPipeline();
    }

    private void CreateBlitPipeline()
    {
        // Load blit shaders (fullscreen triangle)
        SDL_GPUShader* vertexShader = LoadShader("Content/Shaders/Compiled/blit.vert.spv",
            SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 0, 0, 0);
        SDL_GPUShader* fragmentShader = LoadShader("Content/Shaders/Compiled/blit.frag.spv",
            SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 1, 0, 0, 0);

        if (vertexShader == null || fragmentShader == null)
        {
            Console.WriteLine("Blit shaders not found, skipping blit pipeline creation");
            return;
        }

        GpuPipelineBuilder builder = new GpuPipelineBuilder();
        _blitPipeline = builder
            .SetShaders(vertexShader, fragmentShader)
            .SetColorFormat(_swapchainFormat)
            .DisableDepthTest()
            .NoDepthTarget()
            .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE)
            .Build(_device);

        SDL_ReleaseGPUShader(_device, vertexShader);
        SDL_ReleaseGPUShader(_device, fragmentShader);
    }

    private GpuTexture CreateSingleColorTexture(uint color)
    {
        GpuTexture texture = CreateTexture(
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
            1, 1,
            SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER
        );

        UploadToTexture(texture, &color, 1, 1);
        return texture;
    }

    private GpuTexture CreateCheckerboardTexture(uint size, uint color1, uint color2)
    {
        uint[] pixels = new uint[size * size];
        for (uint y = 0; y < size; y++)
        {
            for (uint x = 0; x < size; x++)
            {
                bool checker = ((x / 4) + (y / 4)) % 2 == 0;
                pixels[y * size + x] = checker ? color1 : color2;
            }
        }

        GpuTexture texture = CreateTexture(
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
            size, size,
            SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER
        );

        fixed (uint* pixelPtr = pixels)
        {
            UploadToTexture(texture, pixelPtr, size, size);
        }

        return texture;
    }

    /// <summary>
    /// Handles window resize by recreating render targets.
    /// </summary>
    public void ResizeRenderTargets(uint width, uint height)
    {
        if (width == 0 || height == 0) return;
        if (width == _windowWidth && height == _windowHeight) return;

        _windowWidth = width;
        _windowHeight = height;
        _mainTarget.Resize(width, height);   // waits for the GPU

        Console.WriteLine($"Resized render targets to {width}x{height}");
    }

    #endregion

    #region Resource Management

    /// <summary>
    /// Creates a GPU buffer.
    /// </summary>
    public GpuBuffer CreateBuffer(nuint size, SDL_GPUBufferUsageFlags usage)
    {
        SDL_GPUBufferCreateInfo createInfo = new SDL_GPUBufferCreateInfo
        {
            size = (uint)size,
            usage = usage
        };

        SDL_GPUBuffer* buffer = SDL_CreateGPUBuffer(_device, &createInfo);
        if (buffer == null)
        {
            Console.Error.WriteLine($"Failed to create buffer: {SDL_GetError()}");
        }

        return new GpuBuffer { Buffer = buffer, Size = size };
    }

    /// <summary>
    /// Destroys a GPU buffer.
    /// </summary>
    public void DestroyBuffer(GpuBuffer buffer)
    {
        if (buffer.IsValid)
        {
            SDL_ReleaseGPUBuffer(_device, buffer.Buffer);
        }
    }

    /// <summary>
    /// Creates a GPU texture.
    /// </summary>
    public GpuTexture CreateTexture(SDL_GPUTextureFormat format, uint width, uint height, SDL_GPUTextureUsageFlags usage)
    {
        SDL_GPUTextureCreateInfo createInfo = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = format,
            width = width,
            height = height,
            layer_count_or_depth = 1,
            num_levels = 1,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
            usage = usage
        };

        SDL_GPUTexture* texture = SDL_CreateGPUTexture(_device, &createInfo);
        if (texture == null)
        {
            Console.Error.WriteLine($"Failed to create texture: {SDL_GetError()}");
        }

        return new GpuTexture
        {
            Texture = texture,
            Width = width,
            Height = height,
            Depth = 1,
            Format = format
        };
    }

    /// <summary>
    /// Creates a GPU texture with MSAA.
    /// </summary>
    public GpuTexture CreateTextureMsaa(SDL_GPUTextureFormat format, uint width, uint height,
        SDL_GPUTextureUsageFlags usage, SDL_GPUSampleCount sampleCount)
    {
        SDL_GPUTextureCreateInfo createInfo = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = format,
            width = width,
            height = height,
            layer_count_or_depth = 1,
            num_levels = 1,
            sample_count = sampleCount,
            usage = usage
        };

        SDL_GPUTexture* texture = SDL_CreateGPUTexture(_device, &createInfo);
        if (texture == null)
        {
            Console.Error.WriteLine($"Failed to create MSAA texture: {SDL_GetError()}");
        }

        return new GpuTexture
        {
            Texture = texture,
            Width = width,
            Height = height,
            Depth = 1,
            Format = format
        };
    }

    /// <summary>
    /// Destroys a GPU texture.
    /// </summary>
    public void DestroyTexture(GpuTexture texture)
    {
        if (texture.IsValid)
        {
            SDL_ReleaseGPUTexture(_device, texture.Texture);
        }
    }

    /// <summary>
    /// Creates a GPU sampler.
    /// </summary>
    public GpuSampler CreateSampler(SDL_GPUSamplerCreateInfo createInfo)
    {
        SDL_GPUSampler* sampler = SDL_CreateGPUSampler(_device, &createInfo);
        if (sampler == null)
        {
            Console.Error.WriteLine($"Failed to create sampler: {SDL_GetError()}");
        }

        return new GpuSampler { Sampler = sampler };
    }

    /// <summary>
    /// Destroys a GPU sampler.
    /// </summary>
    public void DestroySampler(GpuSampler sampler)
    {
        if (sampler.IsValid)
        {
            SDL_ReleaseGPUSampler(_device, sampler.Sampler);
        }
    }

    /// <summary>
    /// Uploads data to a GPU buffer using a copy pass.
    /// Creates a dedicated transfer buffer for each upload to avoid race conditions.
    /// </summary>
    public void UploadToBuffer(GpuBuffer buffer, void* data, nuint size)
    {
        // Create a dedicated transfer buffer for this upload (matching C++ behavior)
        SDL_GPUTransferBufferCreateInfo transferInfo = new SDL_GPUTransferBufferCreateInfo
        {
            usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
            size = (uint)size
        };

        SDL_GPUTransferBuffer* transferBuffer = SDL_CreateGPUTransferBuffer(_device, &transferInfo);
        if (transferBuffer == null)
        {
            Console.Error.WriteLine($"Failed to create transfer buffer: {SDL_GetError()}");
            return;
        }

        // Map transfer buffer and copy data
        void* mappedPtr = (void*)SDL_MapGPUTransferBuffer(_device, transferBuffer, false);
        NativeMemory.Copy(data, mappedPtr, size);
        SDL_UnmapGPUTransferBuffer(_device, transferBuffer);

        // Create copy command
        SDL_GPUCommandBuffer* cmdBuffer = SDL_AcquireGPUCommandBuffer(_device);
        SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(cmdBuffer);

        SDL_GPUTransferBufferLocation source = new SDL_GPUTransferBufferLocation
        {
            transfer_buffer = transferBuffer,
            offset = 0
        };

        SDL_GPUBufferRegion destination = new SDL_GPUBufferRegion
        {
            buffer = buffer.Buffer,
            offset = 0,
            size = (uint)size
        };

        SDL_UploadToGPUBuffer(copyPass, &source, &destination, false);

        SDL_EndGPUCopyPass(copyPass);
        SDL_SubmitGPUCommandBuffer(cmdBuffer);

        // Release the transfer buffer (GPU will keep it alive until command completes)
        SDL_ReleaseGPUTransferBuffer(_device, transferBuffer);
    }

    /// <summary>
    /// Uploads data to a GPU texture using a copy pass.
    /// Creates a dedicated transfer buffer for each upload to avoid race conditions.
    /// </summary>
    public void UploadToTexture(GpuTexture texture, void* data, uint width, uint height)
    {
        uint bytesPerPixel = GetBytesPerPixel(texture.Format);
        uint size = width * height * bytesPerPixel;

        // Create a dedicated transfer buffer for this upload (matching C++ behavior)
        SDL_GPUTransferBufferCreateInfo bufferInfo = new SDL_GPUTransferBufferCreateInfo
        {
            usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
            size = size
        };

        SDL_GPUTransferBuffer* transferBuffer = SDL_CreateGPUTransferBuffer(_device, &bufferInfo);
        if (transferBuffer == null)
        {
            Console.Error.WriteLine($"Failed to create transfer buffer for texture: {SDL_GetError()}");
            return;
        }

        // Map transfer buffer and copy data
        void* mappedPtr = (void*)SDL_MapGPUTransferBuffer(_device, transferBuffer, false);
        NativeMemory.Copy(data, mappedPtr, size);
        SDL_UnmapGPUTransferBuffer(_device, transferBuffer);

        // Create copy command
        SDL_GPUCommandBuffer* cmdBuffer = SDL_AcquireGPUCommandBuffer(_device);
        SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(cmdBuffer);

        SDL_GPUTextureTransferInfo transferInfo = new SDL_GPUTextureTransferInfo
        {
            transfer_buffer = transferBuffer,
            offset = 0,
            pixels_per_row = width,
            rows_per_layer = height
        };

        SDL_GPUTextureRegion textureRegion = new SDL_GPUTextureRegion
        {
            texture = texture.Texture,
            x = 0,
            y = 0,
            z = 0,
            w = width,
            h = height,
            d = 1
        };

        SDL_UploadToGPUTexture(copyPass, &transferInfo, &textureRegion, false);

        SDL_EndGPUCopyPass(copyPass);
        SDL_SubmitGPUCommandBuffer(cmdBuffer);

        // Release the transfer buffer (GPU will keep it alive until command completes)
        SDL_ReleaseGPUTransferBuffer(_device, transferBuffer);
    }

    /// <summary>
    /// Loads a shader from a SPIR-V file.
    /// </summary>
    public SDL_GPUShader* LoadShader(string path, SDL_GPUShaderStage stage,
        uint numSamplers, uint numStorageBuffers, uint numStorageTextures, uint numUniformBuffers)
    {
        return GpuShader.Load(_device, path, stage, numSamplers, numStorageBuffers, numStorageTextures, numUniformBuffers);
    }

    /// <summary>
    /// Uploads mesh data to GPU buffers.
    /// </summary>
    public GpuMeshBuffers UploadMesh(Vertex[] vertices, uint[] indices)
    {
        nuint vertexSize = (nuint)(vertices.Length * Vertex.SizeInBytes);
        nuint indexSize = (nuint)(indices.Length * sizeof(uint));

        GpuBuffer vertexBuffer = CreateBuffer(vertexSize, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX);
        GpuBuffer indexBuffer = CreateBuffer(indexSize, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX);

        fixed (Vertex* vertexPtr = vertices)
        {
            UploadToBuffer(vertexBuffer, vertexPtr, vertexSize);
        }

        fixed (uint* indexPtr = indices)
        {
            UploadToBuffer(indexBuffer, indexPtr, indexSize);
        }

        return new GpuMeshBuffers
        {
            VertexBuffer = vertexBuffer,
            IndexBuffer = indexBuffer,
            VertexCount = (uint)vertices.Length,
            IndexCount = (uint)indices.Length,
            IndexElementSize = SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_32BIT
        };
    }

    /// <summary>
    /// Gets bytes per pixel for a texture format.
    /// </summary>
    private static uint GetBytesPerPixel(SDL_GPUTextureFormat format)
    {
        return format switch
        {
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8_UNORM => 1,
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8_UNORM => 2,
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM => 4,
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM_SRGB => 4,
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_B8G8R8A8_UNORM => 4,
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R16G16B16A16_FLOAT => 8,
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R32G32B32A32_FLOAT => 16,
            _ => 4 // Default assumption
        };
    }

    #endregion

    #region Render Loop

    /// <summary>
    /// Runs the main engine loop.
    /// </summary>
    public void Run()
    {
        if (!_isInitialized)
        {
            Console.Error.WriteLine("Engine not initialized. Call Init() first.");
            return;
        }

        _isRunning = true;
        _totalTimer.Start();
        _frameTimer.Start();

        while (_isRunning)
        {
            _frameTimer.Restart();

            using (new ProfileScope("ProcessEvents", ZoneC.BLUE))
                ProcessEvents();

            if (!_windowMinimized)
            {
                using (new ProfileScope("Camera.Update", ZoneC.CYAN))
                    _mainCamera.Update(DeltaTime);

                using (new ProfileScope("UpdateScene", ZoneC.GREEN))
                    UpdateScene();

                using (new ProfileScope("Draw", ZoneC.RED))
                    Draw();

                // The screenshot is taken in Draw; the run ends with it
                if (_screenshotTaken)
                {
                    Console.WriteLine($"Screenshot complete. Exiting after frame {FrameNumber}.");
                    _isRunning = false;
                }
            }

            FrameNumber++;
            DeltaTime = (float)_frameTimer.Elapsed.TotalSeconds;
            FrameTime = DeltaTime * 1000.0f; // Convert to milliseconds

            Profiler.HeartBeat(); // Frame boundary marker for Tracy
        }
    }

    /// <summary>
    /// Stops the engine loop.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
    }

    private void ProcessEvents()
    {
        SDL_Event evt;
        while (SDL_PollEvent(&evt))
        {
            // Forward to ImGui
            _imguiRenderer?.ProcessEvent(&evt);

            SDL_EventType eventType = (SDL_EventType)evt.type;

            switch (eventType)
            {
                case SDL_EventType.SDL_EVENT_QUIT:
                    _isRunning = false;
                    break;

                case SDL_EventType.SDL_EVENT_KEY_DOWN:
                    if ((uint)evt.key.key == SDLK_ESCAPE)
                    {
                        _isRunning = false;
                    }
                    // Toggle demo window with F1
                    if ((uint)evt.key.key == SDLK_F1)
                    {
                        _showDemoWindow = !_showDemoWindow;
                    }
                    // Toggle stats window with F2
                    if ((uint)evt.key.key == SDLK_F2)
                    {
                        _showStatsWindow = !_showStatsWindow;
                    }
                    break;

                case SDL_EventType.SDL_EVENT_WINDOW_RESIZED:
                    int w, h;
                    SDL_GetWindowSize(_window, &w, &h);
                    ResizeRenderTargets((uint)w, (uint)h);
                    break;

                case SDL_EventType.SDL_EVENT_WINDOW_MINIMIZED:
                    _windowMinimized = true;
                    break;

                case SDL_EventType.SDL_EVENT_WINDOW_RESTORED:
                    _windowMinimized = false;
                    break;
            }

            // Forward to the scene or the camera, unless ImGui wants this kind of input
            if (SceneWants(&evt))
            {
                if (Scene != null)
                    Scene.ProcessEvent(&evt, _windowWidth, _windowHeight);
                else
                    _mainCamera.ProcessSdlEvent(&evt);
            }
        }
    }

    /// <summary>Mouse buttons whose press went to the scene: their release goes there too, wherever it lands.</summary>
    uint _sceneButtons;

    /// <summary>
    /// Whether an event goes to the scene. The mouse goes to whatever is under it: the scene unless the cursor is over
    /// an ImGui window (or ImGui owns the drag), with no click needed to move focus either way. The keyboard goes to the
    /// scene unless an ImGui text field is being typed into. Not WantCaptureKeyboard: with keyboard navigation on it
    /// stays set as long as any ImGui window has focus, which is any window last clicked.
    /// </summary>
    private bool SceneWants(SDL_Event* e)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        switch ((SDL_EventType)e->type)
        {
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                if (io.WantCaptureMouse)
                    return false;
                _sceneButtons |= 1u << e->button.button;
                return true;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                uint bit = 1u << e->button.button;
                bool pressedInScene = (_sceneButtons & bit) != 0;
                _sceneButtons &= ~bit;
                return pressedInScene;
            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                return !io.WantCaptureMouse || _sceneButtons != 0;
            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                return !io.WantCaptureMouse;
            case SDL_EventType.SDL_EVENT_KEY_DOWN:
            case SDL_EventType.SDL_EVENT_KEY_UP:
            case SDL_EventType.SDL_EVENT_TEXT_INPUT:
            case SDL_EventType.SDL_EVENT_TEXT_EDITING:
                return !io.WantTextInput;
            default:
                return true;
        }
    }

    private void UpdateScene()
    {
        // Update scene data matrices
        float aspect = (float)_windowWidth / (float)_windowHeight;

        // Use camera view matrix
        _sceneData.View = _mainCamera.GetViewMatrix();

        // Perspective projection. SDL GPU uses +Y-up NDC on all backends (it handles
        // Vulkan's flip internally), so no manual Y flip is needed here.
        _sceneData.Proj = Matrix4x4.CreatePerspectiveFieldOfView(
            GpuMath.DegreesToRadians(70.0f), aspect, 0.1f, 1000.0f);
        _sceneData.ViewProj = _sceneData.View * _sceneData.Proj;

        // Update lighting
        _sceneData.AmbientColor = new Vector4(0.1f, 0.1f, 0.12f, 1.0f);
        _sceneData.SunlightDirection = Vector4.Normalize(new Vector4(1.0f, 1.0f, 0.5f, 0.0f));
        _sceneData.SunlightColor = new Vector4(1.0f, 0.95f, 0.9f, 1.0f);
    }

    private void Draw()
    {
        // Reset stats
        DrawCalls = 0;
        TriangleCount = 0;

        using (new ProfileScope("CollectRenderables", ZoneC.GREEN))
        {
            // Clear draw context
            _drawContext.Clear();

            // Collect renderables
            foreach (var renderable in _sceneRenderables)
            {
                renderable.Draw(Matrix4x4.Identity, _drawContext);
            }
        }

        using (new ProfileScope("ImGui.Frame", ZoneC.PURPLE))
        {
            // Start ImGui frame
            _imguiRenderer?.NewFrame(DeltaTime);
            ImGui.NewFrame();

            // Build ImGui UI
            BuildImGuiUI();

            // End ImGui frame
            ImGui.Render();
        }

        // Acquire command buffer
        SDL_GPUCommandBuffer* commandBuffer = SDL_AcquireGPUCommandBuffer(_device);
        if (commandBuffer == null)
        {
            Console.Error.WriteLine($"Failed to acquire command buffer: {SDL_GetError()}");
            return;
        }

        // Prepare ImGui draw data (uploads buffers, must be before render pass)
        ImDrawDataPtr drawData = ImGui.GetDrawData();
        _imguiRenderer?.PrepareDrawData(drawData, commandBuffer);

        // Acquire swapchain texture
        SDL_GPUTexture* swapchainTexture;

        using (new ProfileScope("Wait Swapchain", ZoneC.PURPLE))
        {
            if (!SDL_WaitAndAcquireGPUSwapchainTexture(commandBuffer, _window, &swapchainTexture, null, null))
            {
                Console.Error.WriteLine($"Failed to acquire swapchain texture: {SDL_GetError()}");
                SDL_SubmitGPUCommandBuffer(commandBuffer);
                return;
            }
        }
        if (swapchainTexture == null)
        {
            // Window is minimized or occluded
            SDL_SubmitGPUCommandBuffer(commandBuffer);
            return;
        }

        // Draw geometry to MSAA render target
        using (new ProfileScope("DrawGeometry", ZoneC.ORANGE))
        {
            _drawGeometryTimer.Restart();
            if (Scene != null)
                Scene.Draw(commandBuffer, _mainTarget, DeltaTime);
            else
                DrawGeometry(commandBuffer);
            _drawGeometryTimer.Stop();
            DrawGeometryTimeMs = (float)_drawGeometryTimer.Elapsed.TotalMilliseconds;
        }

        // Resolve MSAA and blit to swapchain
        using (new ProfileScope("Blit", ZoneC.ORANGE))
            BlitMainTo(commandBuffer, swapchainTexture);

        // Draw ImGui
        using (new ProfileScope("DrawImGui", ZoneC.PURPLE))
            DrawImGui(commandBuffer, swapchainTexture, drawData);

        // Submit (the screenshot frame records its capture first, then submits and waits)
        if (_screenshotFrame >= 0 && (int)FrameNumber >= _screenshotFrame && !_screenshotTaken)
            CaptureScreenshot(commandBuffer, drawData);
        else
            SDL_SubmitGPUCommandBuffer(commandBuffer);
    }

    private void BuildImGuiUI()
    {
        // Stats window (F2 toggles, its close button hides it)
        if (_showStatsWindow)
        {
            var vp = ImGui.GetMainViewport();   // top right: the left edge is the scenes' side panel
            ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X + vp.WorkSize.X - 260, vp.WorkPos.Y + 10), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(250, 150), ImGuiCond.Always);

            ImGui.Begin("Stats", ref _showStatsWindow, ImGuiWindowFlags.NoCollapse);
            ImGui.Text($"Frame: {FrameNumber}");
            ImGui.Text($"Frame Time: {FrameTime:F2} ms");
            ImGui.Text($"FPS: {(FrameTime > 0 ? 1000.0f / FrameTime : 0):F1}");
            ImGui.Separator();
            ImGui.Text($"Draw Calls: {DrawCalls}");
            ImGui.Text($"Triangles: {TriangleCount}");
            ImGui.Separator();
            ImGui.Text($"DrawGeometry: {DrawGeometryTimeMs:F3} ms");
            ImGui.End();
        }

        if (Scene != null)
        {
            Scene.BuildUi();
            return;
        }

        // Demo window
        if (_showDemoWindow)
        {
            ImGui.ShowDemoWindow(ref _showDemoWindow);
        }
    }

    private void DrawImGui(SDL_GPUCommandBuffer* commandBuffer, SDL_GPUTexture* target, ImDrawDataPtr drawData)
    {
        if (_imguiRenderer == null || drawData.TotalVtxCount <= 0)
            return;

        // Begin render pass for ImGui (renders to swapchain with blending)
        SDL_GPUColorTargetInfo colorTargetInfo = new SDL_GPUColorTargetInfo
        {
            texture = target,
            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_LOAD, // Preserve existing content
            store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE
        };

        SDL_GPURenderPass* renderPass = SDL_BeginGPURenderPass(commandBuffer, &colorTargetInfo, 1, null);

        _imguiRenderer.RenderDrawData(drawData, commandBuffer, renderPass);

        SDL_EndGPURenderPass(renderPass);
    }

    private void DrawGeometry(SDL_GPUCommandBuffer* commandBuffer)
    {
        // MSAA pass with depth (1 is far), viewport over the whole target
        SDL_GPURenderPass* renderPass = _mainTarget.BeginPass(
            commandBuffer, new SDL_FColor { r = 0.1f, g = 0.1f, b = 0.15f, a = 1.0f });

        // Draw opaque surfaces
        using (new ProfileScope("OpaquePass", ZoneC.RED))
        {
            if (_pbrMaterial.OpaquePipeline.Pipeline != null)
            {
                SDL_BindGPUGraphicsPipeline(renderPass, _pbrMaterial.OpaquePipeline.Pipeline);

                foreach (var surface in _drawContext.OpaqueSurfaces)
                {
                    DrawRenderObject(renderPass, commandBuffer, surface);
                }
            }
        }

        // Draw transparent surfaces (sorted back-to-front would be ideal)
        using (new ProfileScope("TransparentPass", ZoneC.YELLOW))
        {
            if (_pbrMaterial.TransparentPipeline.Pipeline != null)
            {
                SDL_BindGPUGraphicsPipeline(renderPass, _pbrMaterial.TransparentPipeline.Pipeline);

                foreach (var surface in _drawContext.TransparentSurfaces)
                {
                    DrawRenderObject(renderPass, commandBuffer, surface);
                }
            }
        }

        SDL_EndGPURenderPass(renderPass);
    }

    private void DrawRenderObject(SDL_GPURenderPass* renderPass, SDL_GPUCommandBuffer* commandBuffer, RenderObject obj)
    {
        // Bind vertex buffer
        SDL_GPUBufferBinding vertexBinding = new SDL_GPUBufferBinding
        {
            buffer = obj.VertexBuffer.Buffer,
            offset = 0
        };
        SDL_BindGPUVertexBuffers(renderPass, 0, &vertexBinding, 1);

        // Bind index buffer
        SDL_GPUBufferBinding indexBinding = new SDL_GPUBufferBinding
        {
            buffer = obj.IndexBuffer.Buffer,
            offset = 0
        };
        SDL_BindGPUIndexBuffer(renderPass, &indexBinding, SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_32BIT);

        // Create push constant data
        GpuVertexPushData pushData = new GpuVertexPushData
        {
            ViewProj = _sceneData.ViewProj,
            ModelMatrix = obj.Transform,
            ColorFactors = obj.Material.ColorFactors
        };

        // Push vertex constants
        SDL_PushGPUVertexUniformData(commandBuffer, 0, (nint)(&pushData), (uint)GpuVertexPushData.SizeInBytes);

        // Bind color texture
        SDL_GPUTextureSamplerBinding textureSamplerBinding = new SDL_GPUTextureSamplerBinding
        {
            texture = obj.Material.ColorTexture.IsValid ? obj.Material.ColorTexture.Texture : _whiteTexture.Texture,
            sampler = obj.Material.Sampler.IsValid ? obj.Material.Sampler.Sampler : _defaultSamplerLinear.Sampler
        };
        SDL_BindGPUFragmentSamplers(renderPass, 0, &textureSamplerBinding, 1);

        // Draw
        SDL_DrawGPUIndexedPrimitives(renderPass, obj.IndexCount, 1, obj.FirstIndex, 0, 0);

        DrawCalls++;
        TriangleCount += (int)(obj.IndexCount / 3);
    }

    /// <summary>The main target, resolved, onto a texture of the swapchain's format: the swapchain, or the
    /// screenshot that stands in for it.</summary>
    private void BlitMainTo(SDL_GPUCommandBuffer* commandBuffer, SDL_GPUTexture* target)
    {
        // Use blit pipeline if available, otherwise just copy
        if (_blitPipeline != null)
        {
            SDL_GPUColorTargetInfo colorTargetInfo = new SDL_GPUColorTargetInfo
            {
                texture = target,
                load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_DONT_CARE,
                store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE
            };

            SDL_GPURenderPass* renderPass = SDL_BeginGPURenderPass(commandBuffer, &colorTargetInfo, 1, null);

            SDL_BindGPUGraphicsPipeline(renderPass, _blitPipeline);

            SDL_GPUTextureSamplerBinding textureSamplerBinding = new SDL_GPUTextureSamplerBinding
            {
                texture = _mainTarget.Resolve,
                sampler = _blitSampler.Sampler
            };
            SDL_BindGPUFragmentSamplers(renderPass, 0, &textureSamplerBinding, 1);

            SDL_DrawGPUPrimitives(renderPass, 3, 1, 0, 0);

            SDL_EndGPURenderPass(renderPass);
        }
        else
        {
            // Fallback: direct blit using SDL's blit functionality
            SDL_GPUBlitInfo blitInfo = new SDL_GPUBlitInfo
            {
                source = new SDL_GPUBlitRegion
                {
                    texture = _mainTarget.Resolve,
                    w = _windowWidth,
                    h = _windowHeight
                },
                destination = new SDL_GPUBlitRegion
                {
                    texture = target,
                    w = _windowWidth,
                    h = _windowHeight
                },
                load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_DONT_CARE,
                filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR
            };

            SDL_BlitGPUTexture(commandBuffer, &blitInfo);
        }
    }

    /// <summary>Records the frame again into a texture of the swapchain's format (the swapchain itself can't be
    /// read back), with or without the UI, then submits the frame's command buffer, waits and saves the PNG.</summary>
    private void CaptureScreenshot(SDL_GPUCommandBuffer* cmd, ImDrawDataPtr drawData)
    {
        uint w = _windowWidth, h = _windowHeight;
        var shot = CreateTexture(_swapchainFormat, w, h, SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_COLOR_TARGET);
        var downloadInfo = new SDL_GPUTransferBufferCreateInfo
        {
            usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_DOWNLOAD,
            size = w * h * 4
        };
        SDL_GPUTransferBuffer* download = SDL_CreateGPUTransferBuffer(_device, &downloadInfo);

        BlitMainTo(cmd, shot.Texture);
        if (_screenshotUi)
            DrawImGui(cmd, shot.Texture, drawData);

        SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(cmd);
        var region = new SDL_GPUTextureRegion { texture = shot.Texture, w = w, h = h, d = 1 };
        var transfer = new SDL_GPUTextureTransferInfo { transfer_buffer = download, pixels_per_row = w, rows_per_layer = h };
        SDL_DownloadFromGPUTexture(copyPass, &region, &transfer);
        SDL_EndGPUCopyPass(copyPass);

        SDL_GPUFence* fence = SDL_SubmitGPUCommandBufferAndAcquireFence(cmd);
        SDL_WaitForGPUFences(_device, true, &fence, 1);
        SDL_ReleaseGPUFence(_device, fence);

        byte* pixels = (byte*)SDL_MapGPUTransferBuffer(_device, download, false);
        if (pixels != null)
        {
            SaveScreenshotToPng(pixels, w, h, _screenshotPath);
            SDL_UnmapGPUTransferBuffer(_device, download);
        }
        else
            Console.Error.WriteLine("Failed to map screenshot transfer buffer");

        SDL_ReleaseGPUTransferBuffer(_device, download);
        DestroyTexture(shot);
        _screenshotTaken = true;
    }

    private void SaveScreenshotToPng(byte* pixelData, uint width, uint height, string path)
    {
        // Wrap the downloaded pixels, in the swapchain's format, in a surface (no copy) and save as PNG
        // (SDL's packed formats name the bits of a little-endian word: ABGR8888 is R,G,B,A in memory)
        SDL_PixelFormat format = _swapchainFormat switch
        {
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_B8G8R8A8_UNORM or
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_B8G8R8A8_UNORM_SRGB => SDL_PixelFormat.SDL_PIXELFORMAT_ARGB8888,
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM or
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM_SRGB => SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888,
            SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R10G10B10A2_UNORM => SDL_PixelFormat.SDL_PIXELFORMAT_ABGR2101010,
            _ => SDL_PixelFormat.SDL_PIXELFORMAT_UNKNOWN,
        };
        if (format == SDL_PixelFormat.SDL_PIXELFORMAT_UNKNOWN)
        {
            Console.Error.WriteLine($"Failed to save screenshot: no pixel format for {_swapchainFormat}");
            return;
        }
        SDL_Surface* surface = SDL_CreateSurfaceFrom((int)width, (int)height, format, (IntPtr)pixelData, (int)width * 4);
        if (surface == null)
        {
            Console.Error.WriteLine($"Failed to save screenshot: {SDL_GetError()}");
            return;
        }

        try
        {
            if (!IMG_SavePNG(surface, path))
            {
                Console.Error.WriteLine($"Failed to save screenshot: {SDL_GetError()}");
                return;
            }

            Console.WriteLine($"Screenshot saved to: {path}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to save screenshot: {ex.Message}");
        }
        finally
        {
            SDL_DestroySurface(surface);
        }
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Disposes of all engine resources.
    /// </summary>
    public void Dispose()
    {
        if (!_isInitialized) return;

        // Wait for GPU to finish
        SDL_WaitForGPUIdle(_device);

        Scene?.Dispose();
        Scene = null;

        // Dispose ImGui
        _imguiRenderer?.Dispose();
        ImGui.DestroyContext();

        // Dispose loaded scenes
        foreach (var scene in _loadedScenes)
        {
            scene.Dispose();
        }
        _loadedScenes.Clear();

        // Release pipelines
        if (_blitPipeline != null)
            SDL_ReleaseGPUGraphicsPipeline(_device, _blitPipeline);
        _pbrMaterial.Dispose(this);

        // Release samplers
        DestroySampler(_blitSampler);
        DestroySampler(_defaultSamplerLinear);
        DestroySampler(_defaultSamplerNearest);

        // Release textures
        DestroyTexture(_whiteTexture);
        DestroyTexture(_blackTexture);
        DestroyTexture(_errorCheckerboardTexture);
        DestroyTexture(_defaultNormalTexture);
        _mainTarget?.Dispose();

        // Release window and device
        SDL_ReleaseWindowFromGPUDevice(_device, _window);
        SDL_DestroyWindow(_window);
        SDL_DestroyGPUDevice(_device);
        SDL_Quit();

        _isInitialized = false;
        Console.WriteLine("Engine disposed");
    }

    #endregion
}
