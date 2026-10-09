using System.Numerics;
using SDL;
using Fleetwright.Gpu;
using TracyWrapper;
using static SDL.SDL3;

namespace Fleetwright;

/// <summary>
/// FPS-style camera with keyboard and mouse input.
/// </summary>
public unsafe class Camera
{
    public Vector3 Position = new(30, 0, -85);  // Match C++ starting position
    public Vector3 Velocity = Vector3.Zero;

    /// <summary>Pitch angle in radians (up/down rotation).</summary>
    public float Pitch = 0.0f;

    /// <summary>Yaw angle in radians (left/right rotation).</summary>
    public float Yaw = 0.0f;

    /// <summary>Movement speed in units per second.</summary>
    public float MoveSpeed = 5.0f;

    /// <summary>Mouse sensitivity for look rotation.</summary>
    public float MouseSensitivity = 0.002f;

    /// <summary>Smoothing factor for velocity (0 = instant, 1 = no change).</summary>
    public float VelocitySmoothing = 0.9f;

    private bool _moveForward, _moveBackward, _moveLeft, _moveRight, _moveUp, _moveDown;
    private bool _mouseCaptured = false;

    /// <summary>
    /// Gets the view matrix for rendering.
    /// </summary>
    public Matrix4x4 GetViewMatrix()
    {
        // Match C++ approach: create camera model matrix and invert
        // C++ uses: inverse(translation * rotation) with GLM column-major
        // For row-major System.Numerics: inverse(rotation * translation)
        Matrix4x4 cameraTranslation = Matrix4x4.CreateTranslation(Position);
        Matrix4x4 cameraRotation = GetRotationMatrix();

        // Camera model matrix in row-major order
        Matrix4x4 cameraModel = cameraRotation * cameraTranslation;

        // Invert to get view matrix
        Matrix4x4.Invert(cameraModel, out Matrix4x4 viewMatrix);
        return viewMatrix;
    }

    /// <summary>
    /// Gets the rotation matrix from pitch and yaw.
    /// </summary>
    public Matrix4x4 GetRotationMatrix()
    {
        // Match C++ convention: yaw around -Y axis, then pitch around X
        // Using quaternions for proper composition
        Quaternion pitchRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, Pitch);
        Quaternion yawRotation = Quaternion.CreateFromAxisAngle(-Vector3.UnitY, Yaw);

        // C++ order: yaw * pitch (GLM column-major)
        // For row-major System.Numerics: pitch * yaw gives same result
        Quaternion combined = Quaternion.Concatenate(pitchRotation, yawRotation);
        return Matrix4x4.CreateFromQuaternion(combined);
    }

    /// <summary>
    /// Processes an SDL event for camera input.
    /// </summary>
    public void ProcessSdlEvent(SDL_Event* e)
    {
        SDL_EventType eventType = (SDL_EventType)e->type;

        switch (eventType)
        {
            case SDL_EventType.SDL_EVENT_KEY_DOWN:
            case SDL_EventType.SDL_EVENT_KEY_UP:
                {
                    bool pressed = eventType == SDL_EventType.SDL_EVENT_KEY_DOWN;
                    var key = (uint)e->key.key;

                    if (key == SDLK_W) _moveForward = pressed;
                    else if (key == SDLK_S) _moveBackward = pressed;
                    else if (key == SDLK_A) _moveLeft = pressed;
                    else if (key == SDLK_D) _moveRight = pressed;
                    else if (key == SDLK_SPACE) _moveUp = pressed;
                    else if (key == SDLK_LCTRL || key == SDLK_LSHIFT) _moveDown = pressed;
                }
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                if (e->button.button == 1) // Left click
                {
                    _mouseCaptured = true;
                    SDL_SetWindowRelativeMouseMode(e->button.windowID != 0 ? SDL_GetWindowFromID(e->button.windowID) : null, true);
                }
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                if (e->button.button == 1)
                {
                    _mouseCaptured = false;
                    SDL_SetWindowRelativeMouseMode(e->button.windowID != 0 ? SDL_GetWindowFromID(e->button.windowID) : null, false);
                }
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                if (_mouseCaptured)
                {
                    float dx = e->motion.xrel;
                    float dy = e->motion.yrel;

                    Yaw -= dx * MouseSensitivity;
                    Pitch -= dy * MouseSensitivity;

                    // Clamp pitch to prevent flipping
                    Pitch = GpuMath.Clamp(Pitch, -MathF.PI / 2.0f + 0.01f, MathF.PI / 2.0f - 0.01f);
                }
                break;
        }
    }

    /// <summary>
    /// Updates camera position based on current input state.
    /// Call once per frame.
    /// </summary>
    /// <param name="deltaTime">Time since last frame in seconds.</param>
    public void Update(float deltaTime)
    {
        // Build velocity in camera space (matching C++ approach)
        Vector3 inputVelocity = Vector3.Zero;

        if (_moveForward) inputVelocity.Z = -1;
        if (_moveBackward) inputVelocity.Z = 1;
        if (_moveRight) inputVelocity.X = 1;
        if (_moveLeft) inputVelocity.X = -1;
        if (_moveUp) inputVelocity.Y = 1;
        if (_moveDown) inputVelocity.Y = -1;

        // Transform velocity from camera space to world space
        // C++ does: position += rotation * velocity (column vector)
        // For row-major: worldVelocity = velocity * rotation
        Matrix4x4 rotation = GetRotationMatrix();
        Vector3 worldVelocity = Vector3.Transform(inputVelocity * MoveSpeed, rotation);

        // Apply velocity (C++ has simple approach, no smoothing in its update)
        Position += worldVelocity * deltaTime;
    }

    /// <summary>
    /// Gets whether the mouse is currently captured for look control.
    /// </summary>
    public bool IsMouseCaptured => _mouseCaptured;
}
