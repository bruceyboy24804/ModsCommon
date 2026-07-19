namespace ModsCommon.PropertyValue {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    #endregion

    /// <summary>
    /// A dirty-checking wrapper around a single value: setting <see cref="Value"/> invokes the
    /// change-notification callback only when the new value actually differs from the current one.
    /// The eventual style/marking data model composes these for every editable field (width, offset,
    /// color, ...), the same role CS1 ModsCommon's <c>PropertyValue&lt;T&gt;</c> family played there.
    /// </summary>
    /// <remarks>
    /// Collapses CS1's three-way split (<c>PropertyValue&lt;T&gt;</c> abstract + <c>PropertyClassValue&lt;T&gt;
    /// where T : class</c> + <c>PropertyStructValue&lt;T&gt; where T : struct</c>, each hand-writing its own
    /// equality) into this single generic type using <see cref="EqualityComparer{T}.Default"/>, which already
    /// dispatches correctly to <see cref="IEquatable{T}"/>/<c>object.Equals</c> for both reference and value
    /// types — the class/struct split served no purpose that generic equality doesn't already cover.
    /// Deliberately carries no <c>ToXml</c>/<c>FromXml</c>: serialization is a concern for whatever concrete
    /// domain class wraps this, not this primitive itself (see Phase 1 plan's serialization decision).
    /// </remarks>
    public abstract class BasePropertyValue<T> {
        private readonly Action m_OnChanged;

        /// <summary>Optional display label for UI generation in a later phase; not used by this type itself.</summary>
        public string Label { get; }

        public abstract T Value { get; set; }

        protected BasePropertyValue(Action onChanged) : this(string.Empty, onChanged) { }

        protected BasePropertyValue(string label, Action onChanged) {
            Label = label;
            m_OnChanged = onChanged;
        }

        protected void OnValueChanged() => m_OnChanged?.Invoke();

        public override string ToString() => Value?.ToString() ?? string.Empty;

        public static implicit operator T(BasePropertyValue<T> property) => property.Value;
    }

    /// <summary>Concrete <see cref="BasePropertyValue{T}"/> for any type; see the class remarks for why this replaces CS1's class/struct split.</summary>
    public class PropertyValue<T> : BasePropertyValue<T> {
        private T m_Value;

        public override T Value {
            get => m_Value;
            set {
                if (!AreEqual(value, m_Value)) {
                    m_Value = value;
                    OnValueChanged();
                }
            }
        }

        public PropertyValue(Action onChanged, T value = default) : this(string.Empty, onChanged, value) { }

        public PropertyValue(string label, Action onChanged, T value = default) : base(label, onChanged) {
            m_Value = value;
        }

        /// <summary>
        /// Change-detection comparison, defaulting to <see cref="EqualityComparer{T}.Default"/>. Override
        /// when a type's built-in equality isn't the right notion of "changed" for this property (e.g.
        /// <see cref="PropertyColorValue"/> comparing RGBA bytes explicitly).
        /// </summary>
        protected virtual bool AreEqual(T a, T b) => EqualityComparer<T>.Default.Equals(a, b);
    }
}
