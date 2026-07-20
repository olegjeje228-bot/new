using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using MEC;
using UnityEngine;

namespace EventHUD.AntiAdm
{
    /// <summary>
    /// Анти-лаг: ограничение плотности рагдоллов и предметов.
    ///
    /// Рагдоллы:
    ///   - Не более 9 в радиусе 10 метров
    ///   - Не более 20 в радиусе 100 метров
    ///   - Удаляем самые старые
    ///
    /// Предметы (только выброшенные, не заспавненные map editor):
    ///   - Не более 120 в радиусе 10 метров
    ///   - Удаляем случайно (не по старости)
    ///
    /// При спавне предметов через map editor (mp load / mp cr) —
    /// временно отключаемся на 5 секунд.
    /// </summary>
    public class AntiAdmAntiLagService
    {
        private readonly Config _config;
        private CoroutineHandle _handle;

        // Временное отключение (при map editor spawn)
        private DateTime _disabledUntil = DateTime.MinValue;

        public AntiAdmAntiLagService(Config config)
        {
            _config = config;
        }

        public void Start()
        {
            _handle = Timing.RunCoroutine(ScanLoop());
        }

        public void Stop()
        {
            Timing.KillCoroutines(_handle);
        }

        /// <summary>
        /// Временно отключить проверку предметов (например, при map editor spawn).
        /// </summary>
        public void TemporarilyDisable(float seconds)
        {
            _disabledUntil = DateTime.UtcNow.AddSeconds(seconds);
        }

        private bool IsTemporarilyDisabled => DateTime.UtcNow < _disabledUntil;

        private IEnumerator<float> ScanLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(_config.AntiLagScanInterval);

                if (!_config.AntiAdmEnabled) continue;

