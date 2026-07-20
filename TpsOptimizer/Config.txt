namespace TpsOptimizer
{
    using System.ComponentModel;
    using Exiled.API.Interfaces;

    /// <summary>
    /// Configuration for <see cref="Plugin"/>. Every value is exposed to the EXILED YAML config
    /// so server owners can tune the optimizer without recompiling.
    /// </summary>
    public sealed class Config : IConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether the plugin is enabled.
        /// </summary>
        [Description("Whether the plugin is enabled.")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether EXILED debug logging is enabled.
        /// Cleanup logs (see <see cref="LogCleanups"/>) are written via Log.Debug, so this must be
        /// enabled for them to appear.
        /// </summary>
        [Description("Whether debug logging is enabled.")]
        public bool Debug { get; set; } = false;

        // ===================== Ragdolls =====================

        /// <summary>
        /// Gets or sets the maximum number of ragdolls allowed on the map before the oldest are trimmed.
        /// </summary>
        [Description("Maximum number of ragdolls kept on the map; the oldest beyond this are removed.")]
        public int MaxRagdolls { get; set; } = 15;

        /// <summary>
        /// Gets or sets the age, in seconds, after which a ragdoll becomes eligible for cleanup.
        /// </summary>
        [Description("Age in seconds after which a ragdoll is removed.")]
        public float RagdollLifetime { get; set; } = 120f;

        /// <summary>
        /// Gets or sets the minimum age, in seconds, a ragdoll must reach before it can be touched.
        /// Younger ragdolls are always kept (e.g. so SCP-049 can still recall recent bodies).
        /// </summary>
        [Description("Ragdolls younger than this (seconds) are never removed (SCP-049 protection).")]
        public float RagdollSafeTime { get; set; } = 30f;

        /// <summary>
        /// Gets or sets how often, in seconds, the ragdoll cleanup pass runs.
        /// </summary>
        [Description("Ragdoll cleanup interval, in seconds.")]
        public float RagdollCleanupInterval { get; set; } = 90f;

        // ===================== Pickups =====================

        /// <summary>
        /// Gets or sets the maximum number of floor pickups allowed before the oldest are trimmed.
        /// </summary>
        [Description("Maximum number of floor items kept; the oldest beyond this are removed.")]
        public int MaxPickups { get; set; } = 80;

        /// <summary>
        /// Gets or sets the age, in seconds, after which a pickup becomes eligible for cleanup.
        /// </summary>
        [Description("Age in seconds after which a floor item may be removed.")]
        public float PickupLifetime { get; set; } = 300f;

        /// <summary>
        /// Gets or sets how often, in seconds, the pickup cleanup pass runs.
        /// </summary>
        [Description("Pickup cleanup interval, in seconds.")]
        public float PickupCleanupInterval { get; set; } = 120f;

        /// <summary>
        /// Gets or sets the radius, in meters, used to detect a nearby player who would protect a pickup.
        /// </summary>
        [Description("If a player is within this radius (meters) of an old item, it is kept.")]
        public float PickupSafeRadius { get; set; } = 15f;

        /// <summary>
        /// Gets or sets the age, in seconds, above which the nearby-player radius check is applied
        /// before removing a pickup.
        /// </summary>
        [Description("Age in seconds above which the nearby-player radius check applies.")]
        public float PickupAgeBeforeRadiusCheck { get; set; } = 120f;

        // ===================== GC =====================

        /// <summary>
        /// Gets or sets how often, in seconds, the adaptive garbage collector evaluates TPS.
        /// </summary>
        [Description("Adaptive GC check interval, in seconds.")]
        public float GcCheckInterval { get; set; } = 60f;

        /// <summary>
        /// Gets or sets the TPS threshold below which a light (gen 0) collection is triggered.
        /// </summary>
        [Description("Below this TPS a light gen-0 GC is triggered.")]
        public float GcTpsThresholdSoft { get; set; } = 50f;

        /// <summary>
        /// Gets or sets the TPS threshold below which a gen 1 collection and low-latency mode are triggered.
        /// </summary>
        [Description("Below this TPS a gen-1 GC and SustainedLowLatency mode are triggered.")]
        public float GcTpsThresholdMedium { get; set; } = 40f;

        /// <summary>
        /// Gets or sets the TPS threshold below which a full, forced, blocking collection is triggered.
        /// </summary>
        [Description("Below this TPS a full forced blocking GC is triggered.")]
        public float GcTpsThresholdHard { get; set; } = 30f;

        // ===================== Grenades =====================

        /// <summary>
        /// Gets or sets the maximum number of live grenades (HE/flash) allowed on the map at once.
        /// </summary>
        [Description("Maximum number of active grenades (HE/Flash) on the map at once.")]
        public int MaxActiveGrenades { get; set; } = 15;

        // ===================== Monitoring =====================

        /// <summary>
        /// Gets or sets a value indicating whether cleanup actions are logged (via Log.Debug).
        /// </summary>
        [Description("Whether cleanup actions are logged (requires Debug=true to be visible).")]
        public bool LogCleanups { get; set; } = true;
    }
}
