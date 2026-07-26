using System;
using System.Collections.Generic;
using CustomPlayerEffects;
using EventHUD.Hud;
using Exiled.API.Features;
using MEC;

namespace EventHUD.Medicine
{
    public class InjuryTickService
    {
        private CoroutineHandle _tickHandle;

        private readonly Dictionary<string, DateTime> _lastPassiveTick =
            new Dictionary<string, DateTime>();

        public void Start()
        {
            _tickHandle = Timing.RunCoroutine(TickLoop());
        }

        public void Stop()
        {
            Timing.KillCoroutines(_tickHandle);
            _lastPassiveTick.Clear();
        }

        private IEnumerator<float> TickLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(
                    Plugin.Instance.Config.InjuryTickInterval);

                if (!Rpm.RpModuleManager.Instance.IsEnabled(
                        Rpm.RpModuleType.Medicine))
                {
                    continue;
                }

                foreach (Player player in Player.List)
                {
                    if (player == null || !player.IsAlive)
                        continue;

                    SyncScpItemConditions(player);

                    if (!MedicalStorage.TryGet(
                            player.UserId,
                            out PlayerMedicalState state))
                    {
                        continue;
                    }

                    if (state.FirstAidUsed &&
                        state.FirstAidOriginalBleeding.HasValue &&
                        state.CheckFirstAidExpiry())
                    {
                        ResetPlayer(player.UserId);

                        HudNoticeService.Show(
                            player,
                            "<color=red>Временная повязка слетела! " +
                            "Кровотечение возобновилось.</color>",
                            3f);
                    }

                    GlobalCondition? bleeding = state.GetBleedingLevel();
                    if (!bleeding.HasValue)
                        continue;

                    ProcessBleedingTick(player, state, bleeding.Value);
                }
            }
        }

        /// <summary>
        /// Синхронизирует медицинские состояния с фактически
        /// активными эффектами SCP-1853 и Anti-SCP-207.
        /// </summary>
        private static void SyncScpItemConditions(Player player)
        {
            bool has1853 = player.IsEffectActive<Scp1853>();
            bool hasAnti207 = player.IsEffectActive<AntiScp207>();

            // Не создаём пустое медицинское состояние без необходимости.
            if (!has1853 &&
                !hasAnti207 &&
                !MedicalStorage.TryGet(player.UserId, out _))
            {
                return;
            }

            PlayerMedicalState state =
                MedicalStorage.GetOrCreate(player.UserId);

            if (has1853 && hasAnti207)
            {
                state.RemoveCondition(GlobalCondition.Under1853);
                state.AddCondition(GlobalCondition.Poisoned);
                return;
            }

            state.RemoveCondition(GlobalCondition.Poisoned);

            if (has1853)
                state.AddCondition(GlobalCondition.Under1853);
            else
                state.RemoveCondition(GlobalCondition.Under1853);
        }

        private void ProcessBleedingTick(
            Player player,
            PlayerMedicalState state,
            GlobalCondition bleedLevel)
        {
            Config config = Plugin.Instance.Config;
            DateTime now = DateTime.UtcNow;

            float elapsed =
                (float)(now - state.BleedingStartedAt).TotalSeconds;

            float burstDps;
            float burstDuration;
            float passiveDps;
            float passiveInterval;

            switch (bleedLevel)
            {
                case GlobalCondition.BleedingLight:
                    burstDps = config.LightBleedBurstDps;
                    burstDuration = config.LightBleedBurstDuration;
                    passiveDps = config.LightBleedPassiveDps;
                    passiveInterval = config.LightBleedPassiveInterval;
                    break;

                case GlobalCondition.BleedingMedium:
                    burstDps = config.MediumBleedBurstDps;
                    burstDuration = config.MediumBleedBurstDuration;
                    passiveDps = config.MediumBleedPassiveDps;
                    passiveInterval = config.MediumBleedPassiveInterval;
                    break;

                case GlobalCondition.BleedingHeavy:
                    burstDps = config.HeavyBleedBurstDps;
                    burstDuration = config.HeavyBleedBurstDuration;
                    passiveDps = config.HeavyBleedPassiveDps;
                    passiveInterval = config.HeavyBleedPassiveInterval;
                    break;

                default:
                    return;
            }

            float tickInterval = config.InjuryTickInterval;

            if (!state.BleedingBurstFinished)
            {
                if (elapsed < burstDuration)
                {
                    player.Hurt(
                        burstDps * tickInterval,
                        "Кровотечение");
                }
                else
                {
                    state.BleedingBurstFinished = true;
                    _lastPassiveTick[player.UserId] = now;
                }

                return;
            }

            if (!_lastPassiveTick.TryGetValue(
                    player.UserId,
                    out DateTime lastTick))
            {
                _lastPassiveTick[player.UserId] = now;
                return;
            }

            if ((now - lastTick).TotalSeconds < passiveInterval)
                return;

            player.Hurt(passiveDps, "Кровотечение");
            _lastPassiveTick[player.UserId] = now;
        }

        public void ResetPlayer(string userId)
        {
            _lastPassiveTick.Remove(userId);
        }
    }
}