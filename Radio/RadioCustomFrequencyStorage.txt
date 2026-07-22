using System.Collections.Generic;

namespace EventHUD.Radio
{
    /// <summary>
    /// Хранит персональную свободную частоту каждого игрока (из SSS).
    /// Ключ — UserId.
    /// </summary>
    public static class RadioCustomFrequencyStorage
    {
        private static readonly Dictionary<string, float> _saved = new();

        /// <summary>
        /// Возвращает частоту, если она задана и больше 0.
        /// null — если не задана или была сброшена.
        /// </summary>
        public static float? Get(string userId)
        {
            if (!_saved.TryGetValue(userId, out var freq))
                return null;

            // 0 == сброшено (игрок поставил слайдер в 0)
            return freq > 0f ? freq : (float?)null;
        }

        public static void Set(string userId, float frequency) =>
            _saved[userId] = frequency;

        /// <summary>Явно удаляет запись — равнозначно Set(userId, 0).</summary>
        public static void Remove(string userId) =>
            _saved.Remove(userId);

        /// <summary>Вызывать при старте нового раунда.</summary>
        public static void ClearAll() => _saved.Clear();
    }
}
 