using System.Collections.Generic;

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

        private const string BindingPrefix = "BINDING:";
        private const string TriggerPrefix = "TRIGGER:";

        private readonly List<Action> _updateCallbacks = new();

        protected virtual bool DefaultAutoUpdate => true;

        private string GetBindingKey(string key)
        {
            return $"{BindingPrefix}{key}";
        }

        private string GetTriggerKey(string key)
        {
            return $"{TriggerPrefix}{key}";
        }
        protected override void OnCreate() {
            base.OnCreate();
            m_Log = new PrefixedLogger(GetType().Name);
            m_Log.Debug("OnCreate()");
        }
        protected override void OnUpdate()
        {
            foreach (var action in _updateCallbacks)
            {
                action();
            }

            base.OnUpdate();
        }

        /// <summary>
        /// Creates a one-way value binding (C# -&gt; UI). When <paramref name="autoUpdate"/> is enabled
        /// (defaults to <see cref="DefaultAutoUpdate"/>) the binding is refreshed every frame in OnUpdate.
        /// </summary>
        public ValueBindingHelper<T> CreateBinding<T>(string key, T initialValue, bool? autoUpdate = null)
        {
            var bindingKey = GetBindingKey(key);
            var shouldAutoUpdate = autoUpdate ?? DefaultAutoUpdate;
            var helper = new ValueBindingHelper<T>(new(ModId, bindingKey, initialValue, new GenericUIWriter<T?>()));

            AddBinding(helper.Binding);

            if (shouldAutoUpdate)
            {
                _updateCallbacks.Add(helper.ForceUpdate);
            }

            return helper;
        }

        /// <summary>
        /// Creates a two-way binding: a value binding (C# -&gt; UI) plus a trigger (UI -&gt; C#) keyed by
        /// <paramref name="setterKey"/>. Pass <paramref name="updateCallBack"/> to react to UI-driven changes.
        /// Use the single-key overload when the binding and trigger should share one key.
        /// </summary>
        public ValueBindingHelper<T> CreateGenericBinding<T>(string key, string setterKey, T initialValue, Action<T> updateCallBack = null, bool? autoUpdate = null)
        {
            var bindingKey = GetBindingKey(key);
            var triggerKey = GetTriggerKey(setterKey);
            var shouldAutoUpdate = autoUpdate ?? DefaultAutoUpdate;
            var helper = new ValueBindingHelper<T>(new(ModId, bindingKey, initialValue, new GenericUIWriter<T?>()), updateCallBack);
            var trigger = new TriggerBinding<T>(ModId, triggerKey, helper.UpdateCallback, GenericUIReader<T>.Create());

            AddBinding(helper.Binding);
            AddBinding(trigger);

            if (shouldAutoUpdate)
            {
                _updateCallbacks.Add(helper.ForceUpdate);
            }

            return helper;
        }

        /// <summary>
        /// Creates a two-way binding that shares a single <paramref name="key"/> for both the value binding
        /// and the trigger; the <c>BINDING:</c> / <c>TRIGGER:</c> prefixes keep the two keys distinct.
        /// </summary>
        public ValueBindingHelper<T> CreateGenericBinding<T>(string key, T initialValue, Action<T> updateCallBack, bool? autoUpdate = null)
        {
            var bindingKey = GetBindingKey(key);
            var triggerKey = GetTriggerKey(key);
            var shouldAutoUpdate = autoUpdate ?? DefaultAutoUpdate;
            var helper = new ValueBindingHelper<T>(new(ModId, bindingKey, initialValue, new GenericUIWriter<T?>()), updateCallBack);
            var trigger = new TriggerBinding<T>(ModId, triggerKey, helper.UpdateCallback, GenericUIReader<T>.Create());

            AddBinding(helper.Binding);
            AddBinding(trigger);

            if (shouldAutoUpdate)
            {
                _updateCallbacks.Add(helper.ForceUpdate);
            }

            return helper;
        }

        /// <summary>
        /// Creates a two-way binding with a custom <paramref name="customWriter"/> / <paramref name="customReader"/>,
        /// using <paramref name="key"/> as both the binding key and the trigger (setter) key.
        /// </summary>
        public ValueBindingHelper<T> CreateCustomBinding<T>(string key, T initialValue, IWriter<T> customWriter, IReader<T> customReader, Action<T> updateCallBack = null, bool? autoUpdate = null)
        {
            var bindingKey = GetBindingKey(key);
            var triggerKey = GetTriggerKey(key);
            var shouldAutoUpdate = autoUpdate ?? DefaultAutoUpdate;
            var helper = new ValueBindingHelper<T>(new(ModId, bindingKey, initialValue, customWriter), updateCallBack);
            var trigger = new TriggerBinding<T>(ModId, triggerKey, helper.UpdateCallback, customReader);

            AddBinding(helper.Binding);
            AddBinding(trigger);

            if (shouldAutoUpdate)
            {
                _updateCallbacks.Add(helper.ForceUpdate);
            }

            return helper;
        }

        /// <summary>
        /// Creates a two-way binding with a custom <paramref name="customWriter"/> / <paramref name="customReader"/>,
        /// using a separate <paramref name="setterKey"/> for the trigger so the binding and trigger keys can differ.
        /// </summary>
        public ValueBindingHelper<T> CreateCustomBinding<T>(string key, string setterKey, T initialValue, IWriter<T> customWriter, IReader<T> customReader, Action<T> updateCallBack = null, bool? autoUpdate = null)
        {
            var bindingKey = GetBindingKey(key);
            var triggerKey = GetTriggerKey(setterKey);
            var shouldAutoUpdate = autoUpdate ?? DefaultAutoUpdate;
            var helper = new ValueBindingHelper<T>(new(ModId, bindingKey, initialValue, customWriter), updateCallBack);
            var trigger = new TriggerBinding<T>(ModId, triggerKey, helper.UpdateCallback, customReader);

            AddBinding(helper.Binding);
            AddBinding(trigger);

            if (shouldAutoUpdate)
            {
                _updateCallbacks.Add(helper.ForceUpdate);
            }

            return helper;
        }

        /// <summary>
        /// Creates a getter-driven value binding (C# -&gt; UI). When <paramref name="autoUpdate"/> is true
        /// (the default) the binding is re-evaluated every frame via AddUpdateBinding.
        /// </summary>
        public GetterValueBinding<T> CreateBinding<T>(string key, Func<T> getterFunc, bool autoUpdate = true)
        {
            var bindingKey = GetBindingKey(key);
            var binding = new GetterValueBinding<T>(ModId, bindingKey, getterFunc, new GenericUIWriter<T>());

            if (autoUpdate)
            {
                AddUpdateBinding(binding);
            }
            else
            {
                AddBinding(binding);
            }

            return binding;
        }

        public TriggerBinding CreateTrigger(string key, Action action)
        {
            var triggerKey = GetTriggerKey(key);
            var binding = new TriggerBinding(ModId, triggerKey, action);

            AddBinding(binding);

            return binding;
        }

        public TriggerBinding<T1> CreateTrigger<T1>(string key, Action<T1> action)
        {
            var triggerKey = GetTriggerKey(key);
            var binding = new TriggerBinding<T1>(ModId, triggerKey, action, GenericUIReader<T1>.Create());

            AddBinding(binding);

            return binding;
        }

        public TriggerBinding<T1, T2> CreateTrigger<T1, T2>(string key, Action<T1, T2> action)
        {
            var triggerKey = GetTriggerKey(key);
            var binding = new TriggerBinding<T1, T2>(ModId, triggerKey, action, GenericUIReader<T1>.Create(), GenericUIReader<T2>.Create());

            AddBinding(binding);

            return binding;
        }

        public TriggerBinding<T1, T2, T3> CreateTrigger<T1, T2, T3>(string key, Action<T1, T2, T3> action)
        {
            var triggerKey = GetTriggerKey(key);
            var binding = new TriggerBinding<T1, T2, T3>(ModId, triggerKey, action, GenericUIReader<T1>.Create(), GenericUIReader<T2>.Create(), GenericUIReader<T3>.Create());

            AddBinding(binding);

            return binding;
        }

        public TriggerBinding<T1, T2, T3, T4> CreateTrigger<T1, T2, T3, T4>(string key, Action<T1, T2, T3, T4> action)
        {
            var triggerKey = GetTriggerKey(key);
            var binding = new TriggerBinding<T1, T2, T3, T4>(ModId, triggerKey, action, GenericUIReader<T1>.Create(), GenericUIReader<T2>.Create(), GenericUIReader<T3>.Create(), GenericUIReader<T4>.Create());

            AddBinding(binding);

            return binding;
        }
    }
}
