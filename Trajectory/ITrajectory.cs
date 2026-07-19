namespace ModsCommon.Trajectory {
    #region Using Statements

    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// Identifies the concrete shape backing an <see cref="ITrajectory"/> instance.
    /// </summary>
    public enum TrajectoryType {
        Line,
        Bezier,
        Combined
    }

    /// <summary>
    /// Engine-agnostic curve/line abstraction built on top of <see cref="Colossal.Mathematics"/>
    /// primitives (<see cref="Colossal.Mathematics.Bezier4x3"/>, <see cref="Colossal.Mathematics.Line3.Segment"/>).
    /// This is the shared shape every concrete marking-line/filler/crosswalk edge is expressed in terms of.
    /// </summary>
    /// <remarks>
    /// Deliberately excludes two things CS1 ModsCommon's <c>ITrajectory</c> carried: rendering
    /// (<c>Render(OverlayData)</c>) and mouse-ray hit-testing (<c>GetHitPosition</c>). Rendering should go
    /// straight from the underlying <see cref="Colossal.Mathematics.Bezier4x3"/>/<see cref="Colossal.Mathematics.Line3.Segment"/>
    /// into <see cref="ModsCommon.Rendering.CustomOverlayRenderSystem.Buffer"/>, and ray-hit-testing is a
    /// tool-input concern that belongs with the (not-yet-designed) CS2 raycast/tool pipeline.
    /// </remarks>
    public interface ITrajectory {
        TrajectoryType TrajectoryType { get; }

        /// <summary>Arc length of the trajectory, measured in meters.</summary>
        float Length { get; }

        /// <summary>Straight-line distance between <see cref="StartPosition"/> and <see cref="EndPosition"/>.</summary>
        float Magnitude { get; }

        /// <summary>Angle, in degrees, between <see cref="StartDirection"/> and the inverse of <see cref="EndDirection"/>.</summary>
        float DeltaAngle { get; }

        /// <summary>Normalized direction from <see cref="StartPosition"/> straight to <see cref="EndPosition"/>.</summary>
        float3 Direction { get; }

        float3 StartDirection { get; }
        float3 EndDirection { get; }
        float3 StartPosition { get; }
        float3 EndPosition { get; }

        /// <summary>True when the trajectory has (near) zero extent (start and end collapse to the same point).</summary>
        bool IsZero { get; }

        /// <summary>World-space position at parameter <paramref name="t"/> (0 = start, 1 = end).</summary>
        float3 Position(float t);

        /// <summary>Tangent direction at parameter <paramref name="t"/>.</summary>
        float3 Tangent(float t);

        /// <summary>Returns the portion of this trajectory between parameters <paramref name="t0"/> and <paramref name="t1"/>.</summary>
        ITrajectory Cut(float t0, float t1);

        /// <summary>Splits this trajectory into two halves at its midpoint.</summary>
        void Divide(out ITrajectory trajectory1, out ITrajectory trajectory2);

        /// <summary>Parameter reached after travelling <paramref name="distance"/> meters from the start.</summary>
        float Travel(float distance);

        /// <summary>Parameter reached after travelling <paramref name="distance"/> meters from parameter <paramref name="start"/>.</summary>
        float Travel(float start, float distance);

        /// <summary>Arc length between parameters <paramref name="from"/> and <paramref name="to"/>.</summary>
        float Distance(float from = 0f, float to = 1f);

        /// <summary>Returns this trajectory with start/end swapped.</summary>
        ITrajectory Invert();

        /// <summary>Returns this trajectory offset sideways by <paramref name="start"/> meters at the start and <paramref name="end"/> meters at the end.</summary>
        ITrajectory Shift(float start, float end);

        /// <summary>Returns this trajectory raised by <paramref name="height"/> meters (world-up).</summary>
        ITrajectory Elevate(float height);

        /// <summary>Returns this trajectory raised by <paramref name="start"/> meters at the start and <paramref name="end"/> meters at the end.</summary>
        ITrajectory Elevate(float start, float end);

        /// <summary>Closest point on the trajectory to <paramref name="point"/>, with the corresponding parameter.</summary>
        float3 GetClosestPosition(float3 point, out float t);

        /// <summary>Closest point on the trajectory to <paramref name="point"/>, plus tangent direction at that point.</summary>
        void GetClosestPositionAndDirection(float3 point, out float3 position, out float3 direction, out float t);
    }
}
