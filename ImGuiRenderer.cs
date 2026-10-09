using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using SDL;
using static SDL.SDL3;

namespace Fleetwright;

/// <summary>
/// Renders ImGui using SDL3 GPU API.
/// Based on imgui_impl_sdlgpu3.cpp from Dear ImGui.
/// </summary>
public unsafe class ImGuiRenderer : IDisposable
{
    private readonly SDL_GPUDevice* _device;
    private readonly SDL_Window* _window;

    // Pipeline resources
    private SDL_GPUGraphicsPipeline* _pipeline;
    private SDL_GPUSampler* _sampler;

    // Font texture
    private SDL_GPUTexture* _fontTexture;

    // Buffers
    private SDL_GPUBuffer* _vertexBuffer;
    private SDL_GPUBuffer* _indexBuffer;
    private SDL_GPUTransferBuffer* _vertexTransferBuffer;
    private SDL_GPUTransferBuffer* _indexTransferBuffer;
    private uint _vertexBufferSize;
    private uint _indexBufferSize;

    // Target format
    private readonly SDL_GPUTextureFormat _colorTargetFormat;

    public ImGuiRenderer(SDL_GPUDevice* device, SDL_Window* window, SDL_GPUTextureFormat colorTargetFormat)
    {
        _device = device;
        _window = window;
        _colorTargetFormat = colorTargetFormat;

        CreateDeviceObjects();
    }

    private void CreateDeviceObjects()
    {
        CreateSampler();
        CreatePipeline();
        CreateFontTexture();
    }

    private SDL_GPUShader* LoadShader(string path, SDL_GPUShaderStage stage, uint numSamplers, uint numUniformBuffers)
    {
        if (!System.IO.File.Exists(path))
        {
            Console.WriteLine($"Shader file not found: {path}");
            return null;
        }

        byte[] code = System.IO.File.ReadAllBytes(path);
        return LoadShaderFromBytes(code, SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV, stage, numSamplers, numUniformBuffers);
    }

    private SDL_GPUShader* LoadShaderFromBytes(byte[] code, SDL_GPUShaderFormat format, SDL_GPUShaderStage stage, uint numSamplers, uint numUniformBuffers)
    {
        fixed (byte* codePtr = code)
        {
            byte* entrypoint = (byte*)Marshal.StringToHGlobalAnsi("main");
            SDL_GPUShaderCreateInfo shaderInfo = new()
            {
                code = codePtr,
                code_size = (nuint)code.Length,
                entrypoint = entrypoint,
                format = format,
                stage = stage,
                num_uniform_buffers = numUniformBuffers,
                num_samplers = numSamplers,
                num_storage_buffers = 0,
                num_storage_textures = 0
            };
            SDL_GPUShader* shader = SDL_CreateGPUShader(_device, &shaderInfo);
            Marshal.FreeHGlobal((nint)entrypoint);
            return shader;
        }
    }

    private void CreateSampler()
    {
        SDL_GPUSamplerCreateInfo samplerInfo = new()
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_LINEAR,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
        };

