namespace ModsCommon.Trajectory {
    #region Using Statements

    using System.Collections.Generic;
    using System.Linq;
    using Colossal.Mathematics;
    using Unity.Mathematics;

    #endregion

    /// <summary>Winding order of a closed loop of trajectories (as seen from above, +Y up).</summary>
    public enum WindingDirection {
        ClockWise,
        CounterClockWise
    }

    /// <summary>
    /// Small math helpers shared by the <see cref="ITrajectory"/> implementations that don't naturally
    /// belong on a single concrete type.
    /// </summary>
    public static class TrajectoryExtensions {
        /// <summary>
        /// Winding order of a closed contour (a sequence of trajectories where each one's end connects to
        /// the next one's start). Used by <see cref="ModsCommon.Geometry.Triangulator"/> to know which
        /// side of each edge is "inside" the polygon being triangulated.
        /// </summary>
        public static WindingDirection GetDirection(this IEnumerable<ITrajectory> trajectories) {
            var contour = trajectories.ToArray();
            var clockwiseVotes = 0;

            for (var i = 0; i < contour.Length; i += 1) {
                var next = contour[(i + 1) % contour.Length];
                clockwiseVotes += math.cross(-contour[i].Direction, next.Direction).y < 0f ? 1 : -1;
            }

            return clockwiseVotes >= 0 ? WindingDirection.ClockWise : WindingDirection.CounterClockWise;
        }

        /// <summary>
        /// Rotates a direction vector 90 degrees within the horizontal (XZ) plane, preserving its Y
        /// component's sign contribution as zero (the result always has Y = 0). Used to turn a
        /// trajectory's tangent into the sideways-offset normal consumed by <c>Shift</c>.
        /// </summary>
        /// <param name="direction">The direction to rotate; does not need to be normalized.</param>
        /// <param name="clockwise">When true, rotates clockwise (as seen from above); otherwise counter-clockwise.</param>
        public static float3 Turn90(this float3 direction, bool clockwise) {
            var flat = new float2(direction.x, direction.z);
            if (!MathUtils.TryNormalize(ref flat)) {
                return float3.zero;
            }

            var turned = clockwise ? new float2(flat.y, -flat.x) : new float2(-flat.y, flat.x);
            return new float3(turned.x, 0f, turned.y);
        }

        /// <summary>
        /// Angle, in degrees, between two direction vectors (not required to be normalized). Used by
        /// <see cref="BezierTrajectory.DeltaAngle"/> and <see cref="CombinedTrajectory.DeltaAngle"/>.
        /// </summary>
        public static float AngleDegrees(float3 a, float3 b) {
            var normalizedA = a;
            var normalizedB = b;
            if (!MathUtils.TryNormalize(ref normalizedA) || !MathUtils.TryNormalize(ref normalizedB)) {
                return 0f;
            }

            var dot = math.clamp(math.dot(normalizedA, normalizedB), -1f, 1f);
            return math.degrees(math.acos(dot));
        }
    }
}
