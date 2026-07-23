namespace TpsOptimizer.Optimizers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Exiled.API.Features;
    using Exiled.API.Features.Pickups;
    using MEC;

    /// <summary>
    /// Periodically removes old floor items to keep their count under control, while never touching
    /// gameplay-critical items and never removing items a player is standing next to.
    /// </summary>
    public sealed class PickupOptimizer
    {
        /// <summary>
        /// Item types that are never removed by any optimizer or the round-end cleanup.
        /// </summary>
        private static readonly HashSet<ItemType> Protected = new HashSet<ItemType>
        {
            ItemType.MicroHID,
            ItemType.KeycardO5,
            ItemType.SCP330,
            ItemType.SCP207,
            ItemType.SCP268,
            ItemType.SCP500,
            ItemType.SCP1853,
            ItemType.SCP018,
            ItemType.SCP2176,
            ItemType.Jailbird,
        };

        private readonly Plugin _plugin;

        // Serial -> first time this optimizer observed the pickup. Pickups expose no creation time,
        // so we derive age from DateTime.UtcNow relative to when we first saw each serial.
        private readonly Dictionary<ushort, DateTime> _firstSeen = new Dictionary<ushort, DateTime>();

        // Предметы, заспавненные картой на старте раунда — их никогда не удаляем.
        private readonly HashSet<ushort> _mapItems = new HashSet<ushort>();

        private CoroutineHandle _handle;

        /// <summary>
        /// Initializes a new instance of the <see cref="PickupOptimizer"/> class.
        /// </summary>
        /// <param name="plugin">The owning plugin instance.</param>
        public PickupOptimizer(Plugin plugin) => _plugin = plugin;

        private Config Config => _plugin.Config;

        /// <summary>
        /// Determines whether the given item type is protected and must never be removed.
        /// </summary>
        /// <param name="type">The item type to check.</param>
        /// <returns><see langword="true"/> if the item type is protected; otherwise, <see langword="false"/>.</returns>
        public static bool IsProtected(ItemType type) => Protected.Contains(type);

        /// <summary>
        /// Starts the cleanup coroutine.
        /// </summary>
        public void Start()
        {
            _firstSeen.Clear();
            _mapItems.Clear();
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
            _handle = Timing.RunCoroutine(CleanupLoop());
        }

        /// <summary>
        /// Stops the cleanup coroutine.
        /// </summary>
        public void Stop()
        {
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
            Timing.KillCoroutines(_handle);
            _firstSeen.Clear();
            _mapItems.Clear();
        }

        private void OnRoundStarted()
        {
            _firstSeen.Clear();
            _mapItems.Clear();

            // Через 5 секунд после старта запоминаем все предметы карты — они защищены от очистки.
            Timing.CallDelayed(5f, () =>
            {
                foreach (Pickup pickup in Pickup.List)
                {
                    if (pickup != null)
                        _mapItems.Add(pickup.Serial);
                }
            });
        }

        private IEnumerator<float> CleanupLoop()
        {
            while (true)
            {
                float interval = Config.PickupCleanupInterval > 0f ? Config.PickupCleanupInterval : 120f;
                yield return Timing.WaitForSeconds(interval);

                if (!Config.IsEnabled)
                    continue;

                try
                {
                    Clean();
                }
                catch
                {
                    // Pickups may already be destroyed; never let cleanup crash the coroutine.
                }
            }
        }

        private void Clean()
        {
            DateTime now = DateTime.UtcNow;

            // Copy the collection before iterating: destroying pickups mutates Pickup.List.
            List<Pickup> pickups = Pickup.List.Where(p => p != null).ToList();

            // Register newly-seen serials and prune ages for pickups that no longer exist.
            var present = new HashSet<ushort>();
            foreach (Pickup pickup in pickups)
            {
                present.Add(pickup.Serial);
                if (!_firstSeen.ContainsKey(pickup.Serial))
                    _firstSeen[pickup.Serial] = now;
            }

            foreach (ushort serial in _firstSeen.Keys.Where(s => !present.Contains(s)).ToList())
                _firstSeen.Remove(serial);

            int removed = 0;
            float lifetime = Config.PickupLifetime;
            float radiusCheckAge = Config.PickupAgeBeforeRadiusCheck;
            float sqrRadius = Config.PickupSafeRadius * Config.PickupSafeRadius;

            // Pass 1 - remove items older than the lifetime, keeping any that a player is standing near.
            foreach (Pickup pickup in pickups.ToList())
            {
                try
                {
                    if (pickup == null || IsProtected(pickup.Type) || _mapItems.Contains(pickup.Serial))
                        continue;

                    double age = GetAgeSeconds(pickup, now);
                    if (age <= lifetime)
                        continue;

                    // Older than lifetime (and therefore older than radiusCheckAge): keep if a player is near.
                    if (age > radiusCheckAge && IsPlayerNearby(pickup, sqrRadius))
                        continue;

                    pickup.Destroy();
                    _firstSeen.Remove(pickup.Serial);
                    pickups.Remove(pickup);
                    removed++;
                }
                catch
                {
                    pickups.Remove(pickup);
                }
            }

            // Pass 2 - if still above the cap, remove the oldest non-protected items (radius check for old ones).
            int countable = pickups.Count(p => p != null && !IsProtected(p.Type) && !_mapItems.Contains(p.Serial));
            if (countable > Config.MaxPickups)
            {
                List<Pickup> eligible = pickups
                    .Where(p => p != null && !IsProtected(p.Type) && !_mapItems.Contains(p.Serial))
                    .OrderByDescending(p => GetAgeSeconds(p, now))
                    .ToList();

                int excess = countable - Config.MaxPickups;
                foreach (Pickup pickup in eligible)
                {
                    if (excess <= 0)
                        break;

                    try
                    {
                        double age = GetAgeSeconds(pickup, now);
                        if (age > radiusCheckAge && IsPlayerNearby(pickup, sqrRadius))
                            continue;

                        pickup.Destroy();
                        _firstSeen.Remove(pickup.Serial);
                        pickups.Remove(pickup);
                        removed++;
                        excess--;
                    }
                    catch
                    {
                        pickups.Remove(pickup);
                    }
                }
            }

            if (removed > 0)
            {
                _plugin.TotalPickupsCleaned += removed;
                if (Config.LogCleanups)
                    Log.Debug($"[PickupOptimizer] Removed {removed} pickup(s). Remaining: {pickups.Count}.");
            }
        }

        private double GetAgeSeconds(Pickup pickup, DateTime now)
        {
            if (_firstSeen.TryGetValue(pickup.Serial, out DateTime seen))
                return (now - seen).TotalSeconds;

            // First time we have seen it in this exact call - treat as brand new.
            _firstSeen[pickup.Serial] = now;
            return 0d;
        }

        private static bool IsPlayerNearby(Pickup pickup, float sqrRadius)
        {
            var pickupPos = pickup.Position;
            foreach (Player player in Player.List)
            {
                try
                {
                    if (player == null || !player.IsAlive)
                        continue;

                    // Use squared distance to avoid an unnecessary square root.
                    if ((player.Position - pickupPos).sqrMagnitude <= sqrRadius)
                        return true;
                }
                catch
                {
                    // Ignore players whose state is momentarily invalid.
                }
            }

            return false;
        }
    }
}