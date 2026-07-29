using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using MEC;
using PlayerRoles;
using EventHUD.Rpm;
// целиком PlayerRoles.PlayableScps.Scp096 не импортим - конфликт с эксайловским Scp096Role
using Scp096RageState = PlayerRoles.PlayableScps.Scp096.Scp096RageState;

namespace EventHUD.Scp
{
    /// <summary>
    /// Бесконечная ярость SCP-096.
    /// После старта ярости её таймер постоянно поддерживается.
    /// Ярость можно принудительно закончить через Scp096Role.Calm().
    /// </summary>
    public class Scp096Handler
    {
        private CoroutineHandle _loop;

        public void Start()
        {
            _loop = Timing.RunCoroutine(RageLoop());
        }

        public void Stop()
        {
            Timing.KillCoroutines(_loop);
        }

        private IEnumerator<float> RageLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(0.2f);

                if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine))
                    continue;

                foreach (var player in Player.List)
                {
                    if (player == null || !player.IsAlive)
                        continue;

                    if (player.Role is not Scp096Role scp096)
                        continue;

                    if (scp096.RageState == Scp096RageState.Enraged)
                    {
                        // злой - не даём таймеру дойти до нуля.
                        // подливаем не каждый тик, чтобы не спамить RPC на клиентов
                        if (scp096.EnragedTimeLeft < 15f)
                            scp096.EnragedTimeLeft = 30f;
                    }
                    else if (scp096.EnrageCooldown > 0f)
                    {
                        // спокойный - просто убираем кулдаун на следующий агр
                        scp096.EnrageCooldown = 0f;
                    }
                }
            }
        }
    }
}
