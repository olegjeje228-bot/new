using System;
using System.Collections.Generic;
using PlayerRoles;

namespace EventHUD.Radio
{
    /// <summary>Ивент-волна, созданная командой radio wave add. Живёт до конца раунда / ev stop.</summary>
    public class EventWave
    {
        public string Name;
        public float Frequency;
        public HashSet<RoleTypeId> Roles; // null или пусто = доступна всем

        public bool IsAvailableFor(Exiled.API.Features.Player player) =>
            Roles == null || Roles.Count == 0 || Roles.Contains(player.Role.Type);
    }

    public static class EventWaveStorage
    {
        private static readonly List<EventWave> _waves = new List<EventWave>();
        private static float _nextFreq = 1000f;

        public static IReadOnlyList<EventWave> Waves => _waves;

        public static bool Add(string name, HashSet<RoleTypeId> roles, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(name)) { error = "Пустое название волны."; return false; }
            if (Find(name) != null) { error = $"Волна '{name}' уже существует."; return false; }

            _waves.Add(new EventWave { Name = name.Trim(), Frequency = _nextFreq, Roles = roles });
            _nextFreq += 10f;
            return true;
        }

        public static bool Remove(string name) => _waves.Remove(Find(name));

        public static EventWave Find(string name)
        {
            foreach (var w in _waves)
                if (string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase))
                    return w;
            return null;
        }

        public static List<EventWave> GetAvailableFor(Exiled.API.Features.Player player)
        {
            var list = new List<EventWave>();
            foreach (var w in _waves)
                if (w.IsAvailableFor(player))
                    list.Add(w);
            return list;
        }

        public static void ClearAll() => _waves.Clear();
    }
}