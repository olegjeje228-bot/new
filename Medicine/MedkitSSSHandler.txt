using System;
using System.Linq;
using EventHUD.Rpm;
using Exiled.API.Features;
using UserSettings.ServerSpecific;

namespace EventHUD.Medicine
{
    public static class MedkitSSSHandler
    {
        private static bool _registered;
        private static int _leftId  = 9010;
        private static int _rightId = 9011;
        private static int _enterId = 9012;

        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            var left  = new SSKeybindSetting(_leftId,  "← Назад",           UnityEngine.KeyCode.LeftArrow);
            var right = new SSKeybindSetting(_rightId, "→ Далее",           UnityEngine.KeyCode.RightArrow);
            var enter = new SSKeybindSetting(_enterId, "Enter Подтвердить", UnityEngine.KeyCode.Return);

            EventHUD.Hud.SssRoleSync.HumanSettings.Add(left);
            EventHUD.Hud.SssRoleSync.HumanSettings.Add(right);
            EventHUD.Hud.SssRoleSync.HumanSettings.Add(enter);
            EventHUD.Hud.SssRoleSync.Scp049Settings.Add(left);
            EventHUD.Hud.SssRoleSync.Scp049Settings.Add(right);
            EventHUD.Hud.SssRoleSync.Scp049Settings.Add(enter);

            ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnSettingChanged;
        }

        public static void Unregister()
        {
            if (!_registered) return;
            _registered = false;
            ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnSettingChanged;
        }

        private static bool IsSelectable(MedkitMenuItem it) => it.CanHeal || it.IsInventoryView;

        private static void EnsureValidSelection(MedkitMenuState menuState, System.Collections.Generic.List<MedkitMenuItem> items)
        {
            if (items.Count == 0) return;
            if (menuState.SelectedIndex < 0 || menuState.SelectedIndex >= items.Count || !IsSelectable(items[menuState.SelectedIndex]))
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (IsSelectable(items[i])) { menuState.SelectedIndex = i; return; }
                }
                menuState.SelectedIndex = 0;
            }
        }

        private static void Navigate(MedkitMenuState menuState, System.Collections.Generic.List<MedkitMenuItem> items, int direction)
        {
            if (items.Count == 0) return;
            for (int attempt = 0; attempt < items.Count; attempt++)
            {
                menuState.SelectedIndex += direction;
                if (menuState.SelectedIndex < 0) menuState.SelectedIndex = items.Count - 1;
                if (menuState.SelectedIndex >= items.Count) menuState.SelectedIndex = 0;
                if (IsSelectable(items[menuState.SelectedIndex])) break;
            }
        }

        private static void OnSettingChanged(ReferenceHub hub, ServerSpecificSettingBase setting)
        {
            if (setting.SettingId != _leftId && setting.SettingId != _rightId && setting.SettingId != _enterId)
                return;
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine))
                return;
            if (setting is not SSKeybindSetting keybind || !keybind.SyncIsPressed)
                return;
            var player = Player.Get(hub);
            if (player == null || !player.IsAlive) return;
            if (player.Role.Team == PlayerRoles.Team.SCPs) return; // у SCP-049 своё меню

            // Самолечение — аптечка должна быть в руках
            if (player.CurrentItem == null || player.CurrentItem.Type != ItemType.Medkit)
                return;

            var kit = MedkitInventoryStorage.GetOrCreate(player.CurrentItem.Serial);
            if (kit == null) return;

            var medState = MedicalStorage.GetOrCreate(player.UserId);
            var menuState = MedkitStorage.GetOrCreate(player.UserId);
            var items = MedkitMenuBuilder.Build(medState, Plugin.Instance.Config, kit, false);

            if (setting.SettingId == _enterId)
            {
                if (menuState.IsHealing) return;

                EnsureValidSelection(menuState, items);

                if (menuState.ShowInventory)
                {
                    menuState.ShowInventory = false;
                    menuState.SelectedIndex = 0;
                    return;
                }

                if (menuState.SelectedIndex < 0 || menuState.SelectedIndex >= items.Count) return;
                var selected = items[menuState.SelectedIndex];

                if (selected.IsInventoryView)
                {
                    menuState.ShowInventory = true;
                    return;
                }

                // Запуск лечения
                Plugin.Instance.MedkitHeals?.StartHealing(player);
                return;
            }

            // Навигация Left/Right — только если не лечим
            if (menuState.IsHealing) return;

            EnsureValidSelection(menuState, items);

            int direction = 0;
            if (setting.SettingId == _leftId) direction = -1;
            else if (setting.SettingId == _rightId) direction = 1;

            if (direction != 0)
            {
                Navigate(menuState, items, direction);
            }
        }
    }
}
