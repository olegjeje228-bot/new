using System;
using System.Collections.Generic;
using EventHUD.Enums;

namespace EventHUD.Rpm
{
    public class RpModuleManager
    {
        public static RpModuleManager Instance { get; } = new RpModuleManager();

        private readonly Dictionary<RpModuleType, bool> _states = new();

        private RpModuleManager()
        {
            foreach (RpModuleType type in Enum.GetValues(typeof(RpModuleType)))
                _states[type] = false;
        }

        public bool IsEnabled(RpModuleType type) =>
            _states.TryGetValue(type, out var v) && v;

        public void SetEnabled(RpModuleType type, bool enabled) =>
            _states[type] = enabled;

        public void SetAll(bool enabled)
        {
            foreach (var key in new List<RpModuleType>(_states.Keys))
                _states[key] = enabled;
        }

        /// <summary>
        /// Вызывается при ev prepare/start.
        /// NRP — все модули OFF. Любой RP — все модули ON.
        /// </summary>
        public void OnEventRpChanged(RPType rpType) =>
            SetAll(rpType != RPType.NONRP);

        /// <summary>
        /// Вызывается при ev stop.
        /// Модули остаются в текущем состоянии.
        /// </summary>
        public void OnEventStopped(RPType lastRpType) { }
    }
}
 