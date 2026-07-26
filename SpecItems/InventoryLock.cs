namespace EventHUD.SpecItems
{
    using System.Collections.Generic;
    using Exiled.API.Features;
    using Exiled.Events.EventArgs.Player;

    public static class InventoryLock
    {
        private static readonly HashSet<string> LockedIds = new HashSet<string>();

        private static bool subscribed;

        public static bool IsLocked(Player player)
        {
            return !(player is null) && !string.IsNullOrEmpty(player.UserId) && LockedIds.Contains(player.UserId);
        }

        public static void Lock(Player player)
        {
            if (player is null || string.IsNullOrEmpty(player.UserId))
                return;

            LockedIds.Add(player.UserId);

            try
            {
                player.CurrentItem = null;
            }
            catch
            {
            }

            SpecDebug.Log("LOCK инвентарь: " + player.Nickname);
        }

        public static void Unlock(Player player)
        {
            if (player is null || string.IsNullOrEmpty(player.UserId))
                return;

            LockedIds.Remove(player.UserId);
            SpecDebug.Log("UNLOCK инвентарь: " + player.Nickname);
        }

        public static void Enable()
        {
            if (subscribed)
                return;

            subscribed = true;

            Exiled.Events.Handlers.Player.ChangingItem += OnChangingItem;
            Exiled.Events.Handlers.Player.DroppingItem += OnDroppingItem;
            Exiled.Events.Handlers.Player.DroppingAmmo += OnDroppingAmmo;
            Exiled.Events.Handlers.Player.PickingUpItem += OnPickingUpItem;
            Exiled.Events.Handlers.Player.ReloadingWeapon += OnReloading;
            Exiled.Events.Handlers.Player.Shooting += OnShooting;
            Exiled.Events.Handlers.Player.Left += OnLeft;
        }

        public static void Disable()
        {
            if (!subscribed)
                return;

            subscribed = false;

            Exiled.Events.Handlers.Player.ChangingItem -= OnChangingItem;
            Exiled.Events.Handlers.Player.DroppingItem -= OnDroppingItem;
            Exiled.Events.Handlers.Player.DroppingAmmo -= OnDroppingAmmo;
            Exiled.Events.Handlers.Player.PickingUpItem -= OnPickingUpItem;
            Exiled.Events.Handlers.Player.ReloadingWeapon -= OnReloading;
            Exiled.Events.Handlers.Player.Shooting -= OnShooting;
            Exiled.Events.Handlers.Player.Left -= OnLeft;

            LockedIds.Clear();
        }

        private static void Deny(Player player)
        {
            if (!(player is null))
                player.ShowHint("<color=red>Инвентарь заблокирован</color>", 1.5f);
        }

        private static void OnChangingItem(ChangingItemEventArgs ev)
        {
            if (!IsLocked(ev.Player))
                return;

            ev.IsAllowed = false;
            Deny(ev.Player);
        }

        private static void OnDroppingItem(DroppingItemEventArgs ev)
        {
            if (!IsLocked(ev.Player))
                return;

            ev.IsAllowed = false;
            Deny(ev.Player);
        }

        private static void OnDroppingAmmo(DroppingAmmoEventArgs ev)
        {
            if (!IsLocked(ev.Player))
                return;

            ev.IsAllowed = false;
        }

        private static void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            if (!IsLocked(ev.Player))
                return;

            ev.IsAllowed = false;
        }

        private static void OnReloading(ReloadingWeaponEventArgs ev)
        {
            if (!IsLocked(ev.Player))
                return;

            ev.IsAllowed = false;
        }

        private static void OnShooting(ShootingEventArgs ev)
        {
            if (!IsLocked(ev.Player))
                return;

            ev.IsAllowed = false;
        }

        private static void OnLeft(LeftEventArgs ev)
        {
            if (!(ev.Player is null) && !string.IsNullOrEmpty(ev.Player.UserId))
                LockedIds.Remove(ev.Player.UserId);
        }
    }
}