using System;
using System.Collections.Generic;
using System.Linq;
using EventHUD.Hud;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using MEC;
using UnityEngine;

namespace EventHUD.AntiAdm
{
    /// <summary>
    /// TPS-адаптивная оптимизация сервера.
    /// 3 уровня оптимизации + плотный рестарт с задержкой.
    /// </summary>
    public class TpsOptimizerService
    {
        private readonly Config _config;
        private CoroutineHandle _scanHandle;
        private DateTime? _level3Since;
        private int _currentLevel = 0; // 0 = норма

        // Сохранённые предметы для восстановления (упрощённо — только серийники)
        private readonly HashSet<ushort> _hiddenPickups = new();

        // Предметы карты на старте раунда — не трогаем при любой очистке
        private readonly HashSet<ushort> _mapItems = new();

        public TpsOptimizerService(Config config)
        {
            _config = config;
        }

        public void Start() => _scanHandle = Timing.RunCoroutine(ScanLoop());
        public void Stop() => Timing.KillCoroutines(_scanHandle);

        /// <summary>Запоминает предметы карты на старте раунда — их не удаляем.</summary>
        public void SnapshotMapItems()
        {
            _mapItems.Clear();
            Timing.CallDelayed(5f, () =>
            {
                foreach (var pickup in Pickup.List)
                {
                    if (pickup != null)
                        _mapItems.Add(pickup.Serial);
                }
            });
        }

        private IEnumerator<float> ScanLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1f);

                if (!_config.AntiAdmEnabled)
                    continue;

                float tps = GetTps();
                UpdateLevel(tps);

