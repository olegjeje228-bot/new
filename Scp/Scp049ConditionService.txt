using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using PlayerStatsSystem;

namespace EventHUD.Scp
{
    public enum Doc049Condition
    {
        None,
        Concussion,
        Burn,
        Wound
    }

    public static class Scp049ConditionService
    {
        private sealed class DocState
        {
            public Doc049Condition Condition =
                Doc049Condition.None;

            public float WindowDamage;

            public DateTime WindowStart =
                DateTime.MinValue;

            public DateTime LastSlowness =
                DateTime.MinValue;

            public float HealProgress;

            public float RegenLeft;
        }

        private static readonly Dictionary<string, DocState> States =
            new Dictionary<string, DocState>();

        private const float WoundDamageThreshold = 400f;
        private const float WoundWindowSeconds = 5f;

        private static bool _registered;

        public static void Register()
        {
            if (_registered)
                return;

            _registered = true;

            Exiled.Events.Handlers.Player.Hurting += OnHurting;
            Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        }

        public static void Unregister()
        {
            if (!_registered)
                return;

            _registered = false;

            Exiled.Events.Handlers.Player.Hurting -= OnHurting;
            Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;

            States.Clear();
        }

        private static void OnRoundStarted()
        {
            States.Clear();
        }

        private static void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (ev.Player != null)
                States.Remove(ev.Player.UserId);
        }

        private static DocState Get(Player player)
        {
            if (!States.TryGetValue(
                    player.UserId,
                    out DocState state))
            {
                state = new DocState();
                States[player.UserId] = state;
            }

            return state;
        }

        private static void OnHurting(HurtingEventArgs ev)
        {
            if (ev.Player == null ||
                ev.Player.Role.Type != RoleTypeId.Scp049)
            {
                return;
            }

            DocState state = Get(ev.Player);

            if (ev.DamageHandler.Type == DamageType.Explosion)
            {
                state.Condition = Doc049Condition.Concussion;
                state.HealProgress = 0f;

                ev.Player.EnableEffect(
                    EffectType.Concussed,
                    8f);

                return;
            }

            if (ev.DamageHandler.Type == DamageType.MicroHid ||
                ev.DamageHandler.Type == DamageType.Tesla)
            {
                state.Condition = Doc049Condition.Burn;
                state.HealProgress = 0f;
                return;
            }

            if (!(ev.DamageHandler.Base is FirearmDamageHandler))
                return;

            DateTime now = DateTime.UtcNow;

            if ((now - state.LastSlowness).TotalSeconds >= 1.0)
            {
                state.LastSlowness = now;

                ev.Player.ChangeEffectIntensity(
                    EffectType.Slowness,
                    25,
                    1f);
            }

            if ((now - state.WindowStart).TotalSeconds >
                WoundWindowSeconds)
            {
                state.WindowStart = now;
                state.WindowDamage = 0f;
            }

            state.WindowDamage += ev.Amount;

            if (state.WindowDamage >= WoundDamageThreshold &&
                state.Condition == Doc049Condition.None)
            {
                state.Condition = Doc049Condition.Wound;
                state.HealProgress = 0f;
            }
        }

        public static void Tick(Player player, float deltaTime)
        {
            if (player == null ||
                player.Role.Type != RoleTypeId.Scp049)
            {
                return;
            }

            DocState state = Get(player);

            if (state.RegenLeft > 0f)
            {
                float regeneration =
                    Math.Min(
                        state.RegenLeft,
                        100f / 120f * deltaTime);

                player.Health = Math.Min(
                    player.MaxHealth,
                    player.Health + regeneration);

                state.RegenLeft -= regeneration;
            }

            if (state.Condition == Doc049Condition.None)
                return;

            switch (state.Condition)
            {
                case Doc049Condition.Burn:
                    player.ChangeEffectIntensity(
                        EffectType.Slowness,
                        35,
                        deltaTime + 0.5f);

                    player.EnableEffect(
                        EffectType.Invigorated,
                        deltaTime + 0.5f);
                    break;

                case Doc049Condition.Wound:
                    player.ChangeEffectIntensity(
                        EffectType.Slowness,
                        25,
                        deltaTime + 0.5f);

                    if (player.Role is Scp049Role role &&
                        role.IsCallActive)
                    {
                        role.RemainingCallDuration = 0.1f;
                    }

                    break;
            }

            if (player.CurrentItem?.Type != ItemType.Medkit)
            {
                state.HealProgress = 0f;
                return;
            }

            state.HealProgress += deltaTime;

            if (state.HealProgress <
                GetHealDuration(state.Condition))
            {
                return;
            }

            if (state.Condition == Doc049Condition.Concussion)
            {
                player.DisableEffect(EffectType.Concussed);
                state.RegenLeft = 100f;
            }

            player.DisableEffect(EffectType.Slowness);
            player.DisableEffect(EffectType.Invigorated);

            state.Condition = Doc049Condition.None;
            state.HealProgress = 0f;
            state.WindowDamage = 0f;
            state.WindowStart = DateTime.MinValue;
        }

        public static bool IsAbilityBlocked(Player player)
        {
            if (player == null)
                return false;

            return States.TryGetValue(
                       player.UserId,
                       out DocState state) &&
                   state.Condition == Doc049Condition.Wound;
        }

        public static string GetHudLine(Player player)
        {
            if (player == null ||
                !States.TryGetValue(
                    player.UserId,
                    out DocState state) ||
                state.Condition == Doc049Condition.None)
            {
                return string.Empty;
            }

            string name;
            string color;

            switch (state.Condition)
            {
                case Doc049Condition.Concussion:
                    name = "Контузия";
                    color = "#FF9800";
                    break;

                case Doc049Condition.Burn:
                    name = "Ожог";
                    color = "#FF5722";
                    break;

                default:
                    name = "Ранение";
                    color = "#F44336";
                    break;
            }

            float healDuration =
                GetHealDuration(state.Condition);

            int percentage =
                (int)(state.HealProgress / healDuration * 100f);

            if (percentage < 0)
                percentage = 0;

            if (percentage > 100)
                percentage = 100;

            string healing = state.HealProgress > 0f
                ? $" | Лечение: {percentage}%"
                : " | Возьми аптечку в руки";

            string blocked =
                state.Condition == Doc049Condition.Wound
                    ? " | Способность F заблокирована"
                    : string.Empty;

            return
                $"<color={color}>{name}</color>" +
                healing +
                blocked;
        }

        private static float GetHealDuration(
            Doc049Condition condition)
        {
            return condition switch
            {
                Doc049Condition.Concussion => 10f,
                Doc049Condition.Burn => 15f,
                _ => 20f
            };
        }
    }
}