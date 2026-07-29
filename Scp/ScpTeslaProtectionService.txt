using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using UnityEngine;

namespace EventHUD.Scp
{
    public class ScpTeslaProtectionService
    {
        private const float Scp106TeslaRadius = 5f;
        private const float ConcussionCooldown = 5f;

        private static readonly HashSet<RoleTypeId> ImmuneRoles = new()
        {
            RoleTypeId.Scp3114,
            RoleTypeId.Scp173,
            RoleTypeId.Scp106,
        };

        private readonly Dictionary<int, float> _lastConcussion = new();

        public void OnTriggeringTesla(TriggeringTeslaEventArgs ev)
        {
            if (ev.Player == null || ev.Tesla == null)
                return;

            if (!ImmuneRoles.Contains(ev.Player.Role.Type))
                return;

            // Запрещаем активацию теслы этим игроком
            ev.IsAllowed = false;

            // 106 получает Concussed при входе в радиус (с кулдауном)
            if (ev.Player.Role.Type == RoleTypeId.Scp106)
            {
                float dist = Vector3.Distance(
                    ev.Player.Position,
                    ev.Tesla.Position);

                if (dist <= Scp106TeslaRadius)
                {
                    int pid = ev.Player.Id;
                    float now = Time.time;

                    if (!_lastConcussion.TryGetValue(pid, out float last)
                        || now - last >= ConcussionCooldown)
                    {
                        _lastConcussion[pid] = now;
                        ev.Player.EnableEffect(EffectType.Concussed, 5f);
                    }
                }
            }
        }

        public void OnHurting(HurtingEventArgs ev)
        {
            if (ev.Player == null)
                return;

            if (ev.DamageHandler.Type != DamageType.Tesla)
                return;

            if (ImmuneRoles.Contains(ev.Player.Role.Type))
                ev.IsAllowed = false;
        }

        public void OnRoundRestart()
        {
            _lastConcussion.Clear();
        }
    }
}