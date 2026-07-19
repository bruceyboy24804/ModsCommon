namespace ModsCommon.PropertyValue {
    #region Using Statements

    using System;

    #endregion

    /// <summary>
    /// Thin <see cref="PropertyValue{T}"/> subclass for enum-backed fields (e.g. a line style's
    /// dash pattern). <see cref="PropertyValue{T}"/> already handles enums correctly via
    /// <c>EqualityComparer&lt;T&gt;.Default</c>; this exists mainly for symmetry with CS1 ModsCommon's
    /// naming and to make enum properties self-documenting at the call site.
    /// </summary>
    public class PropertyEnumValue<T> : PropertyValue<T> where T : struct, Enum {
        public PropertyEnumValue(Action onChanged, T value = default) : base(onChanged, value) { }
        public PropertyEnumValue(string label, Action onChanged, T value = default) : base(label, onChanged, value) { }
    }
}
