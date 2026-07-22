using Exiled.API.Features;
using UserSettings.ServerSpecific;

namespace EventHUD.Rpm
{
    public static class FullRpSss
    {
        public const int ConfirmButtonId = 9020;

        private static SSButton _confirmButton;
        private static bool _registered;

        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            _confirmButton = new SSButton(ConfirmButtonId, "Подтверждение РП-биндов", "Я всё подтвердил");
            ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnSettingReceived;
            Refresh();
        }

        public static void Unregister()
        {
            if (!_registered) return;
            _registered = false;
            ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnSettingReceived;
        }

        public static void Refresh()
        {
            if (_confirmButton == null) return;

            var human = EventHUD.Hud.SssRoleSync.HumanSettings;
            var scp049 = EventHUD.Hud.SssRoleSync.Scp049Settings;

            if (FullRpState.IsEnabled)
            {
                if (!human.Contains(_confirmButton)) human.Add(_confirmButton);
                if (!scp049.Contains(_confirmButton)) scp049.Add(_confirmButton);
            }
            else
            {
                human.Remove(_confirmButton);
                scp049.Remove(_confirmButton);
            }

            EventHUD.Hud.SssRoleSync.RebuildDefinedSettings();

            foreach (var player in Player.List)
                EventHUD.Hud.SssRoleSync.SyncPlayer(player);
        }

        private static void OnSettingReceived(ReferenceHub hub, ServerSpecificSettingBase setting)
        {
            if (setting.SettingId != ConfirmButtonId || !(setting is SSButton))
                return;
            if (!FullRpState.IsEnabled)
                return;

            var player = Player.Get(hub);
            if (player == null || FullRpState.IsConfirmed(player.UserId))
                return;

            FullRpState.Confirm(player.UserId);
            EventHUD.Hud.HudNoticeService.Show(player, "<color=#4CAF50>Спасибо! Бинды подтверждены</color>", 3f);
        }
    }
}