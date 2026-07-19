namespace ModsCommon.PropertyValue {
    #region Using Statements

    using System;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// <see cref="PropertyValue{T}"/> specializations for <see cref="Unity.Mathematics"/> float vectors —
    /// deliberately targeting <c>float2</c>/<c>float3</c>/<c>float4</c> rather than CS1 ModsCommon's
    /// <c>UnityEngine.Vector2/3/4</c>-based equivalents, to match the math convention already established
    /// by <see cref="ModsCommon.Rendering.CustomOverlayRenderSystem"/> and <see cref="ModsCommon.Trajectory.ITrajectory"/>.
    /// </summary>
    public class PropertyFloat2Value : PropertyValue<float2> {
        public PropertyFloat2Value(Action onChanged, float2 value = default) : base(onChanged, value) { }
        public PropertyFloat2Value(string label, Action onChanged, float2 value = default) : base(label, onChanged, value) { }
        protected override bool AreEqual(float2 a, float2 b) => math.all(a == b);
    }

    public class PropertyFloat3Value : PropertyValue<float3> {
        public PropertyFloat3Value(Action onChanged, float3 value = default) : base(onChanged, value) { }
        public PropertyFloat3Value(string label, Action onChanged, float3 value = default) : base(label, onChanged, value) { }
        protected override bool AreEqual(float3 a, float3 b) => math.all(a == b);
    }

    public class PropertyFloat4Value : PropertyValue<float4> {
        public PropertyFloat4Value(Action onChanged, float4 value = default) : base(onChanged, value) { }
        public PropertyFloat4Value(string label, Action onChanged, float4 value = default) : base(label, onChanged, value) { }
        protected override bool AreEqual(float4 a, float4 b) => math.all(a == b);
    }
}
