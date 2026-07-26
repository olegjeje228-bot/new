namespace TpsOptimizer
{
    using System;
    using Exiled.API.Features;
    using TpsOptimizer.Optimizers;

    /// <summary>
    /// TPS optimization plugin for SCP: Secret Laboratory (EXILED 9.14.2).
    /// <para>
    /// This plugin ONLY optimizes server TPS. It never blocks commands, kicks, or bans, and is
    /// designed to stay out of the way of admins running and managing the server.
    /// </para>
    /// </summary>
    public sealed class Plugin : Plugin<Config>
    {
        /// <summary>
        /// Gets the active plugin instance, for access from commands and services.
        /// </summary>
        public static Plugin Instance { get; private set; }

        /// <inheritdoc/>
        public override string Name => "TpsOptimizer";

        /// <inheritdoc/>
        public override string Author => "TpsOptimizer";

        /// <inheritdoc/>
        public override Version Version { get; } = new Version(1, 0, 0);

        /// <inheritdoc/>
        public override Version RequiredExiledVersion { get; } = new Version(9, 14, 2);

        /// <summary>
        /// Gets or sets the total number of ragdolls removed this session.
        /// </summary>
        public int TotalRagdollsCleaned { get; set; }

        /// <summary>
        /// Gets or sets the total number of floor pickups removed this session.
        /// </summary>
        public int TotalPickupsCleaned { get; set; }

        /// <summary>
        /// Gets or sets the total number of garbage collections this plugin has triggered this session.
        /// </summary>
        public int TotalGcCalls { get; set; }

        /// <summary>
        /// Gets the UTC time at which the plugin was last enabled (used for the uptime readout).
        /// </summary>
        public DateTime StartTime { get; private set; }

        /// <summary>
        /// Gets the ragdoll auto-cleanup optimizer.
        /// </summary>
        public RagdollOptimizer RagdollOptimizer { get; private set; }

        /// <summary>
        /// Gets the floor-item auto-cleanup optimizer.
        /// </summary>
        public PickupOptimizer PickupOptimizer { get; private set; }

        /// <summary>
        /// Gets the adaptive garbage-collection optimizer.
        /// </summary>
        public GcOptimizer GcOptimizer { get; private set; }

        /// <summary>
        /// Gets the grenade-limit optimizer.
        /// </summary>
        public GrenadeOptimizer GrenadeOptimizer { get; private set; }

        /// <summary>
        /// Gets the round-lifecycle event handlers.
        /// </summary>
        public EventHandlers EventHandlers { get; private set; }

        /// <inheritdoc/>
        public override void OnEnabled()
        {
            Instance = this;
            StartTime = DateTime.UtcNow;

            RagdollOptimizer = new RagdollOptimizer(this);
            PickupOptimizer = new PickupOptimizer(this);
            GcOptimizer = new GcOptimizer(this);
            GrenadeOptimizer = new GrenadeOptimizer(this);
            EventHandlers = new EventHandlers(this);

            Exiled.Events.Handlers.Server.RoundEnded += EventHandlers.OnRoundEnded;

            RagdollOptimizer.Start();
            PickupOptimizer.Start();
            GcOptimizer.Start();
            GrenadeOptimizer.Start();

            Log.Info("TpsOptimizer v1.0.0 enabled.");

            base.OnEnabled();
        }

        /// <inheritdoc/>
        public override void OnDisabled()
        {
            if (EventHandlers != null)
                Exiled.Events.Handlers.Server.RoundEnded -= EventHandlers.OnRoundEnded;

            RagdollOptimizer?.Stop();
            PickupOptimizer?.Stop();
            GcOptimizer?.Stop();
            GrenadeOptimizer?.Stop();

            RagdollOptimizer = null;
            PickupOptimizer = null;
            GcOptimizer = null;
            GrenadeOptimizer = null;
            EventHandlers = null;
            Instance = null;

            Log.Info("TpsOptimizer disabled.");

            base.OnDisabled();
        }

        /// <summary>
        /// Stops and restarts every optimizer coroutine. Used by the <c>tpsreload</c> command so the
        /// optimizers pick up the current config values without a full server restart.
        /// </summary>
        public void RestartOptimizers()
        {
            RagdollOptimizer?.Stop();
            PickupOptimizer?.Stop();
            GcOptimizer?.Stop();
            GrenadeOptimizer?.Stop();

            RagdollOptimizer?.Start();
            PickupOptimizer?.Start();
            GcOptimizer?.Start();
            GrenadeOptimizer?.Start();
        }
    }
}
