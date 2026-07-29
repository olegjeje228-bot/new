using System.Text.RegularExpressions;
using EventHUD.Enums;
using EventHUD.Extensions;
using EventHUD.Models;

namespace EventHUD.Hud
{
    public static class EventStatusBuilder
    {
        public static string Build(Config c)
        {
            var session = EventManager.Instance.Session;

            if (session.State == EventState.None)
                return $"<indent=0%><voffset={c.EventVoffset}em><alpha=#00>.</alpha>";

            string text   = BuildStateText(c, session);
            float  indent = CalculateIndent(c, text);

            return $"<indent={indent:0.###}%><voffset={c.EventVoffset}em>{text}";
        }

        private static string BuildStateText(Config c, EventSession session)
        {
            if (EventManager.Instance.IsAfk(c.AfkThresholdSeconds))
                return $"<color={c.AfkColor}>{c.AfkText}</color>";

            string text = RawText(session);

            if (session.State != EventState.Stopping)
            {
                if (session.ShowHostReturnHighlight)
                    text = $"<color={c.HostReturnColor}>{StripColorTags(text)}</color>";
                else if (!session.HostIsOnline)
                    text = $"<color={c.HostOfflineColor}>{StripColorTags(text)}</color>";
            }

            return text;
        }

        private static float CalculateIndent(Config c, string text)
        {
            float indent = c.EventIndentBase - c.EventIndentSlope * text.VisibleLength();
            // Минимум 18% чтобы не уехало за край
            if (indent < 18f) indent = 18f;
            return indent;
        }

        private static string RawText(EventSession s) => s.State switch
        {
            EventState.Preparing =>
                $"Идёт подготовка ивента: <b>{s.EventName}</b> | Время: {s.Elapsed.ToHudFormat()} | {RpTag(s)}",
            EventState.Starting =>
                $"Ивент начинается: <b>{s.EventName}</b> | {RpTag(s)}",
            EventState.Running =>
                $"Идёт ивент: <b>{s.EventName}</b> | Время: {s.Elapsed.ToHudFormat()} | {RpTag(s)}",
            EventState.Stopping =>
                $"Ивент завершён | Время: {(s.StoppedAt - s.StartedAt).ToHudFormat()} | Проводящий: {s.HostNickname}",
            _ => string.Empty
        };

        private static string RpTag(EventSession s) =>
            $"<color={s.RpType.GetColor()}>{s.RpType.GetShortName()}</color>";

        private static string StripColorTags(string input) =>
            Regex.Replace(input, "<color=.*?>|</color>", "");
    }
}
 