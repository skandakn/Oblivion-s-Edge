using System;
using UnityEngine;

namespace OblivionsEdge.Puzzles
{
    /// <summary>
    /// Mathematical utility for physically correct specular laser ray reflection.
    /// Implements the authoritative reflection rule:
    /// <code>
    /// r = d - 2 * (d · n) * n
    /// </code>
    /// where:
    /// <list type="bullet">
    /// <item><description><c>d</c> is the incoming unit direction vector.</description></item>
    /// <item><description><c>n</c> is the normalized surface normal vector.</description></item>
    /// <item><description><c>r</c> is the resulting reflected unit direction vector.</description></item>
    /// </list>
    /// </summary>
    public static class LaserReflectionMath
    {
        /// <summary>
        /// Calculates the reflected 2D direction vector using the specular reflection rule:
        /// r = d - 2(d · n)n.
        /// </summary>
        /// <param name="incomingDirection">Incoming beam direction vector (will be normalized).</param>
        /// <param name="surfaceNormal">Surface normal vector at point of impact (will be normalized).</param>
        /// <returns>Normalized reflected 2D direction vector.</returns>
        public static Vector2 Reflect2D(Vector2 incomingDirection, Vector2 surfaceNormal)
        {
            if (incomingDirection.sqrMagnitude < 0.00001f || surfaceNormal.sqrMagnitude < 0.00001f)
            {
                return Vector2.zero;
            }

            Vector2 d = incomingDirection.normalized;
            Vector2 n = surfaceNormal.normalized;

            // r = d - 2(d · n)n
            float dot = Vector2.Dot(d, n);
            Vector2 reflection = d - 2f * dot * n;

            return reflection.normalized;
        }

        /// <summary>
        /// Calculates the reflected 3D direction vector using the specular reflection rule:
        /// r = d - 2(d · n)n.
        /// </summary>
        /// <param name="incomingDirection">Incoming beam direction vector (will be normalized).</param>
        /// <param name="surfaceNormal">Surface normal vector at point of impact (will be normalized).</param>
        /// <returns>Normalized reflected 3D direction vector.</returns>
        public static Vector3 Reflect3D(Vector3 incomingDirection, Vector3 surfaceNormal)
        {
            if (incomingDirection.sqrMagnitude < 0.00001f || surfaceNormal.sqrMagnitude < 0.00001f)
            {
                return Vector3.zero;
            }

            Vector3 d = incomingDirection.normalized;
            Vector3 n = surfaceNormal.normalized;

            // r = d - 2(d · n)n
            float dot = Vector3.Dot(d, n);
            Vector3 reflection = d - 2f * dot * n;

            return reflection.normalized;
        }

        /// <summary>
        /// Calculates 2D normal vector perpendicular to a mirror surface oriented at a given angle (in degrees).
        /// Standard 0 degrees faces Vector2.up (0, 1).
        /// </summary>
        /// <param name="angleDegrees">Rotation angle in degrees around the Z axis.</param>
        /// <returns>Normalized 2D normal vector.</returns>
        public static Vector2 AngleToNormal2D(float angleDegrees)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            // For a mirror plane aligned horizontally at 0 deg, its normal points Up (0, 1):
            // (-sin(rad), cos(rad)) or cos/sin depending on convention.
            // When angle is 0, normal is (0, 1). When 90 deg, normal is (-1, 0).
            return new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)).normalized;
        }

        /// <summary>
        /// Verifies whether an incoming ray approaches the front reflective face of a surface
        /// having normal <paramref name="surfaceNormal"/>.
        /// In specular physics, a ray can only hit the front face if d · n &lt; 0.
        /// </summary>
        /// <param name="incomingDirection">Incoming beam direction.</param>
        /// <param name="surfaceNormal">Surface normal.</param>
        /// <returns>True if the ray strikes the front face.</returns>
        public static bool IsFacingSurface(Vector2 incomingDirection, Vector2 surfaceNormal)
        {
            return Vector2.Dot(incomingDirection.normalized, surfaceNormal.normalized) < -0.001f;
        }
    }
}
