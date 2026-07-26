namespace EventHUD.SpecItems
{
    using System;
    using System.Collections.Generic;
    using Exiled.API.Enums;
    using Exiled.API.Features;
    using Exiled.API.Features.Attributes;
    using Exiled.API.Features.Doors;
    using Exiled.API.Features.Items;
    using Exiled.API.Features.Pickups.Projectiles;
    using Exiled.API.Features.Spawn;
    using Exiled.CustomItems.API.Features;
    using Exiled.Events.EventArgs.Player;
    using MEC;
    using UnityEngine;

    [CustomItem(ItemType.GunShotgun)]
    public sealed class GrenadeLauncherRp : CustomWeapon
    {
        private readonly Dictionary<ushort, bool> aiming = new Dictionary<ushort, bool>();

        private readonly Dictionary<ushort, int> loaded = new Dictionary<ushort, int>();

        private readonly HashSet<ushort> reloading = new HashSet<ushort>();

        public override uint Id { get; set; } = 4;

        public override string Name { get; set; } = "ГранатомётРП";

        public override string Description { get; set; } = "Заряжается гранатами. С прицелом - двойной усиленный заряд.";

        public override ItemType Type { get; set; } = ItemType.GunShotgun;

        public override float Weight { get; set; } = 2f;

        public override float Damage { get; set; } = 0f;

        public override byte ClipSize { get; set; } = 6;

        public override SpawnProperties SpawnProperties { get; set; } = new SpawnProperties
        {
            Limit = 0,
            DynamicSpawnPoints = new List<DynamicSpawnPoint>(),
        };

        public int StartLoaded { get; set; } = 2;

        public float ReloadInterval { get; set; } = 0.7f;

        public float NormalSpeed { get; set; } = 5f;

        public float AimingSpeed { get; set; } = 15f;

        public float NormalRecoil { get; set; } = 5f;

        public float AimingRecoil { get; set; } = 10f;

        public float FuseTime { get; set; } = 30f;

        public float ShotSoundVolume { get; set; } = 2f;

        public float ShotSoundRange { get; set; } = 20f;

        public float BlastSoundVolume { get; set; } = 5f;

        public float NearRadius { get; set; } = 5f;

        public float FarRadius { get; set; } = 10f;

        public float NearDamage { get; set; } = 30f;

        public float FarDamage { get; set; } = 2f;

        public void OnAimingDownSight(AimingDownSightEventArgs ev)
        {
            if (ev.Firearm is null)
                return;

            aiming[ev.Firearm.Serial] = ev.AdsIn;
        }

        protected override void OnAcquired(Player player, Item item, bool displayMessage)
        {
            base.OnAcquired(player, item, displayMessage);

            Firearm firearm = item as Firearm;

            if (firearm is null)
                return;

            if (!loaded.ContainsKey(firearm.Serial))
                loaded[firearm.Serial] = StartLoaded;

            Refresh(firearm);
        }

        protected override void OnReloading(ReloadingWeaponEventArgs ev)
        {
            ev.IsAllowed = false;

            Firearm firearm = ev.Firearm;

            if (firearm is null)
                return;

            if (reloading.Contains(firearm.Serial))
                return;

            reloading.Add(firearm.Serial);
            Timing.RunCoroutine(ReloadOneByOne(ev.Player, firearm));
        }

        protected override void OnShooting(ShootingEventArgs ev)
        {
            ev.IsAllowed = false;

            Player player = ev.Player;
            Firearm firearm = ev.Firearm;

            if (player is null || firearm is null)
                return;

            int ammo = GetLoaded(firearm);

            if (ammo <= 0)
            {
                player.ShowHint("Пусто. Нажми R и заряди гранатами", 2f);
                Refresh(firearm);
                return;
            }

            bool ads = aiming.TryGetValue(firearm.Serial, out bool stored) && stored;

            loaded[firearm.Serial] = ammo - 1;
            Refresh(firearm);

            SpecAudio.PlayAt(player.Position, "granatomet.ogg", ShotSoundVolume, ShotSoundRange);

            int shots = ads ? 2 : 1;
            float speed = ads ? AimingSpeed : NormalSpeed;

            for (int i = 0; i < shots; i++)
                Launch(player, speed, ads, i * 0.12f);

            ApplyRecoil(player, ads ? AimingRecoil : NormalRecoil);
        }

        private void Launch(Player player, float speed, bool ads, float spread)
        {
            try
            {
                Vector3 direction = player.CameraTransform.forward;

                if (spread > 0f)
                    direction = Quaternion.Euler(-spread * 10f, 0f, 0f) * direction;

                Projectile projectile = player.ThrowGrenade(ProjectileType.FragGrenade, false).Projectile;

                if (projectile is null)
                    return;

                projectile.Position = player.CameraTransform.position + (direction * 0.8f);

                TimeGrenadeProjectile timed = projectile as TimeGrenadeProjectile;

                if (!(timed is null))
                    timed.FuseTime = FuseTime;

                projectile.GameObject.AddComponent<NoPhysicsProjectile>();

                Rigidbody body = projectile.GameObject.GetComponent<Rigidbody>();

                if (!(body is null))
                {
                    body.useGravity = true;
                    body.velocity = direction * speed;
                }

                DoorBlast blast = projectile.GameObject.AddComponent<DoorBlast>();
                blast.Init(projectile, player, ads, this);
            }
            catch (Exception e)
            {
                SpecDebug.Log("ГРАНАТОМЁТРП ошибка выстрела: " + e.Message);
            }
        }

        public void Detonate(Vector3 position, Player shooter, bool ads, Door door)
        {
            if (ads)
                SpecAudio.PlayAt(position, "granatomet1.ogg", BlastSoundVolume, ShotSoundRange);

            foreach (Player victim in Player.List)
            {
                if (victim is null || !victim.IsAlive)
                    continue;

                float distance = Vector3.Distance(victim.Position, position);

                if (distance > FarRadius)
                    continue;

                float damage;

                if (distance <= NearRadius)
                {
                    damage = NearDamage;
                }
                else
                {
                    float t = (distance - NearRadius) / (FarRadius - NearRadius);
                    damage = Mathf.Lerp(NearDamage, FarDamage, t);
                }

                if (ads)
                    damage *= 2f;

                try
                {
                    victim.Hurt(damage, DamageType.Explosion);
                }
                catch
                {
                    victim.Hurt(damage);
                }
            }

            BreakableDoor breakable = door as BreakableDoor;

            if (!(breakable is null))
            {
                try
                {
                    breakable.Break();
                }
                catch
                {
                }
            }

            SpecDebug.Log("ГРАНАТОМЁТРП: взрыв на двери, ads=" + ads);
        }

        private IEnumerator<float> ReloadOneByOne(Player player, Firearm firearm)
        {
            try
            {
                while (true)
                {
                    if (player is null || !player.IsConnected)
                        yield break;

                    if (GetLoaded(firearm) >= ClipSize)
                        yield break;

                    Item grenade = FindGrenade(player);

                    if (grenade is null)
                    {
                        player.ShowHint("Нет гранат в инвентаре", 2f);
                        yield break;
                    }

                    player.RemoveItem(grenade);
                    loaded[firearm.Serial] = GetLoaded(firearm) + 1;
                    Refresh(firearm);

                    yield return Timing.WaitForSeconds(ReloadInterval);
                }
            }
            finally
            {
                reloading.Remove(firearm.Serial);
            }
        }

        private static Item FindGrenade(Player player)
        {
            foreach (Item item in player.Items)
            {
                if (item.Type == ItemType.GrenadeHE)
                    return item;
            }

            return null;
        }

        private int GetLoaded(Firearm firearm)
        {
            if (loaded.TryGetValue(firearm.Serial, out int value))
                return value;

            loaded[firearm.Serial] = StartLoaded;
            return StartLoaded;
        }

        private void Refresh(Firearm firearm)
        {
            int value = GetLoaded(firearm);

            if (value < 0)
                value = 0;

            if (value > ClipSize)
                value = ClipSize;

            firearm.MaxMagazineAmmo = ClipSize;
            firearm.MagazineAmmo = (byte)value;
        }

        private static void ApplyRecoil(Player player, float degrees)
        {
            try
            {
                Vector3 angles = player.CameraTransform.rotation.eulerAngles;
                player.Rotation = Quaternion.Euler(angles.x - degrees, angles.y, 0f);
            }
            catch (Exception e)
            {
                SpecDebug.Log("Отдача не применилась: " + e.Message);
            }
        }

        public sealed class DoorBlast : MonoBehaviour
        {
            private Projectile projectile;

            private Player shooter;

            private GrenadeLauncherRp owner;

            private bool ads;

            private bool done;

            private float armedAt;

            public void Init(Projectile projectile, Player shooter, bool ads, GrenadeLauncherRp owner)
            {
                this.projectile = projectile;
                this.shooter = shooter;
                this.ads = ads;
                this.owner = owner;
                armedAt = Time.time + 0.05f;
            }

            private void OnCollisionEnter(Collision collision)
            {
                Check(collision.collider);
            }

            private void OnTriggerEnter(Collider other)
            {
                Check(other);
            }

            private void Check(Collider collider)
            {
                if (done || Time.time < armedAt || collider is null)
                    return;

                Door door = NearestDoor(transform.position, 2.5f);

                if (door is null)
                    return;

                done = true;

                try
                {
                    owner.Detonate(transform.position, shooter, ads, door);

                    TimeGrenadeProjectile timed = projectile as TimeGrenadeProjectile;

                    if (!(timed is null) && !timed.IsAlreadyDetonated)
                        timed.Explode();
                }
                catch (Exception e)
                {
                    SpecDebug.Log("DoorBlast ошибка: " + e.Message);
                }
            }

            private static Door NearestDoor(Vector3 point, float maxDistance)
            {
                Door best = null;
                float bestDistance = float.MaxValue;

                foreach (Door door in Door.List)
                {
                    if (door is null)
                        continue;

                    float distance = Vector3.Distance(door.Position, point);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = door;
                    }
                }

                if (bestDistance > maxDistance)
                    return null;

                return best;
            }
        }
    }
}