        _sampler = SDL_CreateGPUSampler(_device, &samplerInfo);
    }

    private void CreatePipeline()
    {
        string driver = SDL_GetGPUDeviceDriver(_device) ?? "";

        // Load shaders from files
        SDL_GPUShader* vertexShader;
        SDL_GPUShader* fragmentShader;

        if (driver == "vulkan")
        {
            // Use embedded shaders which are known to work
            vertexShader = LoadShader("Content/Shaders/Compiled/imgui.vert.spv",                
                SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1);
            fragmentShader = LoadShader("Content/Shaders/Compiled/imgui.frag.spv",                
                SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 1, 0);
        }
        else if (driver == "direct3d12")
        {
            // Fall back to embedded DXBC shaders for D3D12
            vertexShader = LoadShaderFromBytes(DxbcVertexShader,
                SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXBC,
                SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1);
            fragmentShader = LoadShaderFromBytes(DxbcFragmentShader,
                SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXBC,
                SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 1, 0);
        }
        else
        {
            throw new NotSupportedException($"Unsupported GPU driver: {driver}");
        }

        if (vertexShader == null)
            Console.WriteLine($"ERROR: Failed to create ImGui vertex shader: {SDL_GetError()}");
        if (fragmentShader == null)
            Console.WriteLine($"ERROR: Failed to create ImGui fragment shader: {SDL_GetError()}");

        // Vertex buffer description
        SDL_GPUVertexBufferDescription vertexBufferDesc = new()
        {
            slot = 0,
            input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX,
            instance_step_rate = 0,
            pitch = (uint)sizeof(ImDrawVert)
        };

        // Vertex attributes matching ImDrawVert: pos (float2), uv (float2), col (ubyte4)
        SDL_GPUVertexAttribute* vertexAttributes = stackalloc SDL_GPUVertexAttribute[3];
        vertexAttributes[0] = new SDL_GPUVertexAttribute
        {
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2,
            location = 0,
            offset = 0 // pos
        };
        vertexAttributes[1] = new SDL_GPUVertexAttribute
        {
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2,
            location = 1,
            offset = 8 // uv
        };
        vertexAttributes[2] = new SDL_GPUVertexAttribute
        {
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_UBYTE4_NORM,
            location = 2,
            offset = 16 // col
        };

        SDL_GPUVertexInputState vertexInputState = new()
        {
            num_vertex_buffers = 1,
            vertex_buffer_descriptions = &vertexBufferDesc,
            num_vertex_attributes = 3,
            vertex_attributes = vertexAttributes
        };

        // Rasterizer state
        SDL_GPURasterizerState rasterizerState = new()
        {
            fill_mode = SDL_GPUFillMode.SDL_GPU_FILLMODE_FILL,
            cull_mode = SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE,
            front_face = SDL_GPUFrontFace.SDL_GPU_FRONTFACE_COUNTER_CLOCKWISE,
            enable_depth_bias = false,
            enable_depth_clip = false
        };

        // Multisample state
        SDL_GPUMultisampleState multisampleState = new()
        {
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
            enable_mask = false
        };

        // Depth stencil state (disabled)
        SDL_GPUDepthStencilState depthStencilState = new()
        {
            enable_depth_test = false,
            enable_depth_write = false,
            enable_stencil_test = false
        };

        // Blend state (alpha blending)
        SDL_GPUColorTargetBlendState blendState = new()
        {
            enable_blend = true,
            src_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_SRC_ALPHA,
            dst_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
            color_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD,
            src_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE,
            dst_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
            alpha_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD,
            color_write_mask = SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_R |
                               SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_G |
                               SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_B |
                               SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_A
        };

        SDL_GPUColorTargetDescription colorTargetDesc = new()
        {
            format = _colorTargetFormat,
            blend_state = blendState
        };

        SDL_GPUGraphicsPipelineTargetInfo targetInfo = new()
        {
            num_color_targets = 1,
            color_target_descriptions = &colorTargetDesc,
            has_depth_stencil_target = false
        };

        SDL_GPUGraphicsPipelineCreateInfo pipelineInfo = new()
        {
            vertex_shader = vertexShader,
            fragment_shader = fragmentShader,
            vertex_input_state = vertexInputState,
            primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST,
            rasterizer_state = rasterizerState,
            multisample_state = multisampleState,
            depth_stencil_state = depthStencilState,
            target_info = targetInfo
        };

        _pipeline = SDL_CreateGPUGraphicsPipeline(_device, &pipelineInfo);

        if (_pipeline == null)
        {
            Console.WriteLine($"ERROR: Failed to create ImGui pipeline: {SDL_GetError()}");
        }
        else
        {
            Console.WriteLine("ImGui pipeline created successfully");
        }

        // Release shaders
        SDL_ReleaseGPUShader(_device, vertexShader);
        SDL_ReleaseGPUShader(_device, fragmentShader);
    }

    private void CreateFontTexture()
    {
        ImGuiIOPtr io = ImGui.GetIO();

        // Get font texture data
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bytesPerPixel);
        uint uploadSize = (uint)(width * height * bytesPerPixel);

        // Create texture
        SDL_GPUTextureCreateInfo textureInfo = new()
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER,
            width = (uint)width,
            height = (uint)height,
            layer_count_or_depth = 1,
            num_levels = 1,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1
        };

        _fontTexture = SDL_CreateGPUTexture(_device, &textureInfo);

        // Create transfer buffer
        SDL_GPUTransferBufferCreateInfo transferInfo = new()
        {
            usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
            size = uploadSize
        };

        SDL_GPUTransferBuffer* transferBuffer = SDL_CreateGPUTransferBuffer(_device, &transferInfo);

        // Copy data to transfer buffer
        void* mappedPtr = (void*)SDL_MapGPUTransferBuffer(_device, transferBuffer, false);
        Buffer.MemoryCopy((void*)pixels, mappedPtr, uploadSize, uploadSize);
        SDL_UnmapGPUTransferBuffer(_device, transferBuffer);

        // Upload to texture
        SDL_GPUCommandBuffer* cmdBuffer = SDL_AcquireGPUCommandBuffer(_device);
        SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(cmdBuffer);

        SDL_GPUTextureTransferInfo srcInfo = new()
        {
            transfer_buffer = transferBuffer,
            offset = 0
        };

        SDL_GPUTextureRegion dstRegion = new()
        {
            texture = _fontTexture,
            w = (uint)width,
            h = (uint)height,
            d = 1
        };

        SDL_UploadToGPUTexture(copyPass, &srcInfo, &dstRegion, false);
        SDL_EndGPUCopyPass(copyPass);
        SDL_SubmitGPUCommandBuffer(cmdBuffer);

        SDL_ReleaseGPUTransferBuffer(_device, transferBuffer);

        // Store texture ID for ImGui
        io.Fonts.SetTexID((IntPtr)_fontTexture);
    }

    /// <summary>
    /// Process SDL event for ImGui input.
    /// </summary>
    public void ProcessEvent(SDL_Event* evt)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        SDL_EventType eventType = (SDL_EventType)evt->type;

        switch (eventType)
        {
            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                io.AddMousePosEvent(evt->motion.x, evt->motion.y);
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                io.AddMouseWheelEvent(evt->wheel.x, evt->wheel.y);
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                int button = evt->button.button switch
                {
                    1 => 0, // Left
                    2 => 2, // Middle
                    3 => 1, // Right
                    4 => 3, // X1
                    5 => 4, // X2
                    _ => -1
                };
                if (button >= 0)
                    io.AddMouseButtonEvent(button, eventType == SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN);
                break;

            case SDL_EventType.SDL_EVENT_TEXT_INPUT:
                string? text = Marshal.PtrToStringUTF8((IntPtr)evt->text.text);
                if (text != null)
                    io.AddInputCharactersUTF8(text);
                break;

            case SDL_EventType.SDL_EVENT_KEY_DOWN:
            case SDL_EventType.SDL_EVENT_KEY_UP:
                UpdateKeyModifiers(evt->key.mod);
                ImGuiKey key = TranslateKey(evt->key.key);
                if (key != ImGuiKey.None)
                    io.AddKeyEvent(key, eventType == SDL_EventType.SDL_EVENT_KEY_DOWN);
                break;
        }
    }

    private void UpdateKeyModifiers(SDL_Keymod mod)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.AddKeyEvent(ImGuiKey.ModCtrl, (mod & SDL_Keymod.SDL_KMOD_CTRL) != 0);
        io.AddKeyEvent(ImGuiKey.ModShift, (mod & SDL_Keymod.SDL_KMOD_SHIFT) != 0);
        io.AddKeyEvent(ImGuiKey.ModAlt, (mod & SDL_Keymod.SDL_KMOD_ALT) != 0);
        io.AddKeyEvent(ImGuiKey.ModSuper, (mod & SDL_Keymod.SDL_KMOD_GUI) != 0);
    }

    private static ImGuiKey TranslateKey(SDL_Keycode key)
    {
        return (uint)key switch
        {
            SDLK_TAB => ImGuiKey.Tab,
            SDLK_LEFT => ImGuiKey.LeftArrow,
            SDLK_RIGHT => ImGuiKey.RightArrow,
            SDLK_UP => ImGuiKey.UpArrow,
            SDLK_DOWN => ImGuiKey.DownArrow,
            SDLK_PAGEUP => ImGuiKey.PageUp,
            SDLK_PAGEDOWN => ImGuiKey.PageDown,
            SDLK_HOME => ImGuiKey.Home,
            SDLK_END => ImGuiKey.End,
            SDLK_INSERT => ImGuiKey.Insert,
            SDLK_DELETE => ImGuiKey.Delete,
            SDLK_BACKSPACE => ImGuiKey.Backspace,
            SDLK_SPACE => ImGuiKey.Space,
            SDLK_RETURN => ImGuiKey.Enter,
            SDLK_ESCAPE => ImGuiKey.Escape,
            SDLK_APOSTROPHE => ImGuiKey.Apostrophe,
            SDLK_COMMA => ImGuiKey.Comma,
            SDLK_MINUS => ImGuiKey.Minus,
            SDLK_PERIOD => ImGuiKey.Period,
            SDLK_SLASH => ImGuiKey.Slash,
            SDLK_SEMICOLON => ImGuiKey.Semicolon,
            SDLK_EQUALS => ImGuiKey.Equal,
            SDLK_LEFTBRACKET => ImGuiKey.LeftBracket,
            SDLK_BACKSLASH => ImGuiKey.Backslash,
            SDLK_RIGHTBRACKET => ImGuiKey.RightBracket,
            SDLK_GRAVE => ImGuiKey.GraveAccent,
            SDLK_CAPSLOCK => ImGuiKey.CapsLock,
            SDLK_SCROLLLOCK => ImGuiKey.ScrollLock,
            SDLK_NUMLOCKCLEAR => ImGuiKey.NumLock,
            SDLK_PRINTSCREEN => ImGuiKey.PrintScreen,
            SDLK_PAUSE => ImGuiKey.Pause,
            SDLK_KP_0 => ImGuiKey.Keypad0,
            SDLK_KP_1 => ImGuiKey.Keypad1,
            SDLK_KP_2 => ImGuiKey.Keypad2,
            SDLK_KP_3 => ImGuiKey.Keypad3,
            SDLK_KP_4 => ImGuiKey.Keypad4,
            SDLK_KP_5 => ImGuiKey.Keypad5,
            SDLK_KP_6 => ImGuiKey.Keypad6,
            SDLK_KP_7 => ImGuiKey.Keypad7,
            SDLK_KP_8 => ImGuiKey.Keypad8,
            SDLK_KP_9 => ImGuiKey.Keypad9,
            SDLK_KP_PERIOD => ImGuiKey.KeypadDecimal,
            SDLK_KP_DIVIDE => ImGuiKey.KeypadDivide,
            SDLK_KP_MULTIPLY => ImGuiKey.KeypadMultiply,
            SDLK_KP_MINUS => ImGuiKey.KeypadSubtract,
            SDLK_KP_PLUS => ImGuiKey.KeypadAdd,
            SDLK_KP_ENTER => ImGuiKey.KeypadEnter,
            SDLK_KP_EQUALS => ImGuiKey.KeypadEqual,
            SDLK_LCTRL => ImGuiKey.LeftCtrl,
            SDLK_LSHIFT => ImGuiKey.LeftShift,
            SDLK_LALT => ImGuiKey.LeftAlt,
            SDLK_LGUI => ImGuiKey.LeftSuper,
            SDLK_RCTRL => ImGuiKey.RightCtrl,
            SDLK_RSHIFT => ImGuiKey.RightShift,
            SDLK_RALT => ImGuiKey.RightAlt,
            SDLK_RGUI => ImGuiKey.RightSuper,
            SDLK_APPLICATION => ImGuiKey.Menu,
            SDLK_0 => ImGuiKey._0,
            SDLK_1 => ImGuiKey._1,
            SDLK_2 => ImGuiKey._2,
            SDLK_3 => ImGuiKey._3,
            SDLK_4 => ImGuiKey._4,
            SDLK_5 => ImGuiKey._5,
            SDLK_6 => ImGuiKey._6,
            SDLK_7 => ImGuiKey._7,
            SDLK_8 => ImGuiKey._8,
            SDLK_9 => ImGuiKey._9,
            SDLK_A => ImGuiKey.A,
            SDLK_B => ImGuiKey.B,
            SDLK_C => ImGuiKey.C,
            SDLK_D => ImGuiKey.D,
            SDLK_E => ImGuiKey.E,
            SDLK_F => ImGuiKey.F,
            SDLK_G => ImGuiKey.G,
            SDLK_H => ImGuiKey.H,
            SDLK_I => ImGuiKey.I,
            SDLK_J => ImGuiKey.J,
            SDLK_K => ImGuiKey.K,
            SDLK_L => ImGuiKey.L,
            SDLK_M => ImGuiKey.M,
            SDLK_N => ImGuiKey.N,
            SDLK_O => ImGuiKey.O,
            SDLK_P => ImGuiKey.P,
            SDLK_Q => ImGuiKey.Q,
            SDLK_R => ImGuiKey.R,
            SDLK_S => ImGuiKey.S,
            SDLK_T => ImGuiKey.T,
            SDLK_U => ImGuiKey.U,
            SDLK_V => ImGuiKey.V,
            SDLK_W => ImGuiKey.W,
            SDLK_X => ImGuiKey.X,
            SDLK_Y => ImGuiKey.Y,
            SDLK_Z => ImGuiKey.Z,
            SDLK_F1 => ImGuiKey.F1,
            SDLK_F2 => ImGuiKey.F2,
            SDLK_F3 => ImGuiKey.F3,
            SDLK_F4 => ImGuiKey.F4,
            SDLK_F5 => ImGuiKey.F5,
            SDLK_F6 => ImGuiKey.F6,
            SDLK_F7 => ImGuiKey.F7,
            SDLK_F8 => ImGuiKey.F8,
            SDLK_F9 => ImGuiKey.F9,
            SDLK_F10 => ImGuiKey.F10,
            SDLK_F11 => ImGuiKey.F11,
            SDLK_F12 => ImGuiKey.F12,
            _ => ImGuiKey.None
        };
    }

    /// <summary>
    /// Start a new ImGui frame.
    /// </summary>
    public void NewFrame(float deltaTime)
    {
        ImGuiIOPtr io = ImGui.GetIO();

        // Setup display size
        int w, h;
        SDL_GetWindowSize(_window, &w, &h);
        io.DisplaySize = new Vector2(w, h);
        io.DisplayFramebufferScale = Vector2.One;

        // Setup time step (must be > 0)
        io.DeltaTime = deltaTime > 0 ? deltaTime : 1.0f / 60.0f;

        // Update cursor
        if ((io.ConfigFlags & ImGuiConfigFlags.NoMouseCursorChange) == 0)
        {
            ImGuiMouseCursor cursor = ImGui.GetMouseCursor();
            if (cursor == ImGuiMouseCursor.None || io.MouseDrawCursor)
            {
                SDL_HideCursor();
            }
            else
            {
                SDL_ShowCursor();
            }
        }
    }

    /// <summary>
    /// Prepare draw data by uploading buffers. Must be called BEFORE the render pass.
    /// </summary>
    public void PrepareDrawData(ImDrawDataPtr drawData, SDL_GPUCommandBuffer* commandBuffer)
    {
        if (drawData.TotalVtxCount <= 0)
            return;

        uint vertexSize = (uint)(drawData.TotalVtxCount * sizeof(ImDrawVert));
        uint indexSize = (uint)(drawData.TotalIdxCount * sizeof(ushort));

        // Resize vertex buffer if needed
        if (_vertexBuffer == null || _vertexBufferSize < vertexSize)
        {
            if (_vertexBuffer != null)
            {
                SDL_ReleaseGPUBuffer(_device, _vertexBuffer);
                SDL_ReleaseGPUTransferBuffer(_device, _vertexTransferBuffer);
            }

            SDL_GPUBufferCreateInfo bufferInfo = new()
            {
                usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX,
                size = vertexSize
            };
            _vertexBuffer = SDL_CreateGPUBuffer(_device, &bufferInfo);
            _vertexBufferSize = vertexSize;

            SDL_GPUTransferBufferCreateInfo transferInfo = new()
            {
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
                size = vertexSize
            };
            _vertexTransferBuffer = SDL_CreateGPUTransferBuffer(_device, &transferInfo);
        }

        // Resize index buffer if needed
        if (_indexBuffer == null || _indexBufferSize < indexSize)
        {
            if (_indexBuffer != null)
            {
                SDL_ReleaseGPUBuffer(_device, _indexBuffer);
                SDL_ReleaseGPUTransferBuffer(_device, _indexTransferBuffer);
            }

            SDL_GPUBufferCreateInfo bufferInfo = new()
            {
                usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX,
                size = indexSize
            };
            _indexBuffer = SDL_CreateGPUBuffer(_device, &bufferInfo);
            _indexBufferSize = indexSize;

            SDL_GPUTransferBufferCreateInfo transferInfo = new()
            {
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
                size = indexSize
            };
            _indexTransferBuffer = SDL_CreateGPUTransferBuffer(_device, &transferInfo);
        }

        // Copy vertex data
        ImDrawVert* vtxDst = (ImDrawVert*)SDL_MapGPUTransferBuffer(_device, _vertexTransferBuffer, true);
        ushort* idxDst = (ushort*)SDL_MapGPUTransferBuffer(_device, _indexTransferBuffer, true);

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[n];
            Buffer.MemoryCopy((void*)cmdList.VtxBuffer.Data, vtxDst,
                cmdList.VtxBuffer.Size * sizeof(ImDrawVert),
                cmdList.VtxBuffer.Size * sizeof(ImDrawVert));
            Buffer.MemoryCopy((void*)cmdList.IdxBuffer.Data, idxDst,
                cmdList.IdxBuffer.Size * sizeof(ushort),
                cmdList.IdxBuffer.Size * sizeof(ushort));
            vtxDst += cmdList.VtxBuffer.Size;
            idxDst += cmdList.IdxBuffer.Size;
        }

        SDL_UnmapGPUTransferBuffer(_device, _vertexTransferBuffer);
        SDL_UnmapGPUTransferBuffer(_device, _indexTransferBuffer);

        // Upload to GPU
        SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(commandBuffer);

        SDL_GPUTransferBufferLocation vtxSrc = new()
        {
            transfer_buffer = _vertexTransferBuffer,
            offset = 0
        };
        SDL_GPUBufferRegion vtxDstRegion = new()
        {
            buffer = _vertexBuffer,
            offset = 0,
            size = vertexSize
        };
        SDL_UploadToGPUBuffer(copyPass, &vtxSrc, &vtxDstRegion, true);

        SDL_GPUTransferBufferLocation idxSrc = new()
        {
            transfer_buffer = _indexTransferBuffer,
            offset = 0
        };
        SDL_GPUBufferRegion idxDstRegion = new()
        {
            buffer = _indexBuffer,
            offset = 0,
            size = indexSize
        };
        SDL_UploadToGPUBuffer(copyPass, &idxSrc, &idxDstRegion, true);

        SDL_EndGPUCopyPass(copyPass);
    }

    /// <summary>
    /// Render ImGui draw data. Must be called inside a render pass.
    /// </summary>
    public void RenderDrawData(ImDrawDataPtr drawData, SDL_GPUCommandBuffer* commandBuffer, SDL_GPURenderPass* renderPass)
    {
        int fbWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        int fbHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);

       
        if (fbWidth <= 0 || fbHeight <= 0 || drawData.TotalVtxCount <= 0)
            return;

        SDL_BindGPUGraphicsPipeline(renderPass, _pipeline);

        SDL_GPUBufferBinding vertexBinding = new()
        {
            buffer = _vertexBuffer,
            offset = 0
        };
        SDL_BindGPUVertexBuffers(renderPass, 0, &vertexBinding, 1);

        SDL_GPUBufferBinding indexBinding = new()
        {
            buffer = _indexBuffer,
            offset = 0
        };
        SDL_BindGPUIndexBuffer(renderPass, &indexBinding, SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_16BIT);

        // Setup viewport
        SDL_GPUViewport viewport = new()
        {
            x = 0,
            y = 0,
            w = fbWidth,
            h = fbHeight,
            min_depth = 0.0f,
            max_depth = 1.0f
        };
        SDL_SetGPUViewport(renderPass, &viewport);

        // Setup scale and translate for vertex shader
        // Shader expects: struct { vec2 scale; vec2 translate; }
        float L = drawData.DisplayPos.X;
        float R = drawData.DisplayPos.X + drawData.DisplaySize.X;
        float T = drawData.DisplayPos.Y;
        float B = drawData.DisplayPos.Y + drawData.DisplaySize.Y;

        float* pushConstant = stackalloc float[4];
        pushConstant[0] = 2.0f / (R - L);           // scale.x
        pushConstant[1] = 2.0f / (T - B);           // scale.y
        pushConstant[2] = (R + L) / (L - R);        // translate.x
        pushConstant[3] = (T + B) / (B - T);        // translate.y

        SDL_PushGPUVertexUniformData(commandBuffer, 0, (nint)pushConstant, 16);

        // Render command lists
        Vector2 clipOff = drawData.DisplayPos;
        Vector2 clipScale = drawData.FramebufferScale;

        int globalVtxOffset = 0;
        int globalIdxOffset = 0;

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[n];

            for (int cmdI = 0; cmdI < cmdList.CmdBuffer.Size; cmdI++)
            {
                ImDrawCmdPtr cmd = cmdList.CmdBuffer[cmdI];

                // Project scissor/clipping rectangles into framebuffer space
                Vector2 clipMin = new(
                    (cmd.ClipRect.X - clipOff.X) * clipScale.X,
                    (cmd.ClipRect.Y - clipOff.Y) * clipScale.Y);
                Vector2 clipMax = new(
                    (cmd.ClipRect.Z - clipOff.X) * clipScale.X,
                    (cmd.ClipRect.W - clipOff.Y) * clipScale.Y);

                // Clamp
                if (clipMin.X < 0) clipMin.X = 0;
                if (clipMin.Y < 0) clipMin.Y = 0;
                if (clipMax.X > fbWidth) clipMax.X = fbWidth;
                if (clipMax.Y > fbHeight) clipMax.Y = fbHeight;
                if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y)
                    continue;

                // Apply scissor
                SDL_Rect scissorRect = new()
                {
                    x = (int)clipMin.X,
                    y = (int)clipMin.Y,
                    w = (int)(clipMax.X - clipMin.X),
                    h = (int)(clipMax.Y - clipMin.Y)
                };
                SDL_SetGPUScissor(renderPass, &scissorRect);

                // Bind texture
                SDL_GPUTextureSamplerBinding textureSamplerBinding = new()
                {
                    texture = (SDL_GPUTexture*)cmd.TextureId,
                    sampler = _sampler
                };
                SDL_BindGPUFragmentSamplers(renderPass, 0, &textureSamplerBinding, 1);

                // Draw
                SDL_DrawGPUIndexedPrimitives(renderPass, cmd.ElemCount, 1,
                    (uint)(cmd.IdxOffset + globalIdxOffset),
                    (int)(cmd.VtxOffset + globalVtxOffset), 0);
            }

            globalIdxOffset += cmdList.IdxBuffer.Size;
            globalVtxOffset += cmdList.VtxBuffer.Size;
        }

        // Reset scissor
        SDL_Rect fullScissor = new() { x = 0, y = 0, w = fbWidth, h = fbHeight };
        SDL_SetGPUScissor(renderPass, &fullScissor);
    }

    public void Dispose()
    {
        SDL_WaitForGPUIdle(_device);

        if (_pipeline != null) SDL_ReleaseGPUGraphicsPipeline(_device, _pipeline);
        if (_sampler != null) SDL_ReleaseGPUSampler(_device, _sampler);
        if (_fontTexture != null) SDL_ReleaseGPUTexture(_device, _fontTexture);
        if (_vertexBuffer != null) SDL_ReleaseGPUBuffer(_device, _vertexBuffer);
        if (_indexBuffer != null) SDL_ReleaseGPUBuffer(_device, _indexBuffer);
        if (_vertexTransferBuffer != null) SDL_ReleaseGPUTransferBuffer(_device, _vertexTransferBuffer);
        if (_indexTransferBuffer != null) SDL_ReleaseGPUTransferBuffer(_device, _indexTransferBuffer);
    }

    #region Embedded Shaders

    // SPIR-V shaders from imgui_impl_sdlgpu3_shaders.h
    private static readonly byte[] SpirvVertexShader = {
        3,2,35,7,0,0,1,0,11,0,13,0,55,0,0,0,0,0,0,0,17,0,2,0,1,0,0,0,11,0,6,0,1,0,0,0,71,76,83,76,46,115,116,100,46,52,53,48,0,0,0,0,14,0,3,0,0,0,0,0,1,0,0,0,15,0,10,0,0,0,0,0,4,0,0,0,109,
        97,105,110,0,0,0,0,11,0,0,0,15,0,0,0,21,0,0,0,30,0,0,0,31,0,0,0,3,0,3,0,2,0,0,0,194,1,0,0,4,0,10,0,71,76,95,71,79,79,71,76,69,95,99,112,112,95,115,116,121,108,101,95,108,105,110,101,
        95,100,105,114,101,99,116,105,118,101,0,0,4,0,8,0,71,76,95,71,79,79,71,76,69,95,105,110,99,108,117,100,101,95,100,105,114,101,99,116,105,118,101,0,5,0,4,0,4,0,0,0,109,97,105,110,0,
        0,0,0,5,0,3,0,9,0,0,0,0,0,0,0,6,0,5,0,9,0,0,0,0,0,0,0,67,111,108,111,114,0,0,0,6,0,4,0,9,0,0,0,1,0,0,0,85,86,0,0,5,0,3,0,11,0,0,0,79,117,116,0,5,0,4,0,15,0,0,0,97,67,111,108,111,114,
        0,0,5,0,3,0,21,0,0,0,97,85,86,0,5,0,6,0,28,0,0,0,103,108,95,80,101,114,86,101,114,116,101,120,0,0,0,0,6,0,6,0,28,0,0,0,0,0,0,0,103,108,95,80,111,115,105,116,105,111,110,0,6,0,7,0,28,
        0,0,0,1,0,0,0,103,108,95,80,111,105,110,116,83,105,122,101,0,0,0,0,6,0,7,0,28,0,0,0,2,0,0,0,103,108,95,67,108,105,112,68,105,115,116,97,110,99,101,0,6,0,7,0,28,0,0,0,3,0,0,0,103,108,
        95,67,117,108,108,68,105,115,116,97,110,99,101,0,5,0,3,0,30,0,0,0,0,0,0,0,5,0,4,0,31,0,0,0,97,80,111,115,0,0,0,0,5,0,6,0,33,0,0,0,117,80,117,115,104,67,111,110,115,116,97,110,116,0,
        0,0,6,0,5,0,33,0,0,0,0,0,0,0,117,83,99,97,108,101,0,0,6,0,6,0,33,0,0,0,1,0,0,0,117,84,114,97,110,115,108,97,116,101,0,0,5,0,3,0,35,0,0,0,112,99,0,0,71,0,4,0,11,0,0,0,30,0,0,0,0,0,0,
        0,71,0,4,0,15,0,0,0,30,0,0,0,2,0,0,0,71,0,4,0,21,0,0,0,30,0,0,0,1,0,0,0,71,0,3,0,28,0,0,0,2,0,0,0,72,0,5,0,28,0,0,0,0,0,0,0,11,0,0,0,0,0,0,0,72,0,5,0,28,0,0,0,1,0,0,0,11,0,0,0,1,0,
        0,0,72,0,5,0,28,0,0,0,2,0,0,0,11,0,0,0,3,0,0,0,72,0,5,0,28,0,0,0,3,0,0,0,11,0,0,0,4,0,0,0,71,0,4,0,31,0,0,0,30,0,0,0,0,0,0,0,71,0,3,0,33,0,0,0,2,0,0,0,72,0,5,0,33,0,0,0,0,0,0,0,35,
        0,0,0,0,0,0,0,72,0,5,0,33,0,0,0,1,0,0,0,35,0,0,0,8,0,0,0,71,0,4,0,35,0,0,0,33,0,0,0,0,0,0,0,71,0,4,0,35,0,0,0,34,0,0,0,1,0,0,0,19,0,2,0,2,0,0,0,33,0,3,0,3,0,0,0,2,0,0,0,22,0,3,0,6,
        0,0,0,32,0,0,0,23,0,4,0,7,0,0,0,6,0,0,0,4,0,0,0,23,0,4,0,8,0,0,0,6,0,0,0,2,0,0,0,30,0,4,0,9,0,0,0,7,0,0,0,8,0,0,0,32,0,4,0,10,0,0,0,3,0,0,0,9,0,0,0,59,0,4,0,10,0,0,0,11,0,0,0,3,0,0,
        0,21,0,4,0,12,0,0,0,32,0,0,0,1,0,0,0,43,0,4,0,12,0,0,0,13,0,0,0,0,0,0,0,32,0,4,0,14,0,0,0,1,0,0,0,7,0,0,0,59,0,4,0,14,0,0,0,15,0,0,0,1,0,0,0,32,0,4,0,17,0,0,0,3,0,0,0,7,0,0,0,43,0,
        4,0,12,0,0,0,19,0,0,0,1,0,0,0,32,0,4,0,20,0,0,0,1,0,0,0,8,0,0,0,59,0,4,0,20,0,0,0,21,0,0,0,1,0,0,0,32,0,4,0,23,0,0,0,3,0,0,0,8,0,0,0,21,0,4,0,25,0,0,0,32,0,0,0,0,0,0,0,43,0,4,0,25,
        0,0,0,26,0,0,0,1,0,0,0,28,0,4,0,27,0,0,0,6,0,0,0,26,0,0,0,30,0,6,0,28,0,0,0,7,0,0,0,6,0,0,0,27,0,0,0,27,0,0,0,32,0,4,0,29,0,0,0,3,0,0,0,28,0,0,0,59,0,4,0,29,0,0,0,30,0,0,0,3,0,0,0,
        59,0,4,0,20,0,0,0,31,0,0,0,1,0,0,0,30,0,4,0,33,0,0,0,8,0,0,0,8,0,0,0,32,0,4,0,34,0,0,0,2,0,0,0,33,0,0,0,59,0,4,0,34,0,0,0,35,0,0,0,2,0,0,0,32,0,4,0,36,0,0,0,2,0,0,0,8,0,0,0,43,0,4,
        0,6,0,0,0,43,0,0,0,0,0,0,0,43,0,4,0,6,0,0,0,44,0,0,0,0,0,128,63,43,0,4,0,6,0,0,0,49,0,0,0,0,0,128,191,32,0,4,0,50,0,0,0,3,0,0,0,6,0,0,0,54,0,5,0,2,0,0,0,4,0,0,0,0,0,0,0,3,0,0,0,248,
        0,2,0,5,0,0,0,61,0,4,0,7,0,0,0,16,0,0,0,15,0,0,0,65,0,5,0,17,0,0,0,18,0,0,0,11,0,0,0,13,0,0,0,62,0,3,0,18,0,0,0,16,0,0,0,61,0,4,0,8,0,0,0,22,0,0,0,21,0,0,0,65,0,5,0,23,0,0,0,24,0,0,
        0,11,0,0,0,19,0,0,0,62,0,3,0,24,0,0,0,22,0,0,0,61,0,4,0,8,0,0,0,32,0,0,0,31,0,0,0,65,0,5,0,36,0,0,0,37,0,0,0,35,0,0,0,13,0,0,0,61,0,4,0,8,0,0,0,38,0,0,0,37,0,0,0,133,0,5,0,8,0,0,0,
        39,0,0,0,32,0,0,0,38,0,0,0,65,0,5,0,36,0,0,0,40,0,0,0,35,0,0,0,19,0,0,0,61,0,4,0,8,0,0,0,41,0,0,0,40,0,0,0,129,0,5,0,8,0,0,0,42,0,0,0,39,0,0,0,41,0,0,0,81,0,5,0,6,0,0,0,45,0,0,0,42,
        0,0,0,0,0,0,0,81,0,5,0,6,0,0,0,46,0,0,0,42,0,0,0,1,0,0,0,80,0,7,0,7,0,0,0,47,0,0,0,45,0,0,0,46,0,0,0,43,0,0,0,44,0,0,0,65,0,5,0,17,0,0,0,48,0,0,0,30,0,0,0,13,0,0,0,62,0,3,0,48,0,0,
        0,47,0,0,0,65,0,6,0,50,0,0,0,51,0,0,0,30,0,0,0,13,0,0,0,26,0,0,0,61,0,4,0,6,0,0,0,52,0,0,0,51,0,0,0,133,0,5,0,6,0,0,0,53,0,0,0,52,0,0,0,49,0,0,0,65,0,6,0,50,0,0,0,54,0,0,0,30,0,0,0,
        13,0,0,0,26,0,0,0,62,0,3,0,54,0,0,0,53,0,0,0,253,0,1,0,56,0,1,0
    };

    private static readonly byte[] SpirvFragmentShader = {
        3,2,35,7,0,0,1,0,11,0,13,0,30,0,0,0,0,0,0,0,17,0,2,0,1,0,0,0,11,0,6,0,1,0,0,0,71,76,83,76,46,115,116,100,46,52,53,48,0,0,0,0,14,0,3,0,0,0,0,0,1,0,0,0,15,0,7,0,4,0,0,0,4,0,0,0,109,97,
        105,110,0,0,0,0,9,0,0,0,13,0,0,0,16,0,3,0,4,0,0,0,7,0,0,0,3,0,3,0,2,0,0,0,194,1,0,0,4,0,10,0,71,76,95,71,79,79,71,76,69,95,99,112,112,95,115,116,121,108,101,95,108,105,110,101,95,100,
        105,114,101,99,116,105,118,101,0,0,4,0,8,0,71,76,95,71,79,79,71,76,69,95,105,110,99,108,117,100,101,95,100,105,114,101,99,116,105,118,101,0,5,0,4,0,4,0,0,0,109,97,105,110,0,0,0,0,5,
        0,4,0,9,0,0,0,102,67,111,108,111,114,0,0,5,0,3,0,11,0,0,0,0,0,0,0,6,0,5,0,11,0,0,0,0,0,0,0,67,111,108,111,114,0,0,0,6,0,4,0,11,0,0,0,1,0,0,0,85,86,0,0,5,0,3,0,13,0,0,0,73,110,0,0,5,
        0,5,0,22,0,0,0,115,84,101,120,116,117,114,101,0,0,0,0,71,0,4,0,9,0,0,0,30,0,0,0,0,0,0,0,71,0,4,0,13,0,0,0,30,0,0,0,0,0,0,0,71,0,4,0,22,0,0,0,33,0,0,0,0,0,0,0,71,0,4,0,22,0,0,0,34,0,
        0,0,2,0,0,0,19,0,2,0,2,0,0,0,33,0,3,0,3,0,0,0,2,0,0,0,22,0,3,0,6,0,0,0,32,0,0,0,23,0,4,0,7,0,0,0,6,0,0,0,4,0,0,0,32,0,4,0,8,0,0,0,3,0,0,0,7,0,0,0,59,0,4,0,8,0,0,0,9,0,0,0,3,0,0,0,23,
        0,4,0,10,0,0,0,6,0,0,0,2,0,0,0,30,0,4,0,11,0,0,0,7,0,0,0,10,0,0,0,32,0,4,0,12,0,0,0,1,0,0,0,11,0,0,0,59,0,4,0,12,0,0,0,13,0,0,0,1,0,0,0,21,0,4,0,14,0,0,0,32,0,0,0,1,0,0,0,43,0,4,0,
        14,0,0,0,15,0,0,0,0,0,0,0,32,0,4,0,16,0,0,0,1,0,0,0,7,0,0,0,25,0,9,0,19,0,0,0,6,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,27,0,3,0,20,0,0,0,19,0,0,0,32,0,4,0,21,0,0,0,0,
        0,0,0,20,0,0,0,59,0,4,0,21,0,0,0,22,0,0,0,0,0,0,0,43,0,4,0,14,0,0,0,24,0,0,0,1,0,0,0,32,0,4,0,25,0,0,0,1,0,0,0,10,0,0,0,54,0,5,0,2,0,0,0,4,0,0,0,0,0,0,0,3,0,0,0,248,0,2,0,5,0,0,0,65,
        0,5,0,16,0,0,0,17,0,0,0,13,0,0,0,15,0,0,0,61,0,4,0,7,0,0,0,18,0,0,0,17,0,0,0,61,0,4,0,20,0,0,0,23,0,0,0,22,0,0,0,65,0,5,0,25,0,0,0,26,0,0,0,13,0,0,0,24,0,0,0,61,0,4,0,10,0,0,0,27,0,
        0,0,26,0,0,0,87,0,5,0,7,0,0,0,28,0,0,0,23,0,0,0,27,0,0,0,133,0,5,0,7,0,0,0,29,0,0,0,18,0,0,0,28,0,0,0,62,0,3,0,9,0,0,0,29,0,0,0,253,0,1,0,56,0,1,0
    };

    private static readonly byte[] DxbcVertexShader = {
        68,88,66,67,32,50,127,204,241,196,165,104,216,114,216,116,220,164,29,45,1,0,0,0,40,4,0,0,5,0,0,0,52,0,0,0,136,1,0,0,236,1,0,0,92,2,0,0,140,3,0,0,82,68,69,70,76,1,0,0,1,0,0,0,116,0,
        0,0,1,0,0,0,60,0,0,0,1,5,254,255,0,5,0,0,34,1,0,0,19,19,68,37,60,0,0,0,24,0,0,0,40,0,0,0,40,0,0,0,36,0,0,0,12,0,0,0,0,0,0,0,100,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,
        0,1,0,0,0,1,0,0,0,0,0,0,0,117,80,117,115,104,67,111,110,115,116,97,110,116,0,171,171,100,0,0,0,2,0,0,0,140,0,0,0,16,0,0,0,0,0,0,0,0,0,0,0,220,0,0,0,0,0,0,0,8,0,0,0,2,0,0,0,240,0,0,
        0,0,0,0,0,255,255,255,255,0,0,0,0,255,255,255,255,0,0,0,0,20,1,0,0,8,0,0,0,8,0,0,0,2,0,0,0,240,0,0,0,0,0,0,0,255,255,255,255,0,0,0,0,255,255,255,255,0,0,0,0,112,99,95,117,83,99,97,
        108,101,0,102,108,111,97,116,50,0,171,171,171,1,0,3,0,1,0,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,230,0,0,0,112,99,95,117,84,114,97,110,115,108,97,116,101,0,77,105,99,114,
        111,115,111,102,116,32,40,82,41,32,72,76,83,76,32,83,104,97,100,101,114,32,67,111,109,112,105,108,101,114,32,49,48,46,49,0,171,171,73,83,71,78,92,0,0,0,3,0,0,0,8,0,0,0,80,0,0,0,0,0,
        0,0,0,0,0,0,3,0,0,0,0,0,0,0,3,3,0,0,80,0,0,0,1,0,0,0,0,0,0,0,3,0,0,0,1,0,0,0,3,3,0,0,80,0,0,0,2,0,0,0,0,0,0,0,3,0,0,0,2,0,0,0,15,15,0,0,84,69,88,67,79,79,82,68,0,171,171,171,79,83,
        71,78,104,0,0,0,3,0,0,0,8,0,0,0,80,0,0,0,0,0,0,0,0,0,0,0,3,0,0,0,0,0,0,0,15,0,0,0,80,0,0,0,1,0,0,0,0,0,0,0,3,0,0,0,1,0,0,0,3,12,0,0,89,0,0,0,0,0,0,0,1,0,0,0,3,0,0,0,2,0,0,0,15,0,0,
        0,84,69,88,67,79,79,82,68,0,83,86,95,80,111,115,105,116,105,111,110,0,171,171,171,83,72,69,88,40,1,0,0,81,0,1,0,74,0,0,0,106,8,0,1,89,0,0,7,70,142,48,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,
        0,0,1,0,0,0,95,0,0,3,50,16,16,0,0,0,0,0,95,0,0,3,50,16,16,0,1,0,0,0,95,0,0,3,242,16,16,0,2,0,0,0,101,0,0,3,242,32,16,0,0,0,0,0,101,0,0,3,50,32,16,0,1,0,0,0,103,0,0,4,242,32,16,0,2,
        0,0,0,1,0,0,0,104,0,0,2,1,0,0,0,50,0,0,13,50,0,16,0,0,0,0,0,70,16,16,0,0,0,0,0,70,128,48,0,0,0,0,0,0,0,0,0,0,0,0,0,230,138,48,0,0,0,0,0,0,0,0,0,0,0,0,0,54,0,0,6,34,32,16,0,2,0,0,0,
        26,0,16,128,65,0,0,0,0,0,0,0,54,0,0,5,242,32,16,0,0,0,0,0,70,30,16,0,2,0,0,0,54,0,0,5,18,32,16,0,2,0,0,0,10,0,16,0,0,0,0,0,54,0,0,8,194,32,16,0,2,0,0,0,2,64,0,0,0,0,0,0,0,0,0,0,0,0,
        0,0,0,0,128,63,54,0,0,5,50,32,16,0,1,0,0,0,70,16,16,0,1,0,0,0,62,0,0,1,83,84,65,84,148,0,0,0,7,0,0,0,1,0,0,0,0,0,0,0,6,0,0,0,2,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,4,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0,0,0,0,0,0,0
    };

    private static readonly byte[] DxbcFragmentShader = {
        68,88,66,67,235,219,43,109,38,151,39,223,98,41,193,28,215,98,67,110,1,0,0,0,232,2,0,0,5,0,0,0,52,0,0,0,12,1,0,0,88,1,0,0,140,1,0,0,76,2,0,0,82,68,69,70,208,0,0,0,0,0,0,0,0,0,0,0,2,
        0,0,0,60,0,0,0,1,5,255,255,0,5,0,0,167,0,0,0,19,19,68,37,60,0,0,0,24,0,0,0,40,0,0,0,40,0,0,0,36,0,0,0,12,0,0,0,0,0,0,0,140,0,0,0,3,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0,0,
        0,0,2,0,0,0,0,0,0,0,158,0,0,0,2,0,0,0,5,0,0,0,4,0,0,0,255,255,255,255,0,0,0,0,1,0,0,0,12,0,0,0,2,0,0,0,0,0,0,0,95,115,84,101,120,116,117,114,101,95,115,97,109,112,108,101,114,0,115,
        84,101,120,116,117,114,101,0,77,105,99,114,111,115,111,102,116,32,40,82,41,32,72,76,83,76,32,83,104,97,100,101,114,32,67,111,109,112,105,108,101,114,32,49,48,46,49,0,171,73,83,71,78,
        68,0,0,0,2,0,0,0,8,0,0,0,56,0,0,0,0,0,0,0,0,0,0,0,3,0,0,0,0,0,0,0,15,15,0,0,56,0,0,0,1,0,0,0,0,0,0,0,3,0,0,0,1,0,0,0,3,3,0,0,84,69,88,67,79,79,82,68,0,171,171,171,79,83,71,78,44,0,
        0,0,1,0,0,0,8,0,0,0,32,0,0,0,0,0,0,0,0,0,0,0,3,0,0,0,0,0,0,0,15,0,0,0,83,86,95,84,97,114,103,101,116,0,171,171,83,72,69,88,184,0,0,0,81,0,0,0,46,0,0,0,106,8,0,1,90,0,0,6,70,110,48,
        0,0,0,0,0,0,0,0,0,0,0,0,0,2,0,0,0,88,24,0,7,70,126,48,0,0,0,0,0,0,0,0,0,0,0,0,0,85,85,0,0,2,0,0,0,98,16,0,3,242,16,16,0,0,0,0,0,98,16,0,3,50,16,16,0,1,0,0,0,101,0,0,3,242,32,16,0,0,
        0,0,0,104,0,0,2,1,0,0,0,69,0,0,11,242,0,16,0,0,0,0,0,70,16,16,0,1,0,0,0,70,126,32,0,0,0,0,0,0,0,0,0,0,96,32,0,0,0,0,0,0,0,0,0,56,0,0,7,242,32,16,0,0,0,0,0,70,14,16,0,0,0,0,0,70,30,
        16,0,0,0,0,0,62,0,0,1,83,84,65,84,148,0,0,0,3,0,0,0,1,0,0,0,0,0,0,0,3,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
    };

    #endregion
}
