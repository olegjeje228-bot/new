using System;
using System.Collections.Generic;
using Exiled.API.Features;

namespace EventHUD.Hud
{
    /// <summary>
    /// Единая точка для всех временных уведомлений поверх HUD.
    /// Заменяет прямые вызовы player.ShowHint, чтобы не было конфликтов
    /// с основным HUD — всё рисуется в одном хинте.
    /// </summary>
    public static class HudNoticeService
    {
        private class Notice
        {
            public string   Text;
            public DateTime ExpiresAt;
        }

        private static readonly Dictionary<string, Notice> _notices = new();

        public static void Show(Player player, string text, float duration)
        {
            if (player == null || string.IsNullOrEmpty(text))
                return;

            _notices[player.UserId] = new Notice
            {
                Text      = text,
                ExpiresAt = DateTime.UtcNow.AddSeconds(duration)
            };
        }

        public static string GetActive(Player player)
        {
            if (player == null || !_notices.TryGetValue(player.UserId, out var notice))
                return null;

            if (DateTime.UtcNow >= notice.ExpiresAt)
            {
                _notices.Remove(player.UserId);
                return null;
            }

            return notice.Text;
        }

        public static void Clear(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
                _notices.Remove(userId);
        }

        public static void Reset() => _notices.Clear();
    }
}