using System.Collections.Generic;
using Exiled.API.Features;
using UnityEngine;

namespace EventHUD.Scp
{
    public static class ScpProximityChat
    {
        private static readonly HashSet<string> Enabled =
            new HashSet<string>();

        private const float ProximityRange = 15f;

        public static bool IsEnabled(Player player)
        {
            return player != null &&
                   Enabled.Contains(player.UserId);
        }

        public static bool Toggle(Player player)
        {
            if (player == null)
                return false;

            if (Enabled.Remove(player.UserId))
                return false;

            Enabled.Add(player.UserId);
            return true;
        }

        public static void Disable(Player player)
        {
            if (player != null)
                Enabled.Remove(player.UserId);
        }

        public static void Remove(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
                Enabled.Remove(userId);
        }

        public static void Clear()
        {
            Enabled.Clear();
        }

        public static bool ShouldHear(
            Player speaker,
            Player listener)
        {
            if (listener == null)
                return false;

            if (!IsEnabled(listener))
                return true;

            if (speaker == null || !speaker.IsScp)
                return false;

            return Vector3.Distance(
                       speaker.Position,
                       listener.Position) <= ProximityRange;
        }
    }
}