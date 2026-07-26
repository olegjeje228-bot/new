using System.Collections.Generic;
using Exiled.API.Features;
using MEC;

namespace EventHUD
{
    /// <summary>Снимает round lock, если на сервере нет игроков более 3 минут.</summary>
    public static class RoundLockAutoOff
    {
        private const double EmptySecondsLimit = 180.0;
        private static CoroutineHandle _handle;
        private static double _emptySince = -1.0;

        public static void Start()
        {
            Stop();
            _handle = Timing.RunCoroutine(CheckLoop());
        }

        public static void Stop()
        {
            if (_handle.IsRunning)
                Timing.KillCoroutines(_handle);
            _emptySince = -1.0;
        }

        private static IEnumerator<float> CheckLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(15f);

                if (Player.List.Count == 0)
                {
                    if (_emptySince < 0)
                        _emptySince = Timing.LocalTime;
                    else if (Timing.LocalTime - _emptySince >= EmptySecondsLimit && Round.IsLocked)
                    {
                        Round.IsLocked = false;
                        Log.Info("[EventHUD] Сервер пуст более 3 минут — round lock выключен.");
                    }
                }
                else
                {
                    _emptySince = -1.0;
                }
            }
        }
    }
}