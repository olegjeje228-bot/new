using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;

namespace EventHUD.Scp
{
    public sealed class ScpProximityVoiceFilter
    {
        public void OnReceivingVoiceMessage(
            ReceivingVoiceMessageEventArgs ev)
        {
            if (ev.Player == null)
                return;

            Player listener = ev.Player;

            if (!ScpProximityChat.IsEnabled(listener))
                return;

            // В EXILED 9.14.2 сообщение может лежать в ev.VoiceMessage.
            Player speaker = Player.Get(ev.VoiceMessage.Speaker);

            if (!ScpProximityChat.ShouldHear(speaker, listener))
                ev.IsAllowed = false;
        }
    }
}