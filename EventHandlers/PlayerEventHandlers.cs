using Exiled.Events.EventArgs.Player;
using EventHUD.Commands;
using EventHUD.Hud;
using EventHUD.Medicine;
using LabApi.Features.Enums;

namespace EventHUD.EventHandlers
{
    public class PlayerEventHandlers
    {
        public void OnLeft(LeftEventArgs ev)
        {
            if (ev.Player == null) return;
            string uid = ev.Player.UserId;
            HudToggleService.Clear(uid);
            EventManager.Instance.OnPlayerLeft(ev.Player);

            // Чистим медицинские стейты — профилактика утечек памяти
            MedicalStorage.Remove(uid);
            MedkitStorage.Remove(uid);
            ArmorStorage.Remove(uid);
            RegenStorage.Stop(uid);
            // ArmorRemovalStorage может отсутствовать в старых сборках — закомментируй если ошибка
            ArmorRemovalStorage.Remove(uid);
            Plugin.Instance?.InjuryTicks?.ResetPlayer(uid);
            Plugin.Instance?.MedkitHeals?.CancelHealing(uid);
            Plugin.Instance?.CritState?.Cancel(uid);
            Plugin.Instance?.AntiAdmCommands?.CleanupPlayer(uid);
        }

        public void OnVerified(VerifiedEventArgs ev)
        {
            if (ev.Player == null) return;
            HudToggleService.Clear(ev.Player.UserId);
            EventManager.Instance.OnPlayerJoined(ev.Player);
        }

        public void OnSendingValidCommand(SendingValidCommandEventArgs ev)
        {
            if (ev.Type == CommandType.RemoteAdmin)
                EventManager.Instance.NotifyRaActivity();
        }

        public void OnEscaping(EscapingEventArgs ev)
        {
            if (!EscapeCommand.IsEscapeEnabled)
                ev.IsAllowed = false;
        }

        public void OnTriggeringTesla(TriggeringTeslaEventArgs ev)
        {
            if (!TeslaCommand.IsTeslaEnabled)
                ev.IsAllowed = false;
        }
    }
}
