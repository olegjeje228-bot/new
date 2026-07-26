using System;
using System.Collections.Generic;
using Exiled.API.Features;
using MEC;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Критическое состояние: HP <= 0 → 30 секунд до килла.
    /// Можно спасти дефибриллятором (если кровотечение остановлено).
    /// </summary>
    public class CriticalStateService
    {
        private readonly Dictionary<string, CoroutineHandle> _coroutines = new();

        /// <summary>
        /// Вызывается когда HP игрока упал до 0.
        /// Вместо смерти — критическое состояние.
        /// </summary>
        public void EnterCriticalState(Player player)
        {
            if (_coroutines.ContainsKey(player.UserId))
                return; // Уже в критическом

            var state = MedicalStorage.GetOrCreate(player.UserId);
            state.AddCondition(GlobalCondition.CardiacArrest);

            // HP = 1 (не даём умереть)
            player.Health = 1;

            _coroutines[player.UserId] = Timing.RunCoroutine(CriticalCoroutine(player));
        }

        /// <summary>
        /// Спасти игрока дефибриллятором.
        /// </summary>
        public bool Revive(Player player, float healTo = 10f)
        {
            var state = MedicalStorage.GetOrCreate(player.UserId);

            // Нельзя реанимировать если кровотечение не остановлено
            if (state.GetBleedingLevel().HasValue)
                return false;

            // Нельзя реанимировать если смертельное ранение головы
            if (state.HasCondition(GlobalCondition.LethalHeadshot))
                return false;

            state.RemoveCondition(GlobalCondition.CardiacArrest);
            player.Health = healTo;

            if (_coroutines.TryGetValue(player.UserId, out var handle))
            {
                Timing.KillCoroutines(handle);
                _coroutines.Remove(player.UserId);
            }

            return true;
        }

        public void CancelAll()
        {
            foreach (var h in _coroutines.Values)
                Timing.KillCoroutines(h);
            _coroutines.Clear();
        }

        public void Cancel(string userId)
        {
            if (_coroutines.TryGetValue(userId, out var h))
            {
                Timing.KillCoroutines(h);
                _coroutines.Remove(userId);
            }
        }

        private IEnumerator<float> CriticalCoroutine(Player player)
        {
            float elapsed = 0f;
            const float maxTime = 30f;

            while (elapsed < maxTime)
            {
                yield return Timing.WaitForSeconds(1f);
                elapsed += 1f;

                if (player == null || !player.IsAlive)
                {
                    _coroutines.Remove(player?.UserId ?? "");
                    yield break;
                }

                // Если вылечили — выход
                var state = MedicalStorage.GetOrCreate(player.UserId);
                if (!state.HasCondition(GlobalCondition.CardiacArrest))
                {
                    _coroutines.Remove(player.UserId);
                    yield break;
                }

                // Держим HP=1
                if (player.Health > 1)
                    player.Health = 1;
            }

            // 30 сек прошло — килл
            if (player != null && player.IsAlive)
                player.Kill("Критическое состояние — не оказана помощь");

            _coroutines.Remove(player?.UserId ?? "");
        }
    }
}
 