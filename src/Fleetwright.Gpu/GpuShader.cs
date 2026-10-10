using System.Runtime.InteropServices;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Gpu;

/// <summary>Loads SPIR-V shaders. Only a device is needed, so renderers don't depend on the engine.</summary>
public static unsafe class GpuShader
{
    /// <summary>Loads a shader from a SPIR-V file. Content is copied next to the exe, so a relative path resolves
    /// against <see cref="AppContext.BaseDirectory"/>, not the working directory. Null (and a message) on failure.</summary>
    public static SDL_GPUShader* Load(SDL_GPUDevice* device, string path, SDL_GPUShaderStage stage,
        uint numSamplers, uint numStorageBuffers, uint numStorageTextures, uint numUniformBuffers)
    {
        path = Path.Combine(AppContext.BaseDirectory, path);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Shader file not found: {path}");
            return null;
        }

        byte[] code = File.ReadAllBytes(path);
        byte* entrypoint = (byte*)Marshal.StringToHGlobalAnsi("main");
        try
        {
            fixed (byte* codePtr = code)
            {
                var info = new SDL_GPUShaderCreateInfo
                {
                    code = codePtr,
                    code_size = (nuint)code.Length,
                    entrypoint = entrypoint,
                    format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
                    stage = stage,
                    num_samplers = numSamplers,
                    num_storage_buffers = numStorageBuffers,
                    num_storage_textures = numStorageTextures,
                    num_uniform_buffers = numUniformBuffers,
                };
                var shader = SDL_CreateGPUShader(device, &info);
                if (shader == null)
                    Console.Error.WriteLine($"Failed to create shader from {path}: {SDL_GetError()}");
                return shader;
            }
        }
        finally
        {
            Marshal.FreeHGlobal((nint)entrypoint);
        }
    }
}
