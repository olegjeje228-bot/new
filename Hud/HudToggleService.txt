using System.Collections.Generic;
using Exiled.API.Features;

namespace EventHUD.Hud
{
    public static class HudToggleService
    {
        private static readonly HashSet<string> Overrides    = new HashSet<string>();
        private static          bool            _enabledByDefault = true;

        public static void Initialize(bool enabledByDefault)
        {
            _enabledByDefault = enabledByDefault;
            Overrides.Clear();
        }

        public static bool IsEnabled(Player player)
        {
            if (player == null)
                return false;

            return _enabledByDefault
                ? !Overrides.Contains(player.UserId)
                :  Overrides.Contains(player.UserId);
        }

        public static bool Toggle(string userId)
        {
            if (Overrides.Contains(userId))
            {
                Overrides.Remove(userId);
                return _enabledByDefault;
            }

            Overrides.Add(userId);
            return !_enabledByDefault;
        }

        public static void SetReloaded(string userId)
        {
            Overrides.Remove(userId);
        }

        public static void Clear(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
                Overrides.Remove(userId);
        }

        public static void Reset() => Overrides.Clear();
    }
}