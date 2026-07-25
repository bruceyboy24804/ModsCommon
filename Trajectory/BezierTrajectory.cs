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
    /// <see cref="Shift"/> and <see cref="Elevate(float, float)"/> below use a control-point-offset
    /// approximation rather than an exact parallel-curve reconstruction (see their remarks) — that part of
    /// CS1's tangent-fit machinery is still deferred. <see cref="Fit"/>, however, is a real port: see its
    /// own remarks for why <c>Game.Net.NetUtils.FitCurve</c> (the vanilla-road curve fit this project
    /// initially reused for marking-point-to-marking-point lines, e.g. <c>IMT_RegularLine</c>) turned out
    /// to be the wrong tool for that job.
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

        /// <summary>
        /// Fraction of the (capped) combined tangent-intersection length a control point is allowed to
        /// travel — CS1 ModsCommon's <c>BezierTrajectory.curveT</c>. Ported verbatim (see <see cref="Fit"/>).
        /// </summary>
        public const float FitCurveT = 0.3f;

        /// <summary>
        /// Fits a cubic Bezier between two positions given each end's tangent direction — a faithful port
        /// of CS1 ModsCommon's <c>BezierTrajectory.GetMiddleDistance</c>/<c>GetMiddlePoints</c> (the real
        /// algorithm CS1's <c>MarkingRegularLine.CalculateTrajectory</c> actually uses for a node marking;
        /// it never calls anything like <c>NetUtils.FitCurve</c>).
        /// </summary>
        /// <remarks>
        /// Not the same algorithm as <c>Game.Net.NetUtils.FitCurve</c>, which this project initially reused
        /// here (<c>IMT_RegularLine</c>) on the assumption that "vanilla road curve fit" was a reasonable
        /// stand-in for "curve fit between two marking points." It isn't: <c>NetUtils.FitCurve</c> lets a
        /// control point travel up to the full chord distance along its tangent when the two tangents'
        /// alignment pushes that way, which is fine for real road corners (where both tangents are already
        /// road-aligned and point toward a sensible curve) but produces a sharply overshooting/kinked curve
        /// when the two points' tangents are only incidentally related (e.g. one along a road, one along a
        /// crosswalk) — confirmed by decompiling <c>Game.Net.NetUtils.FitCurve</c> and comparing against
        /// this method's real source. The fix is this method's <c>min(intersectionDistance, combinedLength *
        /// <see cref="FitCurveT"/>)</c> cap on each control point, which CS1 always applies and
        /// <c>NetUtils.FitCurve</c> never does. This is also the same root cause flagged (but never
        /// diagnosed) as the Phase 3 connector-curve "overshoot past the real corner" artifact, since
        /// <c>IMT_MarkingBuilder.BuildConnector</c> uses the identical <c>NetUtils.FitCurve</c> shape — not
        /// switched over yet, since that curve feeds the intersection contour (normal-point raycasting,
        /// filler contour) and deserves its own verification pass rather than riding along with this fix.
        ///
        /// Simplified from CS1 in one place: CS1 special-cases a <c>NetSegment.IsStraight</c> fast path
        /// (proportional placement at a smaller <c>straightT = 0.15f</c>, no tangent-intersection at all) for
        /// genuinely straight real road segments — CS2's <c>NetSegment</c> has no equivalent public check to
        /// call, and "is this a real straight road segment" isn't a meaningful question for two arbitrary
        /// marking points anyway. This always takes CS1's general (non-straight) branch, at
        /// <see cref="FitCurveT"/> for both ends — CS1's own fallback for "tangents nearly anti-parallel or
        /// don't intersect" (proportional placement, no cap) already reduces to the same shape for the
        /// genuinely-straight case.
        /// </remarks>
        public static BezierTrajectory Fit(float3 startPos, float3 startDir, float3 endPos, float3 endDir, float startT = FitCurveT, float endT = FitCurveT) {
            var normalizedStart = math.normalizesafe(startDir);
            var normalizedEnd = math.normalizesafe(endDir);
            var chord = math.distance(startPos, endPos);
            var dot = normalizedStart.x * normalizedEnd.x + normalizedStart.z * normalizedEnd.z;

            float startDis;
            float endDis;

            if (dot >= -0.999f && TryIntersectXZ(startPos, normalizedStart, endPos, normalizedEnd, out var u, out var v)) {
                u = math.clamp(u, chord * 0.1f, chord);
                v = math.clamp(v, chord * 0.1f, chord);
                var combined = u + v;
                startDis = math.min(u, combined * startT);
                endDis = math.min(v, combined * endT);
            } else {
                startDis = chord * startT;
                endDis = chord * endT;
            }

            var bezier = new Bezier4x3(startPos, startPos + normalizedStart * startDis, endPos + normalizedEnd * endDis, endPos);
            return new BezierTrajectory(bezier);
        }

        /// <summary>Solves <c>p1 + u*d1 == p2 + v*d2</c> for <c>u</c>/<c>v</c> in the XZ plane — CS1's <c>Line2.Intersect</c>, ported. <paramref name="d1"/>/<paramref name="d2"/> must be unit vectors so <c>u</c>/<c>v</c> come out as real-world distances.</summary>
        private static bool TryIntersectXZ(float3 p1, float3 d1, float3 p2, float3 d2, out float u, out float v) {
            var denom = d1.x * d2.z - d1.z * d2.x;
            if (math.abs(denom) < 1e-6f) {
                u = 0f;
                v = 0f;
                return false;
            }

            var diff = p2 - p1;
            u = (diff.x * d2.z - diff.z * d2.x) / denom;
            v = (diff.x * d1.z - diff.z * d1.x) / denom;
            return true;
        }

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
