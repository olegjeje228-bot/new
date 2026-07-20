using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using MEC;

namespace EventHUD.AntiAdm
{
    /// <summary>
    /// Периодически проверяет плотность гранат (антилаг).
    /// Если в радиусе N метров больше Max гранат — удаляет лишние, оставляя Limit.
    /// AA-09.
    /// </summary>
    public class AntiAdmGrenadeDensityService
    {
        private readonly Config _config;
        private CoroutineHandle _handle;

        public AntiAdmGrenadeDensityService(Config config)
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

        private IEnumerator<float> ScanLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(_config.AntiAdmGrenadeDensityInterval);

                if (!_config.AntiAdmEnabled) continue;

                try { ScanAndClean(); } catch { }
            }
        }

        private void ScanAndClean()
        {
            float radius = _config.AntiAdmGrenadeDensityRadius;
            int max = _config.AntiAdmGrenadeDensityMax;
            int limit = _config.AntiAdmGrenadeDensityLimit;
            float sqrRadius = radius * radius;

            if (limit < 0) limit = 0;
            if (max < limit) max = limit;

            // Собираем все гранаты (дропнутые + брошенные снаряды)
            var grenades = new List<Pickup>();
            foreach (var pickup in Pickup.List)
            {
                if (pickup == null) continue;
                if (pickup.Type == ItemType.GrenadeHE || pickup.Type == ItemType.GrenadeFlash)
                    grenades.Add(pickup);
            }

            if (grenades.Count <= max) return;

            // Итеративно чистим самые плотные кластеры (ограничение проходов)
            for (int iteration = 0; iteration < 50; iteration++)
            {
                List<Pickup> densestCluster = null;

                foreach (var g in grenades)
                {
                    if (g == null) continue;
                    var cluster = new List<Pickup>();
                    foreach (var other in grenades)
                    {
                        if (other == null) continue;
                        if ((other.Position - g.Position).sqrMagnitude <= sqrRadius)
                            cluster.Add(other);
                    }
                    if (densestCluster == null || cluster.Count > densestCluster.Count)
                        densestCluster = cluster;
                }

                if (densestCluster == null || densestCluster.Count <= max)
                    break; // нет кластеров, превышающих порог

                // Удаляем лишние, оставляя limit
                int toRemove = densestCluster.Count - limit;
                for (int i = 0; i < toRemove && i < densestCluster.Count; i++)
                {
                    var p = densestCluster[i];
                    if (p == null) continue;
                    try { p.Destroy(); } catch { }
                    grenades.Remove(p);
                }
            }
        }
    }
}
