using System;
using System.Collections.Generic;
using CustomPlayerEffects;
using EventHUD.Hud;
using Exiled.API.Features;
using MEC;

namespace EventHUD.Medicine
{
    public class MedkitHealService
    {
        private readonly Dictionary<string, CoroutineHandle> _healCoroutines = new();
        private readonly Dictionary<string, ushort> _activeKitSerial = new();

        public void StartHealing(Player player)
        {
            if (player.CurrentItem == null || player.CurrentItem.Type != ItemType.Medkit) return;

            ushort kitSerial = player.CurrentItem.Serial;
            var kit = MedkitInventoryStorage.GetOrCreate(kitSerial);
            if (kit == null) return;

            var medState = MedicalStorage.GetOrCreate(player.UserId);
            var menuState = MedkitStorage.GetOrCreate(player.UserId);
            var items = MedkitMenuBuilder.Build(medState, Plugin.Instance.Config, kit, false);

            if (items.Count == 0 || menuState.SelectedIndex >= items.Count) return;
            var selected = items[menuState.SelectedIndex];
            if (!selected.CanHeal || selected.Stages.Count == 0 || selected.IsBack) return;

            CancelHealing(player.UserId);
            menuState.IsHealing = true;
            menuState.CurrentStage = 0;
            menuState.HealElapsed = 0;
            menuState.HealStartedAt = DateTime.UtcNow;
            menuState.HealingItem = selected;

            _activeKitSerial[player.UserId] = kitSerial;

            if (selected.LocalTarget == LocalInjuryType.Chemical) player.DisableEffect<Concussed>();
            if (selected.LocalTarget == LocalInjuryType.Burn) player.DisableEffect<Burned>();

            _healCoroutines[player.UserId] = Timing.RunCoroutine(HealCoroutine(player, menuState, medState));
        }

        public void CancelHealing(string userId)
        {
            if (_healCoroutines.TryGetValue(userId, out var handle)) { Timing.KillCoroutines(handle); _healCoroutines.Remove(userId); }
            _activeKitSerial.Remove(userId);
            if (MedkitStorage.TryGet(userId, out var menuState)) menuState.Reset();
        }

        public void ClearAll()
        {
            foreach (var handle in _healCoroutines.Values) Timing.KillCoroutines(handle);
            _healCoroutines.Clear();
            _activeKitSerial.Clear();
        }

        private IEnumerator<float> HealCoroutine(Player player, MedkitMenuState menuState, PlayerMedicalState medState)
        {
            var item = menuState.HealingItem;
            for (int stage = 0; stage < item.Stages.Count; stage++)
            {
                menuState.CurrentStage = stage;
                menuState.HealElapsed = 0;
                var stageData = item.Stages[stage];
                float duration = stageData.Duration;
                while (menuState.HealElapsed < duration)
                {
                    yield return Timing.WaitForSeconds(0.5f);
                    menuState.HealElapsed += 0.5f;
                    if (!player.IsAlive) { CancelHealing(player.UserId); yield break; }
                    if (player.CurrentItem == null || player.CurrentItem.Type != ItemType.Medkit)
                    {
                        CancelHealing(player.UserId); yield break;
                    }
                }
                if (item.GlobalTarget == GlobalCondition.BleedingHeavy && stage == 0)
                {
                    Plugin.Instance.InjuryTicks?.ResetPlayer(player.UserId);
                }
            }

            // Реген только при полном лечении
            if (!item.IsFirstAid)
            {
                RegenTier regenTier = RegenTier.Light;
                if (item.TotalTime >= 20f) regenTier = RegenTier.Heavy;
                else if (item.TotalTime >= 10f) regenTier = RegenTier.Medium;
                RegenStorage.Start(player.UserId, regenTier);
            }

            // Списываем расходники
            string userId = player.UserId;
            if (_activeKitSerial.TryGetValue(userId, out ushort serial))
            {
                if (MedkitInventoryStorage.TryGet(serial, out var usedKit))
                {
                    if (item.IsFirstAid)
                        usedKit.ConsumeFirstAid();
                    else
                        usedKit.Consume(item.GlobalTarget, item.LocalTarget);
                }
            }

            ApplyHealResult(player, medState, item);
            menuState.Reset();
            _healCoroutines.Remove(player.UserId);
            _activeKitSerial.Remove(player.UserId);
        }

        private void ApplyHealResult(Player player, PlayerMedicalState medState, MedkitMenuItem item)
        {
            var config = Plugin.Instance.Config;

            if (item.IsFirstAid)
            {
                if (medState.TryApplyFirstAid(out var originalBleed))
                {
                    Plugin.Instance.InjuryTicks?.ResetPlayer(player.UserId);
                    HudNoticeService.Show(player, $"<color=yellow>Первая помощь! Кровотечение ослаблено на 60 сек.</color>", 3f);
                }
                return;
            }

            if (item.GlobalTarget.HasValue)
            {
                medState.RemoveCondition(item.GlobalTarget.Value);
                switch (item.GlobalTarget.Value)
                {
                    case GlobalCondition.BleedingLight:
                    case GlobalCondition.BleedingMedium:
                    case GlobalCondition.BleedingHeavy:
                        Plugin.Instance.InjuryTicks?.ResetPlayer(player.UserId);
                        player.DisableEffect<Concussed>();
                        player.DisableEffect<Deafened>();
                        player.DisableEffect<Blindness>();
                        break;
                    case GlobalCondition.AdrenalineOverdose:
                    case GlobalCondition.PainkillerOverdose:
                        player.DisableEffect<Blurred>();
                        player.DisableEffect<Slowness>();
                        player.DisableEffect<Asphyxiated>();
                        break;
                }
            }
            if (item.LocalTarget.HasValue && item.BodyPartTarget.HasValue)
            {
                medState.RemoveInjury(item.LocalTarget.Value, item.BodyPartTarget.Value);
                switch (item.LocalTarget.Value)
                {
                    case LocalInjuryType.Bruise:
                        if (item.BodyPartTarget == BodyPart.LeftLeg || item.BodyPartTarget == BodyPart.RightLeg)
                        {
                            player.EnableEffect<Slowness>(900f);
                            player.ChangeEffectIntensity<Slowness>(25);
                        }
                        break;
                    case LocalInjuryType.Fracture:
                        player.DisableEffect<Concussed>();
                        player.EnableEffect<Slowness>(36000f);
                        player.ChangeEffectIntensity<Slowness>(config.FractureSlownessIntensity);
                        break;
                    case LocalInjuryType.Gunshot:
                    case LocalInjuryType.Stab:
                        player.DisableEffect<Concussed>();
                        player.DisableEffect<Blindness>();
                        break;
                    case LocalInjuryType.Chemical:
                        player.DisableEffect<Concussed>();
                        break;
                    case LocalInjuryType.Burn:
                        player.DisableEffect<Burned>();
                        break;
                }
            }
        }
    }
}
