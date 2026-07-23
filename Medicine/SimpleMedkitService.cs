using System;
using System.Collections.Generic;
using Exiled.API.Features;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Лечение без биндов (FullRP выключен).
    /// Аптечки типизированы (гражданская/рабочая/военная/парамедик) и имеют
    /// общий ресурс использований: 5 / 10 / 15 / 30.
    /// Капиллярное = 1, венозное = 2, артериальное = 3, перелом = 3,
    /// остальные травмы = 2, ушиб = 1 использование.
    /// </summary>
    public class SimpleMedkitService
    {
        private class Progress { public string TargetKey; public int Uses; }

        private readonly Dictionary<string, Progress> _progress = new Dictionary<string, Progress>();
        private readonly Dictionary<string, DateTime> _lastUse = new Dictionary<string, DateTime>();
        private const float UseCooldown = 2f;

        public void ResetPlayer(string userId)
        {
            _progress.Remove(userId);
            _lastUse.Remove(userId);
        }

        public void ClearAll()
        {
            _progress.Clear();
            _lastUse.Clear();
        }

        public void OnMedkitUsed(Player player, ushort serial)
        {
            if (player == null || !player.IsAlive) return;

            if (_lastUse.TryGetValue(player.UserId, out var last) &&
                (DateTime.UtcNow - last).TotalSeconds < UseCooldown)
                return;
            _lastUse[player.UserId] = DateTime.UtcNow;

            if (!MedkitInventoryStorage.TryGet(serial, out var kit))
                kit = MedkitInventoryStorage.GetOrCreate(serial, MedkitTypeAssigner.GetByRole(player));

            if (kit.SimpleUses <= 0)
                kit.SimpleUses = GetMaxUses(kit.Type);

            kit.SimpleUses--;

            ProcessHealing(player);

            MedkitInventoryStorage.Remove(serial);
            if (kit.SimpleUses > 0)
            {
                var newItem = player.AddItem(ItemType.Medkit);
                if (newItem != null)
                    MedkitInventoryStorage.Set(newItem.Serial, kit);
            }
        }

        private void ProcessHealing(Player player)
        {
            var medState = MedicalStorage.GetOrCreate(player.UserId);

            var bleeding = medState.GetBleedingLevel();
            if (bleeding.HasValue)
            {
                var target = bleeding.Value;
                HandleTarget(player, $"g:{target}", GetUsesFor(target),
                    () => MedkitHealService.CureGlobal(player, medState, target));
                return;
            }

            LocalInjury injury = null;
            foreach (var inj in medState.Injuries)
            {
                if (inj.Type == LocalInjuryType.Corrosion) continue;
                injury = inj;
                break;
            }

            if (injury == null)
            {
                _progress.Remove(player.UserId);
                return;
            }

            var type = injury.Type;
            var part = injury.Part;
            HandleTarget(player, $"l:{type}:{part}", GetUsesFor(type),
                () => MedkitHealService.CureLocal(player, medState, type, part));
        }

        private void HandleTarget(Player player, string key, int required, Action cure)
        {
            if (!_progress.TryGetValue(player.UserId, out var p) || p.TargetKey != key)
            {
                p = new Progress { TargetKey = key, Uses = 0 };
                _progress[player.UserId] = p;
            }

            p.Uses++;

            if (p.Uses >= required)
            {
                _progress.Remove(player.UserId);
                cure();
            }
        }

        public static int GetMaxUses(MedkitType type)
        {
            switch (type)
            {
                case MedkitType.Paramedic:  return 30;
                case MedkitType.Military:   return 15;
                case MedkitType.Industrial: return 10;
                default:                    return 5;
            }
        }

        private int GetUsesFor(GlobalCondition bleeding)
        {
            switch (bleeding)
            {
                case GlobalCondition.BleedingHeavy:  return 3;
                case GlobalCondition.BleedingMedium: return 2;
                default:                             return 1;
            }
        }

        private int GetUsesFor(LocalInjuryType type)
        {
            switch (type)
            {
                case LocalInjuryType.Fracture: return 3;
                case LocalInjuryType.Bruise:   return 1;
                default:                       return 2;
            }
        }
    }
}