using System;
using EventHUD.Rpm;
using Exiled.Events.EventArgs.Player;

namespace EventHUD.Radio
{
    using ExiledRadio = Exiled.API.Features.Items.Radio;

    /// <summary>
    /// Фильтр голосового трафика по рации.
    /// Слышат друг друга только те, у кого совпадает частота.
    /// Дальность — бесконечна (нет проверки расстояния).
    ///
    /// В Exiled ReceivingVoiceMessageEventArgs:
    ///   ev.Sender — тот, кто говорит.
    ///   ev.Player — тот, кто получает сообщение (слушатель).
    /// </summary>
    public class RadioBroadcastFilter
    {
        public void OnReceivingVoiceMessage(ReceivingVoiceMessageEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Radio))
                return;

            // Говорящий должен держать рацию
            if (ev.Sender?.CurrentItem is not ExiledRadio senderRadio)
                return;

            if (!RadioFrequencyStorage.TryGet(senderRadio.Serial, out var senderState))
            {
                ev.IsAllowed = false;
                return;
            }

            // Слушатель должен держать рацию
            if (ev.Player?.CurrentItem is not ExiledRadio receiverRadio)
            {
                ev.IsAllowed = false;
                return;
            }

            if (!RadioFrequencyStorage.TryGet(receiverRadio.Serial, out var receiverState))
            {
                ev.IsAllowed = false;
                return;
            }

            // Частоты должны совпадать (±0.05 — защита от float-погрешности)
            const float Tolerance = 0.05f;
            ev.IsAllowed = Math.Abs(senderState.Frequency - receiverState.Frequency) < Tolerance;
        }
    }
}
 