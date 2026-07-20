using System;
using System.Collections.Generic;
using CommandSystem;
using EventHUD.Enums;
using EventHUD.Extensions;
using EventHUD.Models;
using EventHUD.Rpm;
using Exiled.API.Features;
using MEC;

namespace EventHUD
{
    public class EventManager
    {
        public static EventManager Instance { get; } = new EventManager();

        public EventSession Session        { get; } = new EventSession();
        public DateTime     LastRaActivity { get; private set; } = DateTime.UtcNow;

        private CoroutineHandle _transitionHandle;
        private CoroutineHandle _stopHandle;

        public void Prepare(string eventName, RPType rpType, ICommandSender sender)
        {
            Player player = Player.Get(sender);

            Timing.KillCoroutines(_transitionHandle, _stopHandle);

            Session.Reset();

            Session.State        = EventState.Preparing;
            Session.EventName    = eventName;
            Session.RpType       = rpType;
            Session.HostUserId   = player?.UserId;
            Session.HostNickname = player?.Nickname ?? "Консоль";
            Session.StartedAt    = DateTime.UtcNow;

            RpModuleManager.Instance.OnEventRpChanged(rpType);
            NotifyRaActivity();

            ServerNameService.SetStatus($"Подготовка ивента: {Session.EventName}", Session.RpType.GetColor());

            DiscordBotService.SendPrepare(eventName, rpType, Session.HostNickname);
            DiscordWebhookService.SendPrepareEmbed(Session);
        }

        public bool Start(Player invoker, RPType? rpType, string eventName, out string message)
        {
            if (Session.State == EventState.None)
            {
                message = "Сначала используйте ev prepare!";
                return false;
            }

            if (rpType.HasValue)
                Session.RpType = rpType.Value;

            if (!string.IsNullOrWhiteSpace(eventName))
                Session.EventName = eventName;

            if (invoker != null)
            {
                Session.HostUserId   = invoker.UserId;
                Session.HostNickname = invoker.Nickname;
                Session.HostIsOnline = true;
            }

            // Время подготовки считаем до обновления Session.StartedAt
            TimeSpan prepTime = Session.StartedAt == default
                ? TimeSpan.Zero
                : DateTime.UtcNow - Session.StartedAt;

            Session.State     = EventState.Starting;
            Session.StartedAt = DateTime.UtcNow;

            if (rpType.HasValue)
                RpModuleManager.Instance.OnEventRpChanged(rpType.Value);

            NotifyRaActivity();

            Timing.KillCoroutines(_transitionHandle);
            _transitionHandle = Timing.RunCoroutine(TransitionToRunning());

            message = $"Ивент запущен: {Session.EventName} [{Session.RpType.GetShortName()}]";

            ServerNameService.SetStatus($"Идёт ивент: {Session.EventName}", Session.RpType.GetColor());

            DiscordBotService.SendStart(Session.EventName, Session.RpType, Session.HostNickname, prepTime);
            DiscordWebhookService.SendStartEmbed(Session, prepTime);

            return true;
        }

        public bool Stop(Player invoker, out string message)
        {
            if (Session.State == EventState.None)
            {
                message = "Нет активного ивента.";
                return false;
            }

            Session.State     = EventState.Stopping;
            Session.StoppedAt = DateTime.UtcNow;

            if (invoker != null)
            {
                Session.HostNickname = invoker.Nickname;
                Session.HostUserId   = invoker.UserId;
            }
            else
            {
                Session.HostNickname = "Консоль";
                Session.HostUserId   = null;
            }

            RpModuleManager.Instance.OnEventStopped(Session.RpType);
            NotifyRaActivity();

            Timing.KillCoroutines(_stopHandle);
            _stopHandle = Timing.RunCoroutine(ClearAfterStop());

            message = "Ивент остановлен.";

            TimeSpan duration = Session.StartedAt == default
                ? TimeSpan.Zero
                : Session.StoppedAt - Session.StartedAt;

            ServerNameService.Reset();

            DiscordBotService.SendStop(Session.EventName, Session.RpType, Session.HostNickname, duration);
            DiscordWebhookService.SendStopEmbed(Session, duration);

            return true;
        }

        public void OnPlayerLeft(Player player)
        {
            if (Session.State == EventState.None || player == null)
                return;

            if (player.UserId == Session.HostUserId)
                Session.HostIsOnline = false;
        }

        public void OnPlayerJoined(Player player)
        {
            if (Session.State == EventState.None || player == null)
                return;

            if (player.UserId == Session.HostUserId && !Session.HostIsOnline)
            {
                Session.HostIsOnline           = true;
                Session.HostReturnedAt         = DateTime.UtcNow;
                Session.ShowHostReturnHighlight = true;
            }
        }

        public void NotifyRaActivity() => LastRaActivity = DateTime.UtcNow;

        public bool IsAfk(int thresholdSeconds) =>
            Session.State != EventState.None &&
            (DateTime.UtcNow - LastRaActivity).TotalSeconds >= thresholdSeconds;

        private IEnumerator<float> TransitionToRunning()
        {
            yield return Timing.WaitForSeconds(1f);

            if (Session.State == EventState.Starting)
                Session.State = EventState.Running;
        }

        private IEnumerator<float> ClearAfterStop()
        {
            yield return Timing.WaitForSeconds(Plugin.Instance.Config.StopLingerSeconds);

            if (Session.State == EventState.Stopping)
                Session.Reset();
        }
    }
}
