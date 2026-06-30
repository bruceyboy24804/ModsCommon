namespace ModsCommon.Extensions {
    using System;
    using Colossal.UI.Binding;

    public class ValueBindingHelper<T> {
        private readonly Action<T>? _updateCallBack;
        private T? _valueToUpdate;
        private bool _dirty;

        public ValueBinding<T?> Binding { get; }

        public T? Value
        {
            get => _dirty ? _valueToUpdate : Binding.value;
            set
            {
                _dirty = true;
                _valueToUpdate = value;
            }
        }

        public ValueBindingHelper(ValueBinding<T?> binding, Action<T>? updateCallBack = null)
        {
            Binding = binding;
            _updateCallBack = updateCallBack;
        }

        public void ForceUpdate()
        {
            if (_dirty)
            {
                Binding.Update(_valueToUpdate);

                _dirty = false;
            }
        }

        public void UpdateCallback(T value)
        {
            Value = value;

            _updateCallBack?.Invoke(value);
        }

        public static implicit operator T?(ValueBindingHelper<T> helper)
        {
            return helper.Value;
        }
    }
}
