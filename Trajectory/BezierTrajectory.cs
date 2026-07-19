namespace ModsCommon.Trajectory {
    #region Using Statements

    using System;
    using Colossal.Mathematics;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// An <see cref="ITrajectory"/> backed by a cubic <see cref="Bezier4x3"/> — the exact type
    /// <see cref="ModsCommon.Rendering.CustomOverlayRenderSystem.Buffer.DrawCurve(UnityEngine.Color, Bezier4x3, float, bool)"/>
    /// already accepts. Every member delegates to <see cref="Colossal.Mathematics.MathUtils"/>, which already
    /// implements the same closest-point/cut/divide/length algorithms CS1 ModsCommon hand-rolled.
    /// </summary>
    /// <remarks>
    /// This type intentionally ships only the plain <see cref="Bezier4x3"/>-wrapping constructor. CS1's
    /// <c>BezierTrajectory</c> also had a constructor that fit a curve's control points to a
    /// start/end position + tangent pair (tuned around <c>NetSegment.IsStraight</c> heuristics) — that
    /// belongs in the domain-model phase, where it can be built and validated against real
    /// <c>Game.Net</c> edge/node geometry instead of invented speculatively here. <see cref="Shift"/> and
    /// <see cref="Elevate(float, float)"/> below use a control-point-offset approximation in the
    /// meantime (see their remarks).
    /// </remarks>
    public readonly struct BezierTrajectory : ITrajectory, IEquatable<BezierTrajectory> {
        public TrajectoryType TrajectoryType => TrajectoryType.Bezier;
        public Bezier4x3 Trajectory { get; }

        public float Length { get; }
        public float Magnitude { get; }
        public float DeltaAngle { get; }
        public float3 Direction { get; }
        public float3 StartDirection { get; }
        public float3 EndDirection { get; }
        public float3 StartPosition => Trajectory.a;
        public float3 EndPosition => Trajectory.d;
        public bool IsZero => math.all(MathUtils.Min(Trajectory) == MathUtils.Max(Trajectory));

        public BezierTrajectory(Bezier4x3 trajectory) {
            Trajectory = trajectory;

            Magnitude = math.distance(trajectory.a, trajectory.d);
            Length = Magnitude <= 0.01f ? 0.01f : MathUtils.Length(trajectory);
            Direction = math.normalizesafe(trajectory.d - trajectory.a);
            StartDirection = math.normalizesafe(trajectory.b - trajectory.a);
            EndDirection = math.normalizesafe(trajectory.c - trajectory.d);
            DeltaAngle = 180f - TrajectoryExtensions.AngleDegrees(StartDirection, EndDirection);
        }

        public BezierTrajectory Cut(float t0, float t1) => new BezierTrajectory(MathUtils.Cut(Trajectory, new float2(t0, t1)));
        ITrajectory ITrajectory.Cut(float t0, float t1) => Cut(t0, t1);

        public void Divide(out ITrajectory trajectory1, out ITrajectory trajectory2) {
            MathUtils.Divide(Trajectory, out var bezier1, out var bezier2, 0.5f);
            trajectory1 = new BezierTrajectory(bezier1);
            trajectory2 = new BezierTrajectory(bezier2);
        }

        public float3 Position(float t) => MathUtils.Position(Trajectory, t);
        public float3 Tangent(float t) => MathUtils.Tangent(Trajectory, t);

        public float Travel(float distance) => Travel(0f, distance);

        public float Travel(float start, float distance) {
            var bounds = new Bounds1(start, 1f);
            var remaining = distance;
            MathUtils.ClampLength(Trajectory, ref bounds, ref remaining);
            return bounds.max;
        }

        public float Distance(float from = 0f, float to = 1f) => MathUtils.Length(Trajectory, new Bounds1(from, to));

        public BezierTrajectory Invert() => new BezierTrajectory(MathUtils.Invert(Trajectory));
        ITrajectory ITrajectory.Invert() => Invert();

        /// <remarks>
        /// Approximate: offsets each of the curve's 4 control points sideways along the curve's local
        /// tangent-normal, scaled by the interpolated shift amount at that control point's parameter
        /// (0, 1/3, 2/3, 1). This keeps the curve roughly parallel to the original without needing the
        /// tangent-fit reconstruction CS1 used (see type remarks) — adequate for foundation-phase
        /// geometry, revisit if a later phase needs an exact parallel-curve offset.
        /// </remarks>
        public BezierTrajectory Shift(float start, float end) {
            var curve = Trajectory;

            float3 OffsetPoint(float3 point, float t) {
                var normal = MathUtils.Tangent(curve, t).Turn90(true);
                return point + normal * math.lerp(start, end, t);
            }

            var shifted = new Bezier4x3(
                OffsetPoint(curve.a, 0f),
                OffsetPoint(curve.b, 1f / 3f),
                OffsetPoint(curve.c, 2f / 3f),
                OffsetPoint(curve.d, 1f));

            return new BezierTrajectory(shifted);
        }
        ITrajectory ITrajectory.Shift(float start, float end) => Shift(start, end);

        public BezierTrajectory Elevate(float height) {
            var up = new float3(0f, height, 0f);
            return new BezierTrajectory(Trajectory + up);
        }

        /// <remarks>Approximate: see <see cref="Shift"/> remarks — same control-point interpolation, applied to height instead of a sideways normal.</remarks>
        public BezierTrajectory Elevate(float start, float end) {
            var elevated = new Bezier4x3(
                Trajectory.a + new float3(0f, start, 0f),
                Trajectory.b + new float3(0f, math.lerp(start, end, 1f / 3f), 0f),
                Trajectory.c + new float3(0f, math.lerp(start, end, 2f / 3f), 0f),
                Trajectory.d + new float3(0f, end, 0f));

            return new BezierTrajectory(elevated);
        }
        ITrajectory ITrajectory.Elevate(float height) => Elevate(height);
        ITrajectory ITrajectory.Elevate(float start, float end) => Elevate(start, end);

        public float3 GetClosestPosition(float3 point, out float t) {
            MathUtils.Distance(Trajectory, point, out t);
            return Position(t);
        }

        public void GetClosestPositionAndDirection(float3 point, out float3 position, out float3 direction, out float t) {
            MathUtils.Distance(Trajectory, point, out t);
            position = Position(t);
            direction = math.normalizesafe(Tangent(t));
        }

        public override string ToString() => $"Bezier: {StartPosition} - {EndPosition}";

        public static implicit operator Bezier4x3(BezierTrajectory trajectory) => trajectory.Trajectory;
        public static explicit operator BezierTrajectory(Bezier4x3 bezier) => new BezierTrajectory(bezier);

        public bool Equals(BezierTrajectory other) =>
            math.all(Trajectory.a == other.Trajectory.a) && math.all(Trajectory.b == other.Trajectory.b) &&
            math.all(Trajectory.c == other.Trajectory.c) && math.all(Trajectory.d == other.Trajectory.d);
        public override bool Equals(object obj) => obj is BezierTrajectory other && Equals(other);
        public static bool operator ==(BezierTrajectory first, BezierTrajectory second) => first.Equals(second);
        public static bool operator !=(BezierTrajectory first, BezierTrajectory second) => !first.Equals(second);
        public override int GetHashCode() => Trajectory.a.GetHashCode() ^ Trajectory.b.GetHashCode() ^ Trajectory.c.GetHashCode() ^ Trajectory.d.GetHashCode();
    }
}
