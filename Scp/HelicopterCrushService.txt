using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using MEC;
using PlayerRoles;
using UnityEngine;

namespace EventHUD.Scp
{
    public class HelicopterCrushService
    {
        private const float CrushRadius = 5f;
        private const float CrushHeight = 3f;
        private static readonly Vector3 LandingZone = new Vector3(0f, 0f, 0f);

        private bool _crushActive;

        public void OnRespawnedTeam(RespawnedTeamEventArgs ev)
        {
            if (!ev.Players.Any(p => p != null && p.Role.Team == Team.FoundationForces))
                return;

            if (_crushActive)
                return;

            _crushActive = true;
            Timing.CallDelayed(1f, CheckCrush);
        }

        private void CheckCrush()
        {
            if (!_crushActive)
                return;

            foreach (Player player in Player.List)
            {
                if (player == null || !player.IsAlive)
                    continue;

                if (player.Role.Team == Team.SCPs || player.IsNPC)
                    continue;

                Vector3 pos = player.Position;
                float dx = pos.x - LandingZone.x;
                float dz = pos.z - LandingZone.z;
                float dy = pos.y - LandingZone.y;

                if (dx * dx + dz * dz > CrushRadius * CrushRadius)
                    continue;

                if (dy < 0f || dy > CrushHeight)
                    continue;

                player.EnableEffect<PitDeath>();
            }

            _crushActive = false;
        }

        public void Reset()
        {
            _crushActive = false;
        }
    }
}