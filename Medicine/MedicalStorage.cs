using System.Collections.Generic;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Хранилище медицинских состояний всех игроков.
    /// Ключ — UserId.
    /// </summary>
    public static class MedicalStorage
    {
        private static readonly Dictionary<string, PlayerMedicalState> _states = new();

        public static PlayerMedicalState GetOrCreate(string userId)
        {
            if (!_states.TryGetValue(userId, out var state))
            {
                state = new PlayerMedicalState();
                state.Reset(); // Начинаем с Normal
                _states[userId] = state;
            }
            return state;
        }

        public static bool TryGet(string userId, out PlayerMedicalState state) =>
            _states.TryGetValue(userId, out state);

        public static void Remove(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return;
            _states.Remove(userId);
        }

        /// <summary>Вызывать при старте нового раунда.</summary>
        public static void ClearAll() => _states.Clear();
    }
}
 