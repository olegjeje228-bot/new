using System;
using System.Collections.Generic;
using System.Linq;
using EventHUD.Scp;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using UnityEngine;
using UserSettings.ServerSpecific;

namespace EventHUD.Hud
{
    public static class SssRoleSync
    {
        public static readonly List<ServerSpecificSettingBase>
            HumanSettings =
                new List<ServerSpecificSettingBase>();

        public static readonly List<ServerSpecificSettingBase>
            Scp049Settings =
                new List<ServerSpecificSettingBase>();

        public static readonly List<ServerSpecificSettingBase>
            Scp106Settings =
                new List<ServerSpecificSettingBase>();

        public static readonly List<ServerSpecificSettingBase>
            ScpSettings =
                new List<ServerSpecificSettingBase>();

        private const int ScpProximityKeybindId = 4901;

        private static bool _initialized;

        public static void Init()
        {
            if (_initialized)
                return;

            _initialized = true;

            ScpSettings.Add(
                new SSKeybindSetting(
                    ScpProximityKeybindId,
                    "Разговор за SCP",
                    KeyCode.Z));

            ServerSpecificSettingsSync.ServerOnSettingValueReceived +=
                OnSettingValueReceived;

            List<ServerSpecificSettingBase> all =
                new List<ServerSpecificSettingBase>();

            AddUnique(all, HumanSettings);
            AddUnique(all, Scp049Settings);
            AddUnique(all, Scp106Settings);
            AddUnique(all, ScpSettings);

            ServerSpecificSettingsSync.DefinedSettings =
                all.ToArray();

            ServerSpecificSettingsSync.SendOnJoinFilter =
                hub => false;
        }

        public static void Shutdown()
        {
            if (!_initialized)
                return;

            _initialized = false;

            ServerSpecificSettingsSync.ServerOnSettingValueReceived -=
                OnSettingValueReceived;

            ServerSpecificSettingsSync.SendOnJoinFilter = null;

            HumanSettings.Clear();
            Scp049Settings.Clear();
            Scp106Settings.Clear();
            ScpSettings.Clear();

            ScpProximityChat.Clear();
        }

        public static void SyncPlayer(Player player)
        {
            if (player == null ||
                !player.IsConnected ||
                player.ReferenceHub == null)
            {
                return;
            }

            ServerSpecificSettingBase[] settings;

            switch (player.Role.Type)
            {
                case RoleTypeId.Scp049:
                    settings = Scp049Settings
                        .Concat(ScpSettings)
                        .ToArray();
                    break;

                case RoleTypeId.Scp106:
                    settings = Scp106Settings
                        .Concat(ScpSettings)
                        .ToArray();
                    break;

                default:
                    if (player.Role.Team == Team.SCPs)
                    {
                        settings = ScpSettings.ToArray();
                    }
                    else
                    {
                        ScpProximityChat.Disable(player);
                        settings = HumanSettings.ToArray();
                    }

                    break;
            }

            ServerSpecificSettingsSync.SendToPlayer(
                player.ReferenceHub,
                settings);
        }

        public static void OnChangingRole(
            Exiled.Events.EventArgs.Player.ChangingRoleEventArgs ev)
        {
            Player player = ev.Player;

            Timing.CallDelayed(
                0.6f,
                () => SyncPlayer(player));
        }

        public static void OnVerified(
            Exiled.Events.EventArgs.Player.VerifiedEventArgs ev)
        {
            Player player = ev.Player;

            Timing.CallDelayed(
                1f,
                () => SyncPlayer(player));
        }

        private static void OnSettingValueReceived(
            ReferenceHub hub,
            ServerSpecificSettingBase setting)
        {
            if (!(setting is SSKeybindSetting keybind) ||
                keybind.SettingId != ScpProximityKeybindId ||
                !keybind.SyncIsPressed)
            {
                return;
            }

            Player player = Player.Get(hub);

            if (player == null ||
                !player.IsAlive ||
                !player.IsScp)
            {
                return;
            }

            bool enabled =
                ScpProximityChat.Toggle(player);

            HudNoticeService.Show(
                player,
                enabled
                    ? "<color=#4CAF50>Прокси-чат SCP: ВКЛ</color>"
                    : "<color=#F44336>Прокси-чат SCP: ВЫКЛ</color>",
                2f);
        }

        private static void AddUnique(
            List<ServerSpecificSettingBase> target,
            IEnumerable<ServerSpecificSettingBase> source)
        {
            foreach (ServerSpecificSettingBase setting in source)
            {
                if (!target.Contains(setting))
                    target.Add(setting);
            }
        }
    }
}