                try
                {
                    CleanupRagdolls();
                    CleanupScp018Balls();
                    if (!IsTemporarilyDisabled)
                        CleanupPickups();
                }
                catch (Exception ex)
                {
                    Log.Debug($"[AntiLag] ScanLoop: {ex.Message}");
                }
            }
        }

        // ── Рагдоллы ──
        private void CleanupRagdolls()
        {
            var ragdolls = Ragdoll.List.ToList();
            if (ragdolls.Count == 0) return;

            if (_config.Debug)
                Log.Debug($"[AntiLag] Ragdolls on map: {ragdolls.Count}");

            float closeRadius = _config.AntiLagRagdollCloseRadius;   // 10м
            int closeMax = _config.AntiLagRagdollCloseMax;            // 9
            int farMax = _config.AntiLagRagdollFarMax;               // 20

            float sqrClose = closeRadius * closeRadius;

            // Сначала проверяем глобальный лимит (farMax на всей карте)
            // Если всего рагдоллов больше farMax — удаляем самые старые
            if (ragdolls.Count > farMax)
            {
                var sorted = ragdolls.OrderBy(r => r.CreationTime).ToList();
                int toRemove = ragdolls.Count - farMax;
                for (int i = 0; i < toRemove; i++)
                {
                    try { sorted[i].Destroy(); } catch { }
                }
                if (_config.Debug)
                    Log.Debug($"[AntiLag] Removed {toRemove} ragdolls (global limit {farMax})");
                ragdolls = Ragdoll.List.ToList();
            }

            // Проверяем локальные кластеры (closeMax в closeRadius)
            var toDelete = new HashSet<Ragdoll>();
            foreach (var rag in ragdolls)
            {
                if (rag == null || toDelete.Contains(rag)) continue;

                var cluster = new List<Ragdoll>();
                foreach (var other in ragdolls)
                {
                    if (other == null || toDelete.Contains(other)) continue;
                    if ((other.Position - rag.Position).sqrMagnitude <= sqrClose)
                        cluster.Add(other);
                }

                if (cluster.Count > closeMax)
                {
                    // Сортируем по старости — удаляем самые старые
                    cluster = cluster.OrderBy(r => r.CreationTime).ToList();
                    int remove = cluster.Count - closeMax;
                    for (int i = 0; i < remove; i++)
                    {
                        toDelete.Add(cluster[i]);
                    }
                }
            }

            foreach (var rag in toDelete)
            {
                try { rag.Destroy(); } catch { }
            }

            if (_config.Debug && toDelete.Count > 0)
                Log.Debug($"[AntiLag] Removed {toDelete.Count} ragdolls from clusters");
        }

        // ── Предметы (выброшенные) ──
        private void CleanupPickups()
        {
            var pickups = new List<Pickup>();
            foreach (var p in Pickup.List)
            {
                if (p == null) continue;
                pickups.Add(p);
            }

            if (pickups.Count == 0) return;

            float radius = _config.AntiLagPickupRadius;    // 10м
            int max = _config.AntiLagPickupMax;            // 120
            float sqrRadius = radius * radius;

            // ── Ближний кластер (2м, макс 40) ──
            float closeRadius = 2f;
            int closeMax = _config.AntiLagPickupCloseMax;  // 40
            float sqrClose = closeRadius * closeRadius;

            var toDelete = new HashSet<Pickup>();

            // Проверяем ближние кластеры (2м)
            foreach (var pickup in pickups)
            {
                if (pickup == null || toDelete.Contains(pickup)) continue;

                var closeCluster = new List<Pickup>();
                foreach (var other in pickups)
                {
                    if (other == null || toDelete.Contains(other)) continue;
                    if ((other.Position - pickup.Position).sqrMagnitude <= sqrClose)
                        closeCluster.Add(other);
                }

                if (closeCluster.Count > closeMax)
                {
                    int remove = closeCluster.Count - closeMax;
                    Shuffle(closeCluster);
                    for (int i = 0; i < remove; i++)
                    {
                        toDelete.Add(closeCluster[i]);
                    }
                }
            }

            // Проверяем кластеры (10м)
            foreach (var pickup in pickups)
            {
                if (pickup == null || toDelete.Contains(pickup)) continue;

                var cluster = new List<Pickup>();
                foreach (var other in pickups)
                {
                    if (other == null || toDelete.Contains(other)) continue;
                    if ((other.Position - pickup.Position).sqrMagnitude <= sqrRadius)
                        cluster.Add(other);
                }

                if (cluster.Count > max)
                {
                    // Удаляем СЛУЧАЙНО, не по старости
                    int remove = cluster.Count - max;
                    // Перемешиваем и берём первые remove
                    Shuffle(cluster);
                    for (int i = 0; i < remove; i++)
                    {
                        toDelete.Add(cluster[i]);
                    }
                }
            }

            foreach (var p in toDelete)
            {
                try { p.Destroy(); } catch { }
            }
        }

        // Fisher-Yates shuffle
        private static void Shuffle<T>(List<T> list)
        {
            var rng = new System.Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                var value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        // ── SCP-018 мячики ──
        // Глобальный лимит: максимум AntiLagMaxBallsGlobal на всей карте.
        // Локальный лимит: максимум AntiLagMaxBallsPerRadius в радиусе AntiLagBallRadius.
        // Удаляем старые/дальние от игроков.
        private void CleanupScp018Balls()
        {
            try
            {
                var balls = new List<Pickup>();
                foreach (var p in Pickup.List)
                {
                    if (p == null) continue;
                    if (p.Type == ItemType.SCP018)
                        balls.Add(p);
                }

                if (balls.Count == 0) return;

                // ── Глобальный лимит ──
                int globalMax = _config.AntiLagMaxBallsGlobal;
                if (balls.Count > globalMax)
                {
                    // Сортируем по расстоянию до ближайшего игрока (дальние первыми на удаление)
                    var sorted = balls
                        .OrderBy(b => Player.List.Any(p => p != null && p.IsAlive) ?
                            Player.List.Where(p => p != null && p.IsAlive)
                                .Min(p => Vector3.Distance(p.Position, b.Position)) : 0f)
                        .ToList();
                    int toRemove = balls.Count - globalMax;
                    for (int i = sorted.Count - 1; i >= sorted.Count - toRemove; i--)
                    {
                        try { sorted[i].Destroy(); } catch { }
                    }
                    balls = new List<Pickup>();
                    foreach (var p in Pickup.List)
                    {
                        if (p != null && p.Type == ItemType.SCP018)
                            balls.Add(p);
                    }
                }

                // ── Локальный лимит (в радиусе) ──
                float radius = _config.AntiLagBallRadius;
                int perRadiusMax = _config.AntiLagMaxBallsPerRadius;
                float sqrRadius = radius * radius;

                var toDelete = new HashSet<Pickup>();
                var processed = new HashSet<ushort>();

                foreach (var center in balls)
                {
                    if (processed.Contains(center.Serial)) continue;

                    var cluster = new List<Pickup>();
                    foreach (var other in balls)
                    {
                        if (processed.Contains(other.Serial)) continue;
                        if ((other.Position - center.Position).sqrMagnitude <= sqrRadius)
                            cluster.Add(other);
                    }

                    if (cluster.Count > perRadiusMax)
                    {
                        // Оставляем ближайшие к игрокам, удаляем дальние
                        var sorted = cluster.OrderBy(b =>
                        {
                            float minDist = float.MaxValue;
                            foreach (var p in Player.List)
                            {
                                if (p == null || !p.IsAlive) continue;
                                float d = Vector3.Distance(p.Position, b.Position);
                                if (d < minDist) minDist = d;
                            }
                            return minDist;
                        }).ToList();

                        int remove = cluster.Count - perRadiusMax;
                        for (int i = sorted.Count - 1; i >= sorted.Count - remove; i--)
                        {
                            toDelete.Add(sorted[i]);
                            processed.Add(sorted[i].Serial);
                        }
                    }

                    foreach (var b in cluster)
                        processed.Add(b.Serial);
                }

                foreach (var p in toDelete)
                {
                    try { p.Destroy(); } catch { }
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"[AntiLag] SCP-018 cleanup: {ex.Message}");
            }
        }
    }
}
