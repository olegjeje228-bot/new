using System;
using System.Collections.Generic;
using System.Linq;
using EventHUD.Hud;
using EventHUD.Medicine;
using EventHUD.Rpm;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using UnityEngine;
using UserSettings.ServerSpecific;

namespace EventHUD.Scp
{
    public class Scp106Handler
    {
        private readonly Config _config;

        private const int PocketKeybindId = 9020;
        private static bool _registered;

        private readonly Dictionary<string, DateTime> _lastTouchTime =
            new Dictionary<string, DateTime>();

        private readonly Dictionary<string, int> _touchCount =
            new Dictionary<string, int>();

        private readonly Dictionary<string, DateTime> _pocketCooldown =
            new Dictionary<string, DateTime>();

        public Scp106Handler(Config config)
        {
            _config = config;
        }

        public void RegisterSss()
        {
            if (_registered)
                return;

            _registered = true;

            SssRoleSync.Scp106Settings.Add(
                new SSKeybindSetting(
                    PocketKeybindId,
                    "Телепорт в карман (SCP-106)",
                    KeyCode.Alpha1));

            ServerSpecificSettingsSync.ServerOnSettingValueReceived +=
                OnSettingChanged;
        }

        public void UnregisterSss()
        {
            if (!_registered)
                return;

            _registered = false;

            ServerSpecificSettingsSync.ServerOnSettingValueReceived -=
                OnSettingChanged;
        }

        private void OnSettingChanged(
            ReferenceHub hub,
            ServerSpecificSettingBase setting)
        {
            if (setting.SettingId != PocketKeybindId)
                return;

            if (!RpModuleManager.Instance.IsEnabled(
                    RpModuleType.Medicine))
            {
                return;
            }

            if (!(setting is SSKeybindSetting keybind) ||
                !keybind.SyncIsPressed)
            {
                return;
            }

            Player player = Player.Get(hub);

            if (player == null ||
                !player.IsAlive ||
                player.Role.Type != RoleTypeId.Scp106)
            {
                return;
            }

            if (player.CurrentRoom != null &&
                player.CurrentRoom.Type == RoomType.Pocket)
            {
                HudNoticeService.Show(
                    player,
                    "<color=red>Вы уже в карманном измерении</color>",
                    1f);

                return;
            }

            DateTime now = DateTime.UtcNow;

            if (_pocketCooldown.TryGetValue(
                    player.UserId,
                    out DateTime lastUse) &&
                (now - lastUse).TotalSeconds < 5.0)
            {
                HudNoticeService.Show(
                    player,
                    "<color=red>КД 5 секунд</color>",
                    1f);

                return;
            }

            Room pocket = Room.Get(RoomType.Pocket);
            if (pocket == null)
            {
                Log.Error(
                    "[SCP-106] Комната Pocket не найдена.");

                return;
            }

            _pocketCooldown[player.UserId] = now;

            HudNoticeService.Show(
                player,
                "<color=#00FF00>Телепортация в карман...</color>",
                2f);

            if (player.Role is Exiled.API.Features.Roles.Scp106Role role)
            {
                role.Vigor = 1f;
                role.IsSubmerged = true;
            }

            Vector3 destination =
                pocket.Position + Vector3.up;

            Timing.CallDelayed(
                2f,
                () =>
                {
                    if (player == null ||
                        !player.IsAlive ||
                        player.Role.Type != RoleTypeId.Scp106)
                    {
                        return;
                    }

                    player.Position = destination;

                    if (player.Role is
                        Exiled.API.Features.Roles.Scp106Role currentRole)
                    {
                        currentRole.IsSubmerged = false;
                    }

                    HudNoticeService.Show(
                        player,
                        "<color=#00FF00>Вы в карманном измерении</color>",
                        2f);
                });
        }