                switch (_currentLevel)
                {
                    case 1:
                        ApplyLevel1();
                        break;
                    case 2:
                        ApplyLevel1();
                        ApplyLevel2();
                        break;
                    case 3:
                        ApplyLevel1();
                        ApplyLevel2();
                        TryDenseRestart();
                        break;
                    default:
                        RestoreHiddenItems();
                        break;
                }
            }
        }

        private float GetTps()
        {
            try
            {
                return (float)Server.Tps;
            }
            catch
            {
                return 60f;
            }
        }

        private void UpdateLevel(float tps)
        {
            int newLevel;
            if (tps < _config.TpsLevel3Threshold)
                newLevel = 3;
            else if (tps < _config.TpsLevel2Threshold)
                newLevel = 2;
            else if (tps < _config.TpsLevel1Threshold)
                newLevel = 1;
            else
                newLevel = 0;

            if (newLevel == 3)
            {
                if (_level3Since == null)
                    _level3Since = DateTime.UtcNow;
            }
            else
            {
                _level3Since = null;
            }

            if (newLevel != _currentLevel)
            {
                Log.Debug($"[TpsOptimizer] Level changed: {_currentLevel} -> {newLevel} (TPS={tps:F1})");
                _currentLevel = newLevel;
            }
        }

        // ── Уровень 1: TPS 20-49 ──
        // Только очистка кластеров (не удаляем предметы вдалеке — ломает NRPG)
        private void ApplyLevel1()
        {
            try
            {
                // Кластеры предметов
                CleanupClusters();
            }
            catch { }
        }

        // ── Уровень 2: TPS 10-19 ──
        // Мячики (>2), кластеры >70
        private void ApplyLevel2()
        {
            try
            {
                // Мячики SCP-330: оставляем максимум 2
                var candies = Pickup.List
                    .Where(p => p != null && p.Type == ItemType.SCP330 && !_mapItems.Contains(p.Serial))
                    .ToList();
                if (candies.Count > _config.TpsMaxCandiesBeforeCleanup)
                {
                    for (int i = 0; i < candies.Count - _config.TpsMaxCandiesBeforeCleanup; i++)
                        candies[i].Destroy();
                }

                // Кластеры предметов
                CleanupClusters();
            }
            catch { }
        }

        private void CleanupClusters()
        {
            var pickups = Pickup.List.Where(p => p != null && !_mapItems.Contains(p.Serial)).ToList();
            var processed = new HashSet<ushort>();

            foreach (var center in pickups)
            {
                if (processed.Contains(center.Serial)) continue;

                var cluster = pickups
                    .Where(p => Vector3.Distance(p.Position, center.Position) <= _config.TpsClusterRadius)
                    .ToList();

                if (cluster.Count > _config.TpsClusterCleanupThreshold)
                {
                    // Сортируем по приоритету удаления
                    var sorted = cluster
                        .Select(p => new { Pickup = p, Priority = GetCleanupPriority(p.Type) })
                        .OrderByDescending(x => x.Priority)
                        .ToList();

                    int toRemove = cluster.Count - _config.TpsClusterCleanupThreshold;
                    for (int i = 0; i < toRemove && i < sorted.Count; i++)
                    {
                        sorted[i].Pickup.Destroy();
                        processed.Add(sorted[i].Pickup.Serial);
                    }
                }
            }
        }

        /// <summary>
        /// Чем выше число — тем раньше удаляем.
        /// 1: мусор, фонарики, патроны, монетки
        /// 2: карточки, рация, медкит
        /// 3: SCP, оружие, спец-оружие (не трогаем)
        /// </summary>
        private int GetCleanupPriority(ItemType type)
        {
            // Мусор / фонарик / патроны / монетки
            if (type == ItemType.Coin ||
                type == ItemType.Flashlight ||
                IsAmmoType(type) ||
                type == ItemType.Jailbird) // jailbird как мусор
                return 1;

            // Карточки / рация / медкит
            if (type == ItemType.KeycardJanitor ||
                type == ItemType.KeycardScientist ||
                type == ItemType.KeycardResearchCoordinator ||
                type == ItemType.KeycardZoneManager ||
                type == ItemType.KeycardGuard ||
                type == ItemType.KeycardMTFOperative ||
                type == ItemType.KeycardMTFCaptain ||
                type == ItemType.KeycardFacilityManager ||
                type == ItemType.KeycardChaosInsurgency ||
                type == ItemType.KeycardO5 ||
                type == ItemType.Radio ||
                type == ItemType.Medkit)
                return 2;

            // SCP / оружие / спец-оружие — не трогаем (приоритет 0)
            return 0;
        }

        private bool IsAmmoType(ItemType type)
        {
            return type == ItemType.Ammo556x45 ||
                   type == ItemType.Ammo762x39 ||
                   type == ItemType.Ammo9x19 ||
                   type == ItemType.Ammo12gauge ||
                   type == ItemType.Ammo44cal;
        }

        // ── Уровень 3: TPS < 10 дольше 15 сек ──
        private void TryDenseRestart()
        {
            if (_level3Since == null) return;

            double elapsed = (DateTime.UtcNow - _level3Since.Value).TotalSeconds;
            if (elapsed < _config.TpsDenseRestartDelaySeconds) return;

            Log.Warn($"[TpsOptimizer] Dense restart triggered after {elapsed:F0}s of TPS < {_config.TpsLevel3Threshold}");
            PerformDenseRestart();
            _level3Since = null;
        }

        private void PerformDenseRestart()
        {
            try
            {
                // Очистка всех кэшей
                Radio.RadioFrequencyStorage.ClearAll();
                Radio.RadioCustomFrequencyStorage.ClearAll();
                Medicine.MedicalStorage.ClearAll();
                Medicine.MedkitStorage.ClearAll();
                Medicine.MedkitInventoryStorage.ClearAll();
                Medicine.ArmorStorage.ClearAll();
                Medicine.RegenStorage.ClearAll();
                Medicine.ArmorRemovalStorage.ClearAll();

                Plugin.Instance?.Hud?.Stop();
                Plugin.Instance?.Hud?.Start();
                HudToggleService.Reset();

                Log.Warn("[TpsOptimizer] All caches cleared (dense restart)");
            }
            catch (Exception ex)
            {
                Log.Error($"[TpsOptimizer] Dense restart failed: {ex}");
            }
        }

        private void RestoreHiddenItems()
        {
            // Восстановление удалённых предметов невозможно без сохранения состояния.
            // Очищаем список, чтобы новые спавны не считались "скрытыми".
            _hiddenPickups.Clear();
        }
    }
}