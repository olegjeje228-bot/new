using System.Collections.Generic;
using Exiled.API.Features;
using MEC;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Тик-сервис для стаков контузии корпуса.
    /// При 5+ стаках (асфиксия) — -2HP каждые 3 секунды.
    /// </summary>
    public class BodyStunTickService
    {
        private CoroutineHandle _handle;

        public void Start() => _handle = Timing.RunCoroutine(TickLoop());
        public void Stop() => Timing.KillCoroutines(_handle);

        private IEnumerator<float> TickLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(3f);

                if (!Rpm.RpModuleManager.Instance.IsEnabled(Rpm.RpModuleType.Medicine))
                    continue;

                foreach (var player in Player.List)
                {
                    if (!player.IsAlive)
                        continue;

                    if (!ArmorStorage.TryGet(player.UserId, out var armor))
                        continue;

                    if (!armor.HasAsphyxia)
                        continue;

                    // Проверяем что стаки ещё активны
                    armor.CleanExpiredStacks();
                    if (armor.ActiveStacks < 5)
                    {
                        armor.HasAsphyxia = false;
                        continue;
                    }

                    // -2 HP
                    player.Hurt(2f, "Асфиксия");
                }
            }
        }
    }
}
 