using System.Collections.Generic;

namespace EventHUD.Radio
{
    /// <summary>
    /// Состояние конкретной физической рации (ключ — Serial предмета).
    /// </summary>
    public class RadioState
    {
        public RadioTeam Team       { get; set; } = RadioTeam.Unknown;
        public float     Frequency  { get; set; }

        /// <summary>
        /// false = рация лежала на полу, ещё ни разу не бралась.
        /// true  = уже была взята — сохраняем команду при смене владельца
        ///         (д-класс взял рацию СБ → она остаётся на волне СБ).
        /// </summary>
        public bool IsAssigned { get; set; }

        /// <summary>
        /// Волны, доступные на ЭТОЙ рации (фиксируются по первому владельцу).
        /// СБ, подобравший рацию МОГ, крутит только волны МОГ.
        /// </summary>
        public List<RadioTeam> AllowedTeams { get; set; }
    }

    public static class RadioFrequencyStorage
    {
        private static readonly Dictionary<ushort, RadioState> _states = new();

        public static RadioState GetOrCreate(ushort serial)
        {
            if (!_states.TryGetValue(serial, out var state))
            {
                state           = new RadioState();
                _states[serial] = state;
            }

            return state;
        }

        public static bool TryGet(ushort serial, out RadioState state) =>
            _states.TryGetValue(serial, out state);

        /// <summary>Вызывать при старте нового раунда.</summary>
        public static void ClearAll() => _states.Clear();
    }
}
 