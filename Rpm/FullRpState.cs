using System.Collections.Generic;

namespace EventHUD.Rpm
{
    /// <summary>Глобальный переключатель FullRP + подтверждения биндов.</summary>
    public static class FullRpState
    {
        public static bool IsEnabled { get; set; }

        private static readonly HashSet<string> _confirmed = new HashSet<string>();

        public static bool IsConfirmed(string userId) => userId != null && _confirmed.Contains(userId);
        public static void Confirm(string userId) { if (userId != null) _confirmed.Add(userId); }
        public static void ResetConfirmations() => _confirmed.Clear();
    }
}