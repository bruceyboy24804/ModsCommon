namespace ModsCommon.Trajectory {
    #region Using Statements

    using System;
    using Colossal.Mathematics;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// An <see cref="ITrajectory"/> backed by a straight <see cref="Line3.Segment"/> — the exact type
    /// <see cref="ModsCommon.Rendering.CustomOverlayRenderSystem.Buffer.DrawLine(UnityEngine.Color, Line3.Segment, float, bool)"/>
    /// already accepts.
    /// </summary>
    public readonly struct StraightTrajectory : ITrajectory, IEquatable<StraightTrajectory> {
        public TrajectoryType TrajectoryType => TrajectoryType.Line;
        public Line3.Segment Trajectory { get; }
        public bool StartLimited { get; }
        public bool EndLimited { get; }
        public bool IsSection => StartLimited && EndLimited;

        public float Length { get; }
        public float Magnitude => Length;
        public float DeltaAngle => 0f;
        public float3 Direction { get; }
        public float3 StartDirection => Direction;
        public float3 EndDirection => -Direction;
        public float3 StartPosition => Trajectory.a;
        public float3 EndPosition => Trajectory.b;
        public bool IsZero => math.all(Trajectory.a == Trajectory.b);

        private StraightTrajectory(Line3.Segment trajectory, bool startLimited, bool endLimited, float length, float3 direction) {
            Trajectory = trajectory;
            StartLimited = startLimited;
            EndLimited = endLimited;
            Length = length;
            Direction = direction;
        }

        public StraightTrajectory(Line3.Segment trajectory, bool startLimited, bool endLimited) {
            Trajectory = trajectory;
            StartLimited = startLimited;
            EndLimited = endLimited;

            var delta = trajectory.b - trajectory.a;
            Length = math.length(delta);
            Direction = math.normalizesafe(delta);
        }

        public StraightTrajectory(Line3.Segment trajectory, bool isSection = true) : this(trajectory, isSection, isSection) { }

        public StraightTrajectory(float3 start, float3 end, bool startLimited, bool endLimited)
            : this(new Line3.Segment(start, end), startLimited, endLimited) { }

        public StraightTrajectory(float3 start, float3 end, bool isSection = true)
            : this(new Line3.Segment(start, end), isSection, isSection) { }

        public StraightTrajectory(ITrajectory trajectory, bool startLimited, bool endLimited)
            : this(new Line3.Segment(trajectory.StartPosition, trajectory.EndPosition), startLimited, endLimited) { }

        public StraightTrajectory(ITrajectory trajectory, bool isSection = true)
            : this(new Line3.Segment(trajectory.StartPosition, trajectory.EndPosition), isSection, isSection) { }

        public StraightTrajectory Cut(float t0, float t1) => new StraightTrajectory(Position(t0), Position(t1));
        ITrajectory ITrajectory.Cut(float t0, float t1) => Cut(t0, t1);
        public StraightTrajectory Cut(float t0, float t1, bool isSection) => new StraightTrajectory(Position(t0), Position(t1), isSection);

        public void Divide(out ITrajectory trajectory1, out ITrajectory trajectory2) {
            var middle = (Trajectory.a + Trajectory.b) * 0.5f;
            trajectory1 = new StraightTrajectory(Trajectory.a, middle);
            trajectory2 = new StraightTrajectory(middle, Trajectory.b);
        }

        public float3 Position(float t) => math.lerp(Trajectory.a, Trajectory.b, t);
        public float3 Tangent(float t) => Direction;
        public float Travel(float distance) => Length > 0f ? distance / Length : 0f;
        public float Travel(float start, float distance) => start + Travel(distance);
        public float Distance(float from = 0f, float to = 1f) => Length * (to - from);

        public StraightTrajectory Invert() => new StraightTrajectory(new Line3.Segment(Trajectory.b, Trajectory.a), EndLimited, StartLimited, Length, -Direction);
        ITrajectory ITrajectory.Invert() => Invert();

        public StraightTrajectory Shift(float start, float end) {
            var startNormal = StartDirection.Turn90(true);
            var endNormal = EndDirection.Turn90(false);
            return new StraightTrajectory(StartPosition + startNormal * start, EndPosition + endNormal * end);
        }
        ITrajectory ITrajectory.Shift(float start, float end) => Shift(start, end);

        public StraightTrajectory Elevate(float height) {
            var up = new float3(0f, height, 0f);
            return new StraightTrajectory(StartPosition + up, EndPosition + up, StartLimited, EndLimited);
        }

        public StraightTrajectory Elevate(float start, float end) {
            return new StraightTrajectory(StartPosition + new float3(0f, start, 0f), EndPosition + new float3(0f, end, 0f), StartLimited, EndLimited);
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
            direction = Direction;
        }

        public override string ToString() => $"Straight: {StartPosition} - {EndPosition}";

        public static implicit operator Line3.Segment(StraightTrajectory trajectory) => trajectory.Trajectory;
        public static explicit operator StraightTrajectory(Line3.Segment line) => new StraightTrajectory(line);

        public bool Equals(StraightTrajectory other) => math.all(Trajectory.a == other.Trajectory.a) && math.all(Trajectory.b == other.Trajectory.b);
        public override bool Equals(object obj) => obj is StraightTrajectory other && Equals(other);
        public static bool operator ==(StraightTrajectory first, StraightTrajectory second) => first.Equals(second);
        public static bool operator !=(StraightTrajectory first, StraightTrajectory second) => !first.Equals(second);
        public override int GetHashCode() => Trajectory.a.GetHashCode() ^ Trajectory.b.GetHashCode();
    }
}
