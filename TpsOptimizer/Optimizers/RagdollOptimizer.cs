namespace TpsOptimizer.Optimizers
{
    using System.Collections.Generic;
    using System.Linq;
    using Exiled.API.Features;
    using MEC;

    /// <summary>
    /// Periodically removes old ragdolls to keep their count under control.
    /// <para>
    /// Ragdolls younger than <see cref="Config.RagdollSafeTime"/> are never touched, so SCP-049 can
    /// still recall freshly-created bodies.
    /// </para>
    /// </summary>
    public sealed class RagdollOptimizer
    {
        private readonly Plugin _plugin;
        private CoroutineHandle _handle;

        /// <summary>
        /// Initializes a new instance of the <see cref="RagdollOptimizer"/> class.
        /// </summary>
        /// <param name="plugin">The owning plugin instance.</param>
        public RagdollOptimizer(Plugin plugin) => _plugin = plugin;

        private Config Config => _plugin.Config;

        /// <summary>
        /// Starts the cleanup coroutine.
        /// </summary>
        public void Start() => _handle = Timing.RunCoroutine(CleanupLoop());

        /// <summary>
        /// Stops the cleanup coroutine.
        /// </summary>
        public void Stop() => Timing.KillCoroutines(_handle);

        private IEnumerator<float> CleanupLoop()
        {
            while (true)
            {
                float interval = Config.RagdollCleanupInterval > 0f ? Config.RagdollCleanupInterval : 90f;
                yield return Timing.WaitForSeconds(interval);

                if (!Config.IsEnabled)
                    continue;

                try
                {
                    Clean();
                }
                catch
                {
                    // Ragdolls may already be destroyed; never let cleanup crash the coroutine.
                }
            }
        }

        private void Clean()
        {
            // Copy the collection before iterating: destroying ragdolls mutates Ragdoll.List.
            List<Ragdoll> ragdolls = Ragdoll.List.ToList();
            int removed = 0;

            float safeTime = Config.RagdollSafeTime;
            float lifetime = Config.RagdollLifetime;

            // Pass 1 - remove ragdolls older than the lifetime (but never those younger than safe time).
            foreach (Ragdoll ragdoll in ragdolls.ToList())
            {
                try
                {
                    if (ragdoll == null)
                    {
                        ragdolls.Remove(ragdoll);
                        continue;
                    }

                    // ExistenceTime is the age in seconds and is timezone-independent, unlike CreationTime
                    // (which EXILED derives from DateTime.Now). We use it to compute the object's age.
                    float age = ragdoll.ExistenceTime;
                    if (age < safeTime)
                        continue;

                    if (age > lifetime)
                    {
                        ragdoll.Destroy();
                        ragdolls.Remove(ragdoll);
                        removed++;
                    }
                }
                catch
                {
                    ragdolls.Remove(ragdoll);
                }
            }

            // Pass 2 - if still above the cap, remove the oldest eligible ragdolls (still respecting safe time).
            if (ragdolls.Count > Config.MaxRagdolls)
            {
                List<Ragdoll> eligible = ragdolls
                    .Where(r => IsEligible(r, safeTime))
                    .OrderByDescending(GetAge)
                    .ToList();

                int toRemove = ragdolls.Count - Config.MaxRagdolls;
                foreach (Ragdoll ragdoll in eligible)
                {
                    if (toRemove <= 0)
                        break;

                    try
                    {
                        ragdoll.Destroy();
                        ragdolls.Remove(ragdoll);
                        removed++;
                        toRemove--;
                    }
                    catch
                    {
                        ragdolls.Remove(ragdoll);
                    }
                }
            }

            if (removed > 0)
            {
                _plugin.TotalRagdollsCleaned += removed;
                if (Config.LogCleanups)
                    Log.Debug($"[RagdollOptimizer] Removed {removed} ragdoll(s). Remaining: {ragdolls.Count}.");
            }
        }

        private static bool IsEligible(Ragdoll ragdoll, float safeTime)
        {
            try
            {
                return ragdoll != null && ragdoll.ExistenceTime >= safeTime;
            }
            catch
            {
                return false;
            }
        }

        private static float GetAge(Ragdoll ragdoll)
        {
            try
            {
                return ragdoll.ExistenceTime;
            }
            catch
            {
                return 0f;
            }
        }
    }
}
