using System.Runtime.InteropServices;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Gpu;

/// <summary>
/// Fluent builder for creating SDL GPU graphics pipelines.
/// </summary>
public unsafe class GpuPipelineBuilder
{
    private SDL_GPUShader* _vertexShader;
    private SDL_GPUShader* _fragmentShader;
    private readonly List<SDL_GPUVertexBufferDescription> _vertexBuffers = new();
    private readonly List<SDL_GPUVertexAttribute> _vertexAttributes = new();
    private SDL_GPUPrimitiveType _primitiveType = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST;
    private SDL_GPUFillMode _fillMode = SDL_GPUFillMode.SDL_GPU_FILLMODE_FILL;
    private SDL_GPUCullMode _cullMode = SDL_GPUCullMode.SDL_GPU_CULLMODE_BACK;
    private SDL_GPUFrontFace _frontFace = SDL_GPUFrontFace.SDL_GPU_FRONTFACE_COUNTER_CLOCKWISE;
    private bool _depthTestEnabled = true;
    private bool _depthWriteEnabled = true;
    private SDL_GPUCompareOp _depthCompareOp = SDL_GPUCompareOp.SDL_GPU_COMPAREOP_GREATER_OR_EQUAL;
    private SDL_GPUTextureFormat _colorFormat = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R16G16B16A16_FLOAT;
    private SDL_GPUTextureFormat _depthFormat = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D32_FLOAT;
    private bool _hasDepthTarget = true;
    private SDL_GPUSampleCount _sampleCount = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1;
    private bool _blendEnabled = false;
    private SDL_GPUBlendFactor _srcColorBlendFactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE;
    private SDL_GPUBlendFactor _dstColorBlendFactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ZERO;
    private SDL_GPUBlendOp _colorBlendOp = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD;
    private SDL_GPUBlendFactor _srcAlphaBlendFactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE;
    private SDL_GPUBlendFactor _dstAlphaBlendFactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ZERO;
    private SDL_GPUBlendOp _alphaBlendOp = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD;

    /// <summary>
    /// Sets the vertex and fragment shaders.
    /// </summary>
    public GpuPipelineBuilder SetShaders(SDL_GPUShader* vertexShader, SDL_GPUShader* fragmentShader)
    {
        _vertexShader = vertexShader;
        _fragmentShader = fragmentShader;
        return this;
    }

    /// <summary>
    /// Sets the primitive topology.
    /// </summary>
    public GpuPipelineBuilder SetPrimitiveType(SDL_GPUPrimitiveType type)
    {
        _primitiveType = type;
        return this;
    }

    /// <summary>
    /// Sets the polygon fill mode.
    /// </summary>
    public GpuPipelineBuilder SetPolygonMode(SDL_GPUFillMode mode)
    {
        _fillMode = mode;
        return this;
    }

    /// <summary>
    /// Sets the face culling mode.
    /// </summary>
    public GpuPipelineBuilder SetCullMode(SDL_GPUCullMode mode)
    {
        _cullMode = mode;
        return this;
    }

    /// <summary>
    /// Sets the front face winding order.
    /// </summary>
    public GpuPipelineBuilder SetFrontFace(SDL_GPUFrontFace frontFace)
    {
        _frontFace = frontFace;
        return this;
    }

    /// <summary>
    /// Enables depth testing with specified parameters.
    /// </summary>
    public GpuPipelineBuilder EnableDepthTest(bool write, SDL_GPUCompareOp compareOp)
    {
        _depthTestEnabled = true;
        _depthWriteEnabled = write;
        _depthCompareOp = compareOp;
        return this;
    }

    /// <summary>
    /// Disables depth testing.
    /// </summary>
    public GpuPipelineBuilder DisableDepthTest()
    {
        _depthTestEnabled = false;
        _depthWriteEnabled = false;
        return this;
    }

    /// <summary>
    /// Sets the color target format.
    /// </summary>
    public GpuPipelineBuilder SetColorFormat(SDL_GPUTextureFormat format)
    {
        _colorFormat = format;
        return this;
    }

    /// <summary>
    /// Sets the depth target format.
    /// </summary>
    public GpuPipelineBuilder SetDepthFormat(SDL_GPUTextureFormat format)
    {
        _depthFormat = format;
        _hasDepthTarget = true;
        return this;
    }

