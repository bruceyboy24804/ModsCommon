namespace ModsCommon.Systems {
    #region Using Statements

    using System;

    using Colossal.UI.Binding;

    using Game.UI;

    using ModsCommon.Extensions;
    using ModsCommon.Utils;

    #endregion

    /// <summary>
    /// Base <see cref="UISystemBase"/> for C# &lt;-&gt; UI bindings. Mirrors
    /// <see cref="CommonGameSystemBase"/>: wires up a <see cref="PrefixedLogger"/> (<c>m_Log</c>) in
    /// OnCreate, prefixed with the derived system's type name. Derived systems should call
    /// <c>base.OnCreate()</c> when overriding. Also provides CreateBinding / CreateTrigger helpers.
    /// </summary>
    public abstract partial class CommonUISystemBase : UISystemBase {
        protected internal PrefixedLogger m_Log;

        /// <summary>
        /// The mod Id used as the binding group. Subclasses provide their mod's Id
        /// (e.g. <c>=&gt; MyMod.Instance.Id</c>).
        /// </summary>
        protected abstract string ModId { get; }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_Log = new PrefixedLogger(GetType().Name);
            m_Log.Debug("OnCreate()");
        }

        public ValueBindingHelper<T> CreateBinding<T>(string key, T initialValue) {
            var helper = new ValueBindingHelper<T>(new(ModId, $"BINDING:{key}", initialValue, new GenericUIWriter<T>()));

            AddBinding(helper.Binding);

            return helper;
        }

        public ValueBindingHelper<T> CreateBinding<T>(string key, T initialValue, Action<T> updateCallBack = null) {
            var helper = new ValueBindingHelper<T>(new(ModId, $"BINDING:{key}", initialValue, new GenericUIWriter<T>()), updateCallBack);
            var trigger = new TriggerBinding<T>(ModId, $"TRIGGER:{key}", helper.UpdateCallback, new GenericUIReader<T>());

            AddBinding(helper.Binding);
            AddBinding(trigger);

            return helper;
        }

        public ValueBindingHelper<T> CreateBinding<T>(string key, T initialValue, Action<T> updateCallBack = null, IWriter<T> customWriter = null, IReader<T> customReader = null) {
            var helper = new ValueBindingHelper<T>(
                new(ModId, $"BINDING:{key}", initialValue, customWriter),
                updateCallBack);
            var trigger = new TriggerBinding<T>(ModId, $"TRIGGER:{key}", helper.UpdateCallback, customReader);

            AddBinding(helper.Binding);
            AddBinding(trigger);

            return helper;
        }

        public ValueBindingHelper<T> CreateBinding<T>(string key, string setterKey, T initialValue, Action<T> updateCallBack = null) {
            var helper = new ValueBindingHelper<T>(new(ModId, $"BINDING:{key}", initialValue, new GenericUIWriter<T>()), updateCallBack);
            var trigger = new TriggerBinding<T>(ModId, $"TRIGGER:{setterKey}", helper.UpdateCallback, new GenericUIReader<T>());

            AddBinding(helper.Binding);
            AddBinding(trigger);

            return helper;
        }

        public GetterValueBinding<T> CreateBinding<T>(string key, Func<T> getterFunc) {
            var binding = new GetterValueBinding<T>(ModId, key, getterFunc, new GenericUIWriter<T>());

            AddBinding(binding);

            return binding;
        }

        public TriggerBinding CreateTrigger(string key, Action action) {
            var binding = new TriggerBinding(ModId, $"TRIGGER:{key}", action);

            AddBinding(binding);

            return binding;
        }

        public TriggerBinding<T1> CreateTrigger<T1>(string key, Action<T1> action) {
            var binding = new TriggerBinding<T1>(ModId, $"TRIGGER:{key}", action, new GenericUIReader<T1>());

            AddBinding(binding);

            return binding;
        }

        public TriggerBinding<T1, T2> CreateTrigger<T1, T2>(string key, Action<T1, T2> action) {
            var binding = new TriggerBinding<T1, T2>(ModId, $"TRIGGER:{key}", action, new GenericUIReader<T1>(), new GenericUIReader<T2>());

            AddBinding(binding);

            return binding;
        }

        public TriggerBinding<T1, T2, T3> CreateTrigger<T1, T2, T3>(string key, Action<T1, T2, T3> action) {
            var binding = new TriggerBinding<T1, T2, T3>(ModId, $"TRIGGER:{key}", action, new GenericUIReader<T1>(), new GenericUIReader<T2>(), new GenericUIReader<T3>());

            AddBinding(binding);

            return binding;
        }

        public TriggerBinding<T1, T2, T3, T4> CreateTrigger<T1, T2, T3, T4>(string key, Action<T1, T2, T3, T4> action) {
            var binding = new TriggerBinding<T1, T2, T3, T4>(ModId, $"TRIGGER:{key}", action, new GenericUIReader<T1>(), new GenericUIReader<T2>(), new GenericUIReader<T3>(), new GenericUIReader<T4>());

            AddBinding(binding);

            return binding;
        }
    }
}
