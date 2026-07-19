namespace ModsCommon.PropertyValue {
    #region Using Statements

    using System;
    using UnityEngine;

    #endregion

    /// <summary>
    /// A <see cref="PropertyValue{T}"/> of <see cref="Color32"/> that compares RGBA bytes explicitly for
    /// change detection, rather than relying on <see cref="Color32"/>'s own equality behavior.
    /// </summary>
    public class PropertyColorValue : PropertyValue<Color32> {
        public PropertyColorValue(Action onChanged, Color32 value = default) : base(onChanged, value) { }
        public PropertyColorValue(string label, Action onChanged, Color32 value = default) : base(label, onChanged, value) { }

        protected override bool AreEqual(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
