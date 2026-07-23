using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;

namespace EventHUD
{
    /// <summary>Блокирует волны подкрепления (МОГ/ПХ) и лочит боеголовку при старте раунда.</summary>
    public static class RoundControl
    {
        public static void Register()
        {
            Exiled.Events.Handlers.Server.RespawningTeam += OnRespawningTeam;
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        }

        public static void Unregister()
        {
            Exiled.Events.Handlers.Server.RespawningTeam -= OnRespawningTeam;
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        }

        private static void OnRespawningTeam(RespawningTeamEventArgs ev)
        {
            ev.IsAllowed = false;
        }

        private static void OnRoundStarted()
        {
            Warhead.Stop();
            Warhead.IsLocked = true;
        }
    }
}