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
    /// 2. Если рядом >= порог гранат — удаляет лишние сверх лимита.
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

            // ── Лимит гранат в одной точке ──
            float chainRadius = _config.AntiAdmGrenadeChainRadius;
            var nearbyGrenades = GetNearbyGrenades(
                explosionPos, chainRadius, ev.Projectile);

            int maxAllowed = _config.AntiAdmMaxGrenadesPerSpot;

            if (nearbyGrenades.Count > maxAllowed)
            {
                // Оставляем maxAllowed ближайших, удаляем только лишние
                foreach (var pickup in nearbyGrenades
                             .OrderBy(p => (p.Position - explosionPos).sqrMagnitude)
                             .Skip(maxAllowed))
                {
                    try { pickup.Destroy(); } catch { }
                }
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

        private List<Pickup> GetNearbyGrenades(
            Vector3 pos,
            float radius,
            Exiled.API.Features.Pickups.Projectiles.Projectile exclude)
        {
            float sqrRadius = radius * radius;
            var result = new List<Pickup>();

            foreach (var pickup in Pickup.List)
            {
                if (pickup == null || pickup == exclude) continue;
                if (pickup.Type != ItemType.GrenadeHE &&
                    pickup.Type != ItemType.GrenadeFlash)
                    continue;
                if ((pickup.Position - pos).sqrMagnitude <= sqrRadius)
                    result.Add(pickup);
            }

            return result;
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