        public void OnHurting(HurtingEventArgs ev)
        {
            if (ev.Player == null)
                return;

            // SCP-106 не получает урон кармана и неуязвим,
            // пока физически находится внутри Pocket.
            if (ev.Player.Role.Type == RoleTypeId.Scp106 &&
                (ev.DamageHandler.Type == DamageType.PocketDimension ||
                 (ev.Player.CurrentRoom != null &&
                  ev.Player.CurrentRoom.Type == RoomType.Pocket)))
            {
                ev.IsAllowed = false;
                return;
            }

            // Обычный урон кармана людям здесь не меняем и не лечим.
            if (ev.DamageHandler.Type == DamageType.PocketDimension)
                return;

            if (ev.DamageHandler.Type != DamageType.Scp106)
                return;

            if (!ev.Player.IsAlive ||
                ev.Player.Role.Team == Team.SCPs)
            {
                return;
            }

            string userId = ev.Player.UserId;
            DateTime now = DateTime.UtcNow;

            if (_lastTouchTime.TryGetValue(
                    userId,
                    out DateTime lastTouch) &&
                (now - lastTouch).TotalSeconds > 5.0)
            {
                _touchCount[userId] = 0;
            }

            _lastTouchTime[userId] = now;

            if (!_touchCount.ContainsKey(userId))
                _touchCount[userId] = 0;

            _touchCount[userId]++;

            if (_touchCount[userId] == 1)
            {
                ev.Amount = 0f;

                InjuryApplier.ApplyCorrosion(ev.Player);

                HudNoticeService.Show(
                    ev.Player,
                    "<color=red>Коррозия! Второе касание " +
                    "телепортирует в карман.</color>",
                    3f);

                return;
            }

            ev.Amount = 0f;
            _touchCount[userId] = 0;

            Player victim = ev.Player;

            Room pocket = Room.Get(RoomType.Pocket);
            if (pocket == null)
            {
                Log.Error(
                    "[SCP-106] Невозможно телепортировать жертву: " +
                    "комната Pocket не найдена.");

                return;
            }

            // Лечение происходит один раз — только на втором касании.
            victim.Health = Math.Min(
                victim.Health + 100f,
                victim.MaxHealth);

            Vector3 destination =
                pocket.Position + Vector3.up;

            Timing.CallDelayed(
                0.5f,
                () =>
                {
                    if (victim == null || !victim.IsAlive)
                        return;

                    victim.Position = destination;

                    HudNoticeService.Show(
                        victim,
                        "<color=#FF0000>Вы телепортированы " +
                        "в карманное измерение!</color>",
                        3f);
                });
        }

        public void OnEscapingPocket(
            EscapingPocketDimensionEventArgs ev)
        {
            if (ev.Player == null ||
                ev.Player.Role.Type != RoleTypeId.Scp106)
            {
                return;
            }

            ev.IsAllowed = true;
            ClearPocketEffects(ev.Player);
        }

        public void OnFailingEscapePocket(
            FailingEscapePocketDimensionEventArgs ev)
        {
            if (ev.Player == null ||
                ev.Player.Role.Type != RoleTypeId.Scp106)
            {
                return;
            }

            ev.IsAllowed = false;

            Player player = ev.Player;

            List<Room> rooms = Room.List
                .Where(room =>
                    room != null &&
                    room.Type != RoomType.Pocket &&
                    room.Type != RoomType.Surface)
                .ToList();

            if (rooms.Count == 0)
            {
                Log.Error(
                    "[SCP-106] Нет безопасной комнаты для выхода из Pocket.");

                return;
            }

            Room targetRoom =
                rooms[UnityEngine.Random.Range(0, rooms.Count)];

            Vector3 destination =
                targetRoom.Position + Vector3.up;

            Timing.CallDelayed(
                0.1f,
                () =>
                {
                    if (player == null || !player.IsAlive)
                        return;

                    player.Position = destination;
                    ClearPocketEffects(player);

                    HudNoticeService.Show(
                        player,
                        "<color=#00FF00>Вы вышли из кармана</color>",
                        2f);
                });
        }

        private static void ClearPocketEffects(Player player)
        {
            Timing.CallDelayed(
                0.2f,
                () =>
                {
                    if (player == null || !player.IsAlive)
                        return;

                    try
                    {
                        player.DisableEffect<
                            CustomPlayerEffects.PocketCorroding>();

                        player.DisableEffect<
                            CustomPlayerEffects.Corroding>();

                        player.DisableEffect<
                            CustomPlayerEffects.Traumatized>();
                    }
                    catch (Exception exception)
                    {
                        Log.Debug(
                            $"[SCP-106] Не удалось снять эффекты Pocket: " +
                            $"{exception.Message}");
                    }
                });
        }

        public void ClearPlayer(string userId)
        {
            _lastTouchTime.Remove(userId);
            _touchCount.Remove(userId);
            _pocketCooldown.Remove(userId);
        }

        public void ClearAll()
        {
            _lastTouchTime.Clear();
            _touchCount.Clear();
            _pocketCooldown.Clear();
        }
    }
}