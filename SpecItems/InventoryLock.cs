namespace EventHUD.SpecItems
{
    using System.Collections.Generic;
    using Exiled.API.Features;
    using Exiled.Events.EventArgs.Player;

    public static class InventoryLock
    {
        private static readonly Dictionary<string, ushort> Locked = new Dictionary<string, ushort>();

        public static bool IsLocked(Player player)
        {
            if (player == null)
                return false;

            return Locked.ContainsKey(player.UserId ?? player.Nickname);
        }

        public static void Lock(Player player, ushort allowedSerial)
        {
            if (player == null)
                return;

            string id = player.UserId ?? player.Nickname;
            Locked[id] = allowedSerial;

            if (player.CurrentItem != null && player.CurrentItem.Serial != allowedSerial)
                player.CurrentItem = null;

            player.ShowHint("<color=red>Инвентарь заблокирован. Выстрел из телепортера - возврат</color>", 4f);
            SpecDebug.Log("ЛОК: " + player.Nickname + " заблокирован, разрешён serial " + allowedSerial);
        }

        public static void Unlock(Player player)
        {
            if (player == null)
                return;

            string id = player.UserId ?? player.Nickname;

            if (Locked.Remove(id))
            {
                player.ShowHint("<color=green>Инвентарь разблокирован</color>", 2f);
                SpecDebug.Log("ЛОК: " + player.Nickname + " разблокирован");
            }
        }

        public static void Enable()
        {
            Exiled.Events.Handlers.Player.ChangingItem += OnChangingItem;
            Exiled.Events.Handlers.Player.DroppingItem += OnDroppingItem;
            Exiled.Events.Handlers.Player.DroppingAmmo += OnDroppingAmmo;
            Exiled.Events.Handlers.Player.PickingUpItem += OnPickingUpItem;
            Exiled.Events.Handlers.Player.ReloadingWeapon += OnReloadingWeapon;
            Exiled.Events.Handlers.Player.Shooting += OnShooting;
            Exiled.Events.Handlers.Player.Left += OnLeft;
            Exiled.Events.Handlers.Player.Died += OnDied;
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
            SpecDebug.Log("ЛОК: система включена");
        }

        public static void Disable()
        {
            Exiled.Events.Handlers.Player.ChangingItem -= OnChangingItem;
            Exiled.Events.Handlers.Player.DroppingItem -= OnDroppingItem;
            Exiled.Events.Handlers.Player.DroppingAmmo -= OnDroppingAmmo;
            Exiled.Events.Handlers.Player.PickingUpItem -= OnPickingUpItem;
            Exiled.Events.Handlers.Player.ReloadingWeapon -= OnReloadingWeapon;
            Exiled.Events.Handlers.Player.Shooting -= OnShooting;
            Exiled.Events.Handlers.Player.Left -= OnLeft;
            Exiled.Events.Handlers.Player.Died -= OnDied;
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
            Locked.Clear();
        }

        private static bool TryGetAllowed(Player player, out ushort allowedSerial)
        {
            allowedSerial = 0;

            if (player == null)
                return false;

            return Locked.TryGetValue(player.UserId ?? player.Nickname, out allowedSerial);
        }

        private static void OnChangingItem(ChangingItemEventArgs ev)
        {
            ushort allowed;

            if (!TryGetAllowed(ev.Player, out allowed))
                return;

            if (ev.Item != null && ev.Item.Serial == allowed)
                return;

            ev.IsAllowed = false;
        }

        private static void OnDroppingItem(DroppingItemEventArgs ev)
        {
            if (IsLocked(ev.Player))
                ev.IsAllowed = false;
        }

        private static void OnDroppingAmmo(DroppingAmmoEventArgs ev)
        {
            if (IsLocked(ev.Player))
                ev.IsAllowed = false;
        }

        private static void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            if (IsLocked(ev.Player))
                ev.IsAllowed = false;
        }

        private static void OnReloadingWeapon(ReloadingWeaponEventArgs ev)
        {
            if (IsLocked(ev.Player))
                ev.IsAllowed = false;
        }

        private static void OnShooting(ShootingEventArgs ev)
        {
            ushort allowed;

            if (!TryGetAllowed(ev.Player, out allowed))
                return;

            if (ev.Firearm != null && ev.Firearm.Serial == allowed)
                return;

            ev.IsAllowed = false;
        }

        private static void OnLeft(LeftEventArgs ev)
        {
            if (ev.Player != null)
                Locked.Remove(ev.Player.UserId ?? ev.Player.Nickname);
        }

        private static void OnDied(DiedEventArgs ev)
        {
            if (ev.Player != null && Locked.Remove(ev.Player.UserId ?? ev.Player.Nickname))
                SpecDebug.Log("ЛОК: " + ev.Player.Nickname + " разблокирован (смерть)");
        }

        private static void OnRoundStarted()
        {
            if (Locked.Count > 0)
                SpecDebug.Log("ЛОК: рестарт раунда, снято блокировок: " + Locked.Count);

            Locked.Clear();
            TowerTeleporter.ResetState();
        }
    }
}