using System;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Одна конкретная травма на конкретной части тела.
    /// </summary>
    public class LocalInjury
    {
        public LocalInjuryType Type     { get; set; }
        public BodyPart        Part     { get; set; }
        public DateTime        OccurredAt { get; set; }

        public LocalInjury(LocalInjuryType type, BodyPart part)
        {
            Type       = type;
            Part       = part;
            OccurredAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Строка для HUD: "Огнестр.(пр.р)"
        /// </summary>
        public string ToHudString()
        {
            return $"<color={Type.GetColor()}>{Type.GetShortName()}({Part.GetShortName()})</color>";
        }

        /// <summary>
        /// Полная строка для команды .injuries
        /// </summary>
        public string ToFullString()
        {
            return $"{Type.GetFullName()} — {Part.GetFullName()} ({(DateTime.UtcNow - OccurredAt).TotalSeconds:0}с назад)";
        }
    }
}
 