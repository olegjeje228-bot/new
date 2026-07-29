namespace EventHUD.SpecItems
{
    using System.Collections.Generic;
    using Exiled.API.Features;
    using Exiled.API.Features.Attributes;
    using Exiled.API.Features.Spawn;
    using Exiled.CustomItems.API.Features;
    using Exiled.Events.EventArgs.Player;
    using UnityEngine;

    [CustomItem(ItemType.GunCOM15)]
    public sealed class TowerTeleporter : CustomWeapon
    {
        public static readonly Vector3 TowerPosition = new Vector3(39f, 314f, -31f);

        private const float UseCooldown = 1f;

        private static readonly Dictionary<string, Vector3> ReturnPoints = new Dictionary<string, Vector3>();

        private static readonly HashSet<string> LockedByGun = new HashSet<string>();

        private static readonly Dictionary<string, float> LastUse = new Dictionary<string, float>();

        private readonly HashSet<ushort> adsSerials = new HashSet<ushort>();

        public override uint Id { get; set; } = 2;

        public override ItemType Type { get; set; } = ItemType.GunCOM15;

        public override string Name { get; set; } = "Телепортер";

        public override string Description { get; set; } = "Телепорт в башню и обратно";

        public override float Weight { get; set; } = 0.6f;

        public override SpawnProperties SpawnProperties { get; set; } = new SpawnProperties();

        public override float Damage { get; set; } = 0f;

        public override byte ClipSize { get; set; } = 24;

        public static void ResetState()
        {
            ReturnPoints.Clear();
            LockedByGun.Clear();
            LastUse.Clear();
        }

        public void OnAimingDownSight(AimingDownSightEventArgs ev)
        {
            if (ev.Firearm == null || !Check(ev.Firearm))
                return;

            if (ev.AdsIn)
                adsSerials.Add(ev.Firearm.Serial);
            else
                adsSerials.Remove(ev.Firearm.Serial);
        }

        protected override void OnShooting(ShootingEventArgs ev)
        {
            ev.IsAllowed = false;

            Player player = ev.Player;

            if (player == null || !player.IsAlive || ev.Firearm == null)
                return;

            string id = player.UserId ?? player.Nickname;
            float now = Time.time;
            float last;

            if (LastUse.TryGetValue(id, out last) && now - last < UseCooldown)
                return;

            LastUse[id] = now;

            Vector3 back;

            if (ReturnPoints.TryGetValue(id, out back))
            {
                ReturnPoints.Remove(id);
                player.Position = back + Vector3.up * 0.1f;

                if (LockedByGun.Remove(id))
                    InventoryLock.Unlock(player);

                player.ShowHint("<color=green>Возврат</color>", 2f);
                SpecDebug.Log("ТЕЛЕПОРТЕР: возврат " + player.Nickname);
            }
            else
            {
                bool ads = adsSerials.Contains(ev.Firearm.Serial);

                ReturnPoints[id] = player.Position;
                player.Position = TowerPosition;

                if (ads)
                {
                    LockedByGun.Add(id);
                    InventoryLock.Lock(player, ev.Firearm.Serial);
                }

                player.ShowHint("<color=yellow>Башня. Выстрел из телепортера - возврат</color>", 3f);
                SpecDebug.Log("ТЕЛЕПОРТЕР: " + player.Nickname + " -> башня, ads=" + ads + ", serial=" + ev.Firearm.Serial);
            }
        }
    }
}