    /// <summary>
    /// Disables the depth target.
    /// </summary>
    public GpuPipelineBuilder NoDepthTarget()
    {
        _hasDepthTarget = false;
        return this;
    }

    /// <summary>
    /// Sets the MSAA sample count.
    /// </summary>
    public GpuPipelineBuilder SetMultisampling(SDL_GPUSampleCount count)
    {
        _sampleCount = count;
        return this;
    }

    /// <summary>
    /// Disables blending.
    /// </summary>
    public GpuPipelineBuilder DisableBlending()
    {
        _blendEnabled = false;
        return this;
    }

    /// <summary>
    /// Enables alpha blending (src * srcAlpha + dst * (1 - srcAlpha)).
    /// </summary>
    public GpuPipelineBuilder EnableBlendingAlpha()
    {
        _blendEnabled = true;
        _srcColorBlendFactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_SRC_ALPHA;
        _dstColorBlendFactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
        _colorBlendOp = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD;
        _srcAlphaBlendFactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE;
        _dstAlphaBlendFactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
        _alphaBlendOp = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD;
        return this;
    }

    /// <summary>
    /// Enables additive blending (src + dst).
    /// </summary>
    public GpuPipelineBuilder EnableBlendingAdditive()
    {
        _blendEnabled = true;
        _srcColorBlendFactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_SRC_ALPHA;
        _dstColorBlendFactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE;
        _colorBlendOp = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD;
        _srcAlphaBlendFactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE;
        _dstAlphaBlendFactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE;
        _alphaBlendOp = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD;
        return this;
    }

    /// <summary>
    /// Clears the vertex input layout.
    /// </summary>
    public GpuPipelineBuilder ClearVertexLayout()
    {
        _vertexBuffers.Clear();
        _vertexAttributes.Clear();
        return this;
    }

    /// <summary>
    /// Adds a vertex buffer description.
    /// </summary>
    public GpuPipelineBuilder AddVertexBuffer(uint slot, uint pitch, SDL_GPUVertexInputRate inputRate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX)
    {
        _vertexBuffers.Add(new SDL_GPUVertexBufferDescription
        {
            slot = slot,
            pitch = pitch,
            input_rate = inputRate,
            instance_step_rate = 0
        });
        return this;
    }

    /// <summary>
    /// Adds a vertex attribute.
    /// </summary>
    public GpuPipelineBuilder AddVertexAttribute(uint location, uint bufferSlot, SDL_GPUVertexElementFormat format, uint offset)
    {
        _vertexAttributes.Add(new SDL_GPUVertexAttribute
        {
            location = location,
            buffer_slot = bufferSlot,
            format = format,
            offset = offset
        });
        return this;
    }

