namespace EventHUD.SpecItems
{
    using System.Collections.Generic;
    using Exiled.API.Enums;
    using Exiled.API.Features;
    using Exiled.API.Features.Attributes;
    using Exiled.API.Features.Spawn;
    using Exiled.CustomItems.API.Features;
    using Exiled.Events.EventArgs.Player;
    using UnityEngine;

    [CustomItem(ItemType.GunCOM15)]
    public sealed class TowerTeleporter : CustomWeapon
    {
        private readonly Dictionary<ushort, bool> aiming = new Dictionary<ushort, bool>();

        private readonly Dictionary<string, Vector3> returnPoints = new Dictionary<string, Vector3>();

        private readonly HashSet<string> lockedByGun = new HashSet<string>();

        public override uint Id { get; set; } = 2;

        public override string Name { get; set; } = "Телепортер в башню";

        public override string Description { get; set; } = "Выстрел - телепорт в башню и обратно. С прицелом - ещё и блокировка инвентаря.";

        public override ItemType Type { get; set; } = ItemType.GunCOM15;

        public override float Weight { get; set; } = 0.6f;

        public override float Damage { get; set; } = 0f;

        public override byte ClipSize { get; set; } = 24;

        public override SpawnProperties SpawnProperties { get; set; } = new SpawnProperties
        {
            Limit = 0,
            DynamicSpawnPoints = new List<DynamicSpawnPoint>(),
        };

        public Vector3 TowerPosition { get; set; } = new Vector3(39f, 314f, -31f);

        public void OnAimingDownSight(AimingDownSightEventArgs ev)
        {
            if (ev.Firearm is null)
                return;

            aiming[ev.Firearm.Serial] = ev.AdsIn;
        }

        protected override void OnShooting(ShootingEventArgs ev)
        {
            ev.IsAllowed = false;

            Player player = ev.Player;

            if (player is null || string.IsNullOrEmpty(player.UserId))
                return;

            bool ads = false;

            if (!(ev.Firearm is null) && aiming.TryGetValue(ev.Firearm.Serial, out bool stored))
                ads = stored;

            bool alreadyInTower = returnPoints.ContainsKey(player.UserId);

            if (alreadyInTower)
            {
                Vector3 back = returnPoints[player.UserId];
                returnPoints.Remove(player.UserId);
                player.Position = back;

                if (lockedByGun.Remove(player.UserId))
                    InventoryLock.Unlock(player);

                player.ShowHint("<color=yellow>Возврат на исходную позицию</color>", 3f);
                SpecDebug.Log("ТЕЛЕПОРТЕР: " + player.Nickname + " вернулся");
                return;
            }

            returnPoints[player.UserId] = player.Position;
            player.Position = TowerPosition;

            if (ads)
            {
                lockedByGun.Add(player.UserId);
                InventoryLock.Lock(player);
                player.ShowHint("<color=red>Башня. Инвентарь заблокирован</color>", 4f);
            }
            else
            {
                player.ShowHint("<color=yellow>Башня</color>", 3f);
            }

            SpecDebug.Log("ТЕЛЕПОРТЕР: " + player.Nickname + " в башню, блокировка=" + ads);
        }
    }
}