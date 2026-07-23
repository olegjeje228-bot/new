using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using EventHUD.Rpm;
using BaseScp3114Identity = PlayerRoles.PlayableScps.Scp3114.Scp3114Identity;

namespace EventHUD.Scp
{
    public class Scp3114Handler
    {
        private readonly Config _config;

        private readonly Dictionary<string, CoroutineHandle> _disguiseLoops =
            new Dictionary<string, CoroutineHandle>();

        private readonly Dictionary<string, int> _bulletHits =
            new Dictionary<string, int>();

        public Scp3114Handler(Config config)
        {
            _config = config;
        }

        public void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine))
                return;

            if (ev.Player == null)
                return;

            string uid = ev.Player.UserId;
            ClearPlayer(uid);

            if (ev.NewRole != RoleTypeId.Scp3114)
                return;

            Timing.CallDelayed(1f, () =>
            {
                if (ev.Player == null ||
                    !ev.Player.IsAlive ||
                    ev.Player.Role.Type != RoleTypeId.Scp3114)
                {
                    return;
                }

                CoroutineHandle handle = Timing.RunCoroutine(
                    KeepDisguiseDuration(ev.Player));

                _disguiseLoops[uid] = handle;
            });
        }

        private IEnumerator<float> KeepDisguiseDuration(Player player)
        {
            string uid = player.UserId;
            float baseDuration = 0f;

            // Читаем стандартную длительность из роли
            if (player.Role is Scp3114Role scp3114)
                baseDuration = scp3114.DisguiseDuration;

            if (baseDuration <= 0f)
                baseDuration = 30f; // fallback

            float multiplied = baseDuration * 4f;

            while (player != null &&
                   player.IsAlive &&
                   player.Role is Scp3114Role role)
            {
                role.DisguiseDuration = multiplied;

                if (role.DisguiseStatus == BaseScp3114Identity.DisguiseStatus.Active)
                {
                    // Устанавливаем реальный таймер на x4
                    role.Identity.RemainingDuration.Trigger(multiplied);
                }

                yield return Timing.WaitForSeconds(1f);
            }
        }

        public void OnHurting(HurtingEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine))
                return;

            if (ev.Player == null || ev.Attacker == null)
                return;

            if (ev.Player.Role.Type != RoleTypeId.Scp3114)
                return;

            // Считаем только огнестрельные попадания
            if (ev.DamageHandler.Type != Exiled.API.Enums.DamageType.Com15 &&
                ev.DamageHandler.Type != Exiled.API.Enums.DamageType.Com18 &&
                ev.DamageHandler.Type != Exiled.API.Enums.DamageType.Com45 &&
                ev.DamageHandler.Type != Exiled.API.Enums.DamageType.Revolver &&
                ev.DamageHandler.Type != Exiled.API.Enums.DamageType.Fsp9 &&
                ev.DamageHandler.Type != Exiled.API.Enums.DamageType.Crossvec &&
                ev.DamageHandler.Type != Exiled.API.Enums.DamageType.Shotgun &&
                ev.DamageHandler.Type != Exiled.API.Enums.DamageType.AK &&
                ev.DamageHandler.Type != Exiled.API.Enums.DamageType.Logicer &&
                ev.DamageHandler.Type != Exiled.API.Enums.DamageType.E11Sr &&
                ev.DamageHandler.Type != Exiled.API.Enums.DamageType.Frmg0 &&
                ev.DamageHandler.Type != Exiled.API.Enums.DamageType.A7 &&
                ev.DamageHandler.Type != Exiled.API.Enums.DamageType.Firearm)
            {
                return;
            }

            // Не считаем отменённый урон
            if (ev.Amount <= 0f)
                return;

            string uid = ev.Player.UserId;

            if (!_bulletHits.ContainsKey(uid))
                _bulletHits[uid] = 0;

            _bulletHits[uid]++;

            if (_bulletHits[uid] >= 5)
            {
                _bulletHits[uid] = 0;

                if (ev.Player.Role is Scp3114Role scp3114)
                {
                    scp3114.DisguiseStatus = BaseScp3114Identity.DisguiseStatus.None;
                }
            }
        }

        public void OnItemAdded(ItemAddedEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine))
                return;

            if (ev.Player == null || ev.Item == null)
                return;

            if (ev.Player.Role.Type != RoleTypeId.Scp3114)
                return;
        }

        public void OnShooting(ShootingEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine))
                return;

            if (ev.Player == null || ev.Player.Role.Type != RoleTypeId.Scp3114)
                return;

            ev.IsAllowed = true;
        }

        public void ClearPlayer(string userId)
        {
            if (_disguiseLoops.TryGetValue(userId, out CoroutineHandle handle))
            {
                Timing.KillCoroutines(handle);
                _disguiseLoops.Remove(userId);
            }

            _bulletHits.Remove(userId);
        }

        public void ClearAll()
        {
            foreach (CoroutineHandle handle in _disguiseLoops.Values)
                Timing.KillCoroutines(handle);

            _disguiseLoops.Clear();
            _bulletHits.Clear();
        }
    }
}