    /// <summary>
    /// Sets up the default vertex layout matching the Vertex struct.
    /// Layout: Position (vec3), UvX (float), Normal (vec3), UvY (float), Color (vec4)
    /// </summary>
    public GpuPipelineBuilder SetDefaultVertexLayout()
    {
        _vertexBuffers.Clear();
        _vertexAttributes.Clear();

        // Single vertex buffer with Vertex stride
        _vertexBuffers.Add(new SDL_GPUVertexBufferDescription
        {
            slot = 0,
            pitch = (uint)Vertex.SizeInBytes,
            input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX,
            instance_step_rate = 0
        });

        // Use Marshal.OffsetOf to get actual runtime offsets
        // Location 0: Position (vec3)
        _vertexAttributes.Add(new SDL_GPUVertexAttribute
        {
            location = 0,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3,
            offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.Position))
        });

        // Location 1: UvX (float)
        _vertexAttributes.Add(new SDL_GPUVertexAttribute
        {
            location = 1,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT,
            offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.UvX))
        });

        // Location 2: Normal (vec3)
        _vertexAttributes.Add(new SDL_GPUVertexAttribute
        {
            location = 2,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3,
            offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.Normal))
        });

        // Location 3: UvY (float)
        _vertexAttributes.Add(new SDL_GPUVertexAttribute
        {
            location = 3,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT,
            offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.UvY))
        });

        // Location 4: Color (vec4)
        _vertexAttributes.Add(new SDL_GPUVertexAttribute
        {
            location = 4,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT4,
            offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.Color))
        });

        return this;
    }

    /// <summary>
    /// Builds the graphics pipeline.
    /// </summary>
    public SDL_GPUGraphicsPipeline* Build(SDL_GPUDevice* device)
    {
        if (_vertexShader == null || _fragmentShader == null)
        {
            Console.Error.WriteLine("GpuPipelineBuilder: Shaders not set");
            return null;
        }

        // Allocate vertex buffer descriptions
        SDL_GPUVertexBufferDescription* vertexBufferDescs = null;
        if (_vertexBuffers.Count > 0)
        {
            vertexBufferDescs = (SDL_GPUVertexBufferDescription*)NativeMemory.Alloc(
                (nuint)(_vertexBuffers.Count * sizeof(SDL_GPUVertexBufferDescription)));
            for (int i = 0; i < _vertexBuffers.Count; i++)
            {
                vertexBufferDescs[i] = _vertexBuffers[i];
            }
        }

        // Allocate vertex attributes
        SDL_GPUVertexAttribute* vertexAttrs = null;
        if (_vertexAttributes.Count > 0)
        {
            vertexAttrs = (SDL_GPUVertexAttribute*)NativeMemory.Alloc(
                (nuint)(_vertexAttributes.Count * sizeof(SDL_GPUVertexAttribute)));
            for (int i = 0; i < _vertexAttributes.Count; i++)
            {
                vertexAttrs[i] = _vertexAttributes[i];
            }
        }

        // Color target description
        SDL_GPUColorTargetDescription colorTargetDesc = new SDL_GPUColorTargetDescription
        {
            format = _colorFormat,
            blend_state = new SDL_GPUColorTargetBlendState
            {
                enable_blend = _blendEnabled,
                src_color_blendfactor = _srcColorBlendFactor,
                dst_color_blendfactor = _dstColorBlendFactor,
                color_blend_op = _colorBlendOp,
                src_alpha_blendfactor = _srcAlphaBlendFactor,
                dst_alpha_blendfactor = _dstAlphaBlendFactor,
                alpha_blend_op = _alphaBlendOp,
                color_write_mask = (SDL_GPUColorComponentFlags)(
                    (uint)SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_R |
                    (uint)SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_G |
                    (uint)SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_B |
                    (uint)SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_A)
            }
        };

        SDL_GPUColorTargetDescription* colorTargetDescPtr = &colorTargetDesc;

        // Pipeline create info
        SDL_GPUGraphicsPipelineCreateInfo createInfo = new SDL_GPUGraphicsPipelineCreateInfo
        {
            vertex_shader = _vertexShader,
            fragment_shader = _fragmentShader,
            vertex_input_state = new SDL_GPUVertexInputState
            {
                vertex_buffer_descriptions = vertexBufferDescs,
                num_vertex_buffers = (uint)_vertexBuffers.Count,
                vertex_attributes = vertexAttrs,
                num_vertex_attributes = (uint)_vertexAttributes.Count
            },
            primitive_type = _primitiveType,
            rasterizer_state = new SDL_GPURasterizerState
            {
                fill_mode = _fillMode,
                cull_mode = _cullMode,
                front_face = _frontFace,
                depth_bias_constant_factor = 0.0f,
                depth_bias_clamp = 0.0f,
                depth_bias_slope_factor = 0.0f,
                enable_depth_bias = false,
                enable_depth_clip = true
            },
            multisample_state = new SDL_GPUMultisampleState
            {
                sample_count = _sampleCount,
                sample_mask = 0,
                enable_mask = false
            },
            depth_stencil_state = new SDL_GPUDepthStencilState
            {
                compare_op = _depthCompareOp,
                enable_depth_test = _depthTestEnabled,
                enable_depth_write = _depthWriteEnabled,
                enable_stencil_test = false
            },
            target_info = new SDL_GPUGraphicsPipelineTargetInfo
            {
                color_target_descriptions = colorTargetDescPtr,
                num_color_targets = 1,
                depth_stencil_format = _hasDepthTarget ? _depthFormat : 0,
                has_depth_stencil_target = _hasDepthTarget
            }
        };

        SDL_GPUGraphicsPipeline* pipeline = SDL_CreateGPUGraphicsPipeline(device, &createInfo);

        // Free allocated memory
        if (vertexBufferDescs != null)
            NativeMemory.Free(vertexBufferDescs);
        if (vertexAttrs != null)
            NativeMemory.Free(vertexAttrs);

        if (pipeline == null)
        {
            Console.Error.WriteLine($"GpuPipelineBuilder: Failed to create pipeline: {SDL_GetError()}");
        }

        return pipeline;
    }
}
