using System;

namespace EventHUD.Extensions
{
    public static class TimeExtensions
    {
        public static string ToHudFormat(this TimeSpan span)
        {
            return span.TotalHours >= 1
                ? span.ToString(@"hh\:mm\:ss")
                : span.ToString(@"mm\:ss");
        }
    }
}
 