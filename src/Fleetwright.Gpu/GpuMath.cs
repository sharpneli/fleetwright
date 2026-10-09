using System.Numerics;

namespace Fleetwright.Gpu;

/// <summary>
/// Math utilities for GPU rendering, providing GLM-equivalent functions.
/// </summary>
public static class GpuMath
{
    /// <summary>
    /// Creates a perspective projection matrix.
    /// </summary>
    /// <param name="fovYRadians">Vertical field of view in radians.</param>
    /// <param name="aspectRatio">Aspect ratio (width / height).</param>
    /// <param name="nearPlane">Near clipping plane distance.</param>
    /// <param name="farPlane">Far clipping plane distance.</param>
    /// <returns>A perspective projection matrix.</returns>
    public static Matrix4x4 CreatePerspective(float fovYRadians, float aspectRatio, float nearPlane, float farPlane)
    {
        float tanHalfFov = MathF.Tan(fovYRadians / 2.0f);

        Matrix4x4 result = new Matrix4x4();

        result.M11 = 1.0f / (aspectRatio * tanHalfFov);
        result.M22 = 1.0f / tanHalfFov;
        result.M33 = farPlane / (nearPlane - farPlane);
        result.M34 = -1.0f;
        result.M43 = (nearPlane * farPlane) / (nearPlane - farPlane);
        result.M44 = 0.0f;

        return result;
    }

    /// <summary>
    /// Creates an infinite reverse-Z perspective projection matrix.
    /// This provides better depth precision for large scenes.
    /// </summary>
    /// <param name="fovYRadians">Vertical field of view in radians.</param>
    /// <param name="aspectRatio">Aspect ratio (width / height).</param>
    /// <param name="nearPlane">Near clipping plane distance.</param>
    /// <returns>An infinite reverse-Z perspective projection matrix.</returns>
    public static Matrix4x4 CreateInfiniteReversePerspective(float fovYRadians, float aspectRatio, float nearPlane)
    {
        float tanHalfFov = MathF.Tan(fovYRadians / 2.0f);

        Matrix4x4 result = new Matrix4x4();

        result.M11 = 1.0f / (aspectRatio * tanHalfFov);
        result.M22 = 1.0f / tanHalfFov;
        result.M33 = 0.0f;
        result.M34 = -1.0f;
        result.M43 = nearPlane;
        result.M44 = 0.0f;

        return result;
    }

    /// <summary>
    /// Creates a reverse-Z perspective projection matrix.
    /// Maps near plane to depth 1 and far plane to depth 0 for better precision.
    /// This matches the GLM trick of swapping near/far in glm::perspective.
    /// </summary>
    /// <param name="fovYRadians">Vertical field of view in radians.</param>
    /// <param name="aspectRatio">Aspect ratio (width / height).</param>
    /// <param name="nearPlane">Near clipping plane distance.</param>
    /// <param name="farPlane">Far clipping plane distance.</param>
    /// <returns>A reverse-Z perspective projection matrix.</returns>
    public static Matrix4x4 CreateReversePerspective(float fovYRadians, float aspectRatio, float nearPlane, float farPlane)
    {
        // This creates the same matrix as glm::perspective with swapped near/far
        // Near maps to 1, far maps to 0
        float tanHalfFov = MathF.Tan(fovYRadians / 2.0f);

        Matrix4x4 result = new Matrix4x4();

        result.M11 = 1.0f / (aspectRatio * tanHalfFov);
        result.M22 = 1.0f / tanHalfFov;
        // For reverse-Z: swap the depth calculation
        // Standard: M33 = far/(near-far), M43 = near*far/(near-far)
        // Reverse-Z (swap near/far in formula): M33 = near/(far-near), M43 = far*near/(far-near)
        result.M33 = nearPlane / (farPlane - nearPlane);
        result.M34 = -1.0f;
        result.M43 = (farPlane * nearPlane) / (farPlane - nearPlane);
        result.M44 = 0.0f;

        return result;
    }

    /// <summary>
    /// Creates a view matrix looking from eye position toward target.
    /// </summary>
    /// <param name="eye">Camera position.</param>
    /// <param name="target">Point to look at.</param>
    /// <param name="up">Up direction vector.</param>
    /// <returns>A view matrix.</returns>
    public static Matrix4x4 CreateLookAt(Vector3 eye, Vector3 target, Vector3 up)
    {
        return Matrix4x4.CreateLookAt(eye, target, up);
    }

    /// <summary>
    /// Creates an orthographic projection matrix.
    /// </summary>
    public static Matrix4x4 CreateOrthographic(float left, float right, float bottom, float top, float near, float far)
    {
        return Matrix4x4.CreateOrthographicOffCenter(left, right, bottom, top, near, far);
    }

    /// <summary>
    /// Converts degrees to radians.
    /// </summary>
    public static float DegreesToRadians(float degrees)
    {
        return degrees * (MathF.PI / 180.0f);
    }

    /// <summary>
    /// Converts radians to degrees.
    /// </summary>
    public static float RadiansToDegrees(float radians)
    {
        return radians * (180.0f / MathF.PI);
    }

    /// <summary>
    /// Linear interpolation between two values.
    /// </summary>
    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    /// <summary>
    /// Linear interpolation between two vectors.
    /// </summary>
    public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
    {
        return Vector3.Lerp(a, b, t);
    }

    /// <summary>
    /// Clamps a value between min and max.
    /// </summary>
    public static float Clamp(float value, float min, float max)
    {
        return MathF.Max(min, MathF.Min(max, value));
    }

    /// <summary>
    /// Extracts the forward direction from a rotation matrix.
    /// </summary>
    public static Vector3 GetForward(Matrix4x4 rotationMatrix)
    {
        return new Vector3(-rotationMatrix.M31, -rotationMatrix.M32, -rotationMatrix.M33);
    }

    /// <summary>
    /// Extracts the right direction from a rotation matrix.
    /// </summary>
    public static Vector3 GetRight(Matrix4x4 rotationMatrix)
    {
        return new Vector3(rotationMatrix.M11, rotationMatrix.M12, rotationMatrix.M13);
    }

    /// <summary>
    /// Extracts the up direction from a rotation matrix.
    /// </summary>
    public static Vector3 GetUp(Matrix4x4 rotationMatrix)
    {
        return new Vector3(rotationMatrix.M21, rotationMatrix.M22, rotationMatrix.M23);
    }
}
