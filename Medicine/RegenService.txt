using System;
using System.Collections.Generic;
using Exiled.API.Features;
using MEC;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Регенерация HP после полного лечения.
    /// Лёгкие раны: до 100 HP за 1 мин (≈1.67 HP/с)
    /// Средние: до 100 HP за 2 мин (≈0.83 HP/с)
    /// Тяжёлые: до 100 HP за 5 мин (≈0.33 HP/с)
    /// </summary>
    public enum RegenTier
    {
        None,
        Light,   // 1 мин до 100 HP
        Medium,  // 2 мин до 100 HP
        Heavy    // 5 мин до 100 HP
    }

    public class RegenState
    {
        public RegenTier Tier;
        public DateTime StartedAt;
    }

    public static class RegenStorage
    {
        private static readonly Dictionary<string, RegenState> _states = new();

        public static void Start(string userId, RegenTier tier)
        {
            // Берём самый тяжёлый тир если уже есть
            if (_states.TryGetValue(userId, out var existing))
            {
                if ((int)tier > (int)existing.Tier)
                    existing.Tier = tier;
                return;
            }

            _states[userId] = new RegenState
            {
                Tier = tier,
                StartedAt = DateTime.UtcNow
            };
        }

        public static void Stop(string userId) => _states.Remove(userId);

        public static bool TryGet(string userId, out RegenState state) =>
            _states.TryGetValue(userId, out state);

        public static void ClearAll() => _states.Clear();
    }

    public class RegenTickService
    {
        private CoroutineHandle _handle;

        public void Start() => _handle = Timing.RunCoroutine(TickLoop());
        public void Stop() => Timing.KillCoroutines(_handle);

        private IEnumerator<float> TickLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1f);

                foreach (var player in Player.List)
                {
                    if (!player.IsAlive)
                        continue;

                    if (!RegenStorage.TryGet(player.UserId, out var regen))
                        continue;

                    if (regen.Tier == RegenTier.None)
                        continue;

                    // Если у игрока ещё есть активные травмы — не хилим
                    if (MedicalStorage.TryGet(player.UserId, out var med) && med.HasAnything)
                    {
                        RegenStorage.Stop(player.UserId);
                        continue;
                    }

                    if (player.Health >= player.MaxHealth)
                    {
                        RegenStorage.Stop(player.UserId);
                        continue;
                    }

                    float hpPerSec = regen.Tier switch
                    {
                        RegenTier.Light  => 100f / 60f,   // ~1.67 HP/с
                        RegenTier.Medium => 100f / 120f,  // ~0.83 HP/с
                        RegenTier.Heavy  => 100f / 300f,  // ~0.33 HP/с
                        _                => 0f
                    };

                    player.Health = Math.Min(player.Health + hpPerSec, player.MaxHealth);
                }
            }
        }
    }
}
 