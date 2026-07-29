using System.Collections.Generic;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Хранилище состояний меню аптечки для каждого игрока.
    /// </summary>
    public static class MedkitStorage
    {
        private static readonly Dictionary<string, MedkitMenuState> _states = new();

        public static MedkitMenuState GetOrCreate(string userId)
        {
            if (!_states.TryGetValue(userId, out var state))
            {
                state = new MedkitMenuState();
                _states[userId] = state;
            }
            return state;
        }

        public static bool TryGet(string userId, out MedkitMenuState state) =>
            _states.TryGetValue(userId, out state);

        public static void Remove(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return;
            _states.Remove(userId);
        }

        public static void ClearAll() => _states.Clear();
    }
}
 