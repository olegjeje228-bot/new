using System;
using System.Collections.Generic;
using Exiled.API.Features;

namespace EventHUD.Radio
{
    /// <summary>
    /// Хранит активный hint смены волны для каждого игрока.
    /// HudCompositor вставляет его в нижнюю часть HUD на RadioSwitchHintDuration секунд.
    /// </summary>
    public static class RadioSwitchNoticeService
    {
        private class Notice
        {
            public string   Text;
            public DateTime ExpiresAt;
        }

        private static readonly Dictionary<string, Notice> _notices = new();

        public static void Show(Player player, string text, float duration)
        {
            _notices[player.UserId] = new Notice
            {
                Text      = text,
                ExpiresAt = DateTime.UtcNow.AddSeconds(duration)
            };
        }

        public static string GetActive(Player player)
        {
            if (!_notices.TryGetValue(player.UserId, out var notice))
                return null;

            if (DateTime.UtcNow >= notice.ExpiresAt)
            {
                _notices.Remove(player.UserId);
                return null;
            }

            return notice.Text;
        }
    }
}
 