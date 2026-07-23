using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Map;
using UnityEngine;

namespace EventHUD.AntiAdm
{
    /// <summary>
    /// Обработчик взрывов гранат:
    /// 1. Если рядом с взрывом >= порог предметов — очищает их (антилаг).
    /// 2. Если рядом >= порог гранат — блокирует детонацию цепных гранат.
    /// </summary>
    public class AntiAdmGrenadeHandler
    {
        private readonly Config _config;

        public AntiAdmGrenadeHandler(Config config)
        {
            _config = config;
        }

        public void OnExplodingGrenade(ExplodingGrenadeEventArgs ev)
        {
            if (!_config.AntiAdmEnabled) return;
            if (ev.Projectile == null) return;

            Vector3 explosionPos = ev.Projectile.Position;

            // ── Цепная детонация ──
            // Считаем гранаты в радиусе
            float chainRadius = _config.AntiAdmGrenadeChainRadius;
            int nearbyGrenades = CountNearbyGrenades(explosionPos, chainRadius, ev.Projectile);

            if (nearbyGrenades >= _config.AntiAdmGrenadeChainThreshold)
            {
                // Блокируем и удаляем соседние гранаты
                DestroyNearbyGrenades(explosionPos, chainRadius, ev.Projectile);
                // Саму гранату не блокируем — она уже взрывается
            }

            // ── Очистка предметов ──
            float cleanRadius = _config.AntiAdmGrenadeItemCleanRadius;
            var nearbyItems = GetNearbyPickups(explosionPos, cleanRadius);

            if (nearbyItems.Count >= _config.AntiAdmGrenadeItemCleanThreshold)
            {
                foreach (var pickup in nearbyItems)
                {
                    try { pickup.Destroy(); } catch { }
                }
            }
        }

        private int CountNearbyGrenades(Vector3 pos, float radius, Exiled.API.Features.Pickups.Projectiles.Projectile exclude)
        {
            int count = 0;
            float sqrRadius = radius * radius;

            foreach (var pickup in Pickup.List)
            {
                if (pickup == null || pickup == exclude) continue;
                if (pickup.Type != ItemType.GrenadeHE && pickup.Type != ItemType.GrenadeFlash)
                    continue;
                if ((pickup.Position - pos).sqrMagnitude <= sqrRadius)
                    count++;
            }
            return count;
        }

        private void DestroyNearbyGrenades(Vector3 pos, float radius, Exiled.API.Features.Pickups.Projectiles.Projectile exclude)
        {
            float sqrRadius = radius * radius;
            var toDestroy = new List<Pickup>();

            foreach (var pickup in Pickup.List)
            {
                if (pickup == null || pickup == exclude) continue;
                if (pickup.Type != ItemType.GrenadeHE && pickup.Type != ItemType.GrenadeFlash)
                    continue;
                if ((pickup.Position - pos).sqrMagnitude <= sqrRadius)
                    toDestroy.Add(pickup);
            }

            foreach (var p in toDestroy)
            {
                try { p.Destroy(); } catch { }
            }
        }

        private List<Pickup> GetNearbyPickups(Vector3 pos, float radius)
        {
            float sqrRadius = radius * radius;
            var result = new List<Pickup>();

            foreach (var pickup in Pickup.List)
            {
                if (pickup == null) continue;
                if ((pickup.Position - pos).sqrMagnitude <= sqrRadius)
                    result.Add(pickup);
            }
            return result;
        }
    }
}
