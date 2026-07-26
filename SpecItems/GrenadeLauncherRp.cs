namespace EventHUD.SpecItems
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        private const int MaxLoaded = 6;
        private const float NormalSpeed = 5f;
        private const float AdsSpeed = 15f;
        private const float FuseSeconds = 3f;

        private static readonly Dictionary<ushort, int> Loaded = new Dictionary<ushort, int>();

        private readonly HashSet<ushort> adsSerials = new HashSet<ushort>();

        private readonly HashSet<ushort> reloadingNow = new HashSet<ushort>();

        public override uint Id { get; set; } = 4;

        public override string Name { get; set; } = "ГранатомётРП";

        public override string Description { get; set; } = "Дробовик, заряжаемый гранатами";

        public override float Weight { get; set; } = 2.5f;

        public override SpawnProperties SpawnProperties { get; set; } = new SpawnProperties();

        public override float Damage { get; set; } = 0f;

        public override byte ClipSize { get; set; } = 6;

        public void OnAimingDownSight(AimingDownSightEventArgs ev)
        {
            if (ev.Firearm == null || !Check(ev.Firearm))
                return;

            if (ev.AdsIn)
                adsSerials.Add(ev.Firearm.Serial);
            else
                adsSerials.Remove(ev.Firearm.Serial);
        }

        protected override void OnReloading(ReloadingWeaponEventArgs ev)
        {
            ev.IsAllowed = false;

            if (ev.Player == null || ev.Firearm == null)
                return;

            if (reloadingNow.Contains(ev.Firearm.Serial))
                return;

            Timing.RunCoroutine(ReloadOneByOne(ev.Player, ev.Firearm.Serial));
        }

        private IEnumerator<float> ReloadOneByOne(Player player, ushort serial)
        {
            reloadingNow.Add(serial);

            while (true)
            {
                if (player == null || !player.IsAlive || player.CurrentItem == null || player.CurrentItem.Serial != serial)
                    break;

                int count;
                Loaded.TryGetValue(serial, out count);

                if (count >= MaxLoaded)
                {
                    player.ShowHint("Заряжен полностью: " + count + "/" + MaxLoaded, 2f);
                    break;
                }

                Item grenade = player.Items.FirstOrDefault(i => i.Type == ItemType.GrenadeHE);

                if (grenade == null)
                {
                    player.ShowHint("<color=yellow>Нет гранат в инвентаре</color>", 2f);
                    break;
                }

                yield return Timing.WaitForSeconds(0.9f);

                if (player == null || !player.IsAlive || player.CurrentItem == null || player.CurrentItem.Serial != serial)
                    break;

                grenade = player.Items.FirstOrDefault(i => i.Type == ItemType.GrenadeHE);

                if (grenade == null)
                    break;

                player.RemoveItem(grenade);
                Loaded.TryGetValue(serial, out count);
                count++;
                Loaded[serial] = count;
                player.ShowHint("Заряжено: " + count + "/" + MaxLoaded, 1.5f);
                SpecDebug.Log("ГРП: заряжено " + count + "/" + MaxLoaded + " у " + player.Nickname);
            }

            reloadingNow.Remove(serial);
        }

        protected override void OnShooting(ShootingEventArgs ev)
        {
            ev.IsAllowed = false;

            Player player = ev.Player;

            if (player == null || ev.Firearm == null)
                return;

            ushort serial = ev.Firearm.Serial;
            int count;
            Loaded.TryGetValue(serial, out count);

            if (count <= 0)
            {
                player.ShowHint("<color=yellow>Пусто! Перезарядка: R (нужны гранаты)</color>", 2f);
                return;
            }

            bool ads = adsSerials.Contains(serial);
            Vector3 direction = player.CameraTransform.forward;

            SpecAudio.PlayAt(player.Position, "granatomet.ogg", 2f, 20f);

            if (ads && count >= 2)
            {
                Loaded[serial] = count - 2;
                FireOne(player, Quaternion.AngleAxis(-1.5f, player.CameraTransform.up) * direction, AdsSpeed, true);
                FireOne(player, Quaternion.AngleAxis(1.5f, player.CameraTransform.up) * direction, AdsSpeed, true);
                Recoil(player, 10f);
            }
            else
            {
                Loaded[serial] = count - 1;
                float speed = ads ? AdsSpeed : NormalSpeed;
                FireOne(player, direction, speed, ads);
                Recoil(player, ads ? 10f : 5f);
            }

            SpecDebug.Log("ГРП: выстрел, ads=" + ads + ", осталось " + Loaded[serial]);
        }

        private static void FireOne(Player player, Vector3 direction, float speed, bool ads)
        {
            try
            {
                Projectile projectile = player.ThrowGrenade(ProjectileType.FragGrenade, false).Projectile;

                TimeGrenadeProjectile timed = projectile as TimeGrenadeProjectile;

                if (timed != null)
                    timed.FuseTime = FuseSeconds;

                projectile.Position = player.CameraTransform.position + direction * 0.7f;

                Rigidbody body = projectile.GameObject.GetComponent<Rigidbody>();

                if (body != null)
                {
                    body.velocity = direction * speed;
                    body.angularVelocity = Vector3.zero;
                }

                projectile.GameObject.AddComponent<NoPhysicsProjectile>();

                DoorDetonator detonator = projectile.GameObject.AddComponent<DoorDetonator>();
                detonator.Init(projectile, player, ads);
            }
            catch (Exception e)
            {
                SpecDebug.Log("ГРП выстрел err: " + e.Message);
            }
        }

        private static void Recoil(Player player, float degreesUp)
        {
            try
            {
                Vector3 angles = player.CameraTransform.rotation.eulerAngles;
                player.Rotation = Quaternion.Euler(angles.x - degreesUp, angles.y, angles.z);
            }
            catch (Exception e)
            {
                SpecDebug.Log("ГРП recoil err: " + e.Message);
            }
        }
    }

    public sealed class DoorDetonator : MonoBehaviour
    {
        private Exiled.API.Features.Pickups.Projectiles.Projectile projectile;

        private Exiled.API.Features.Player attacker;

        private bool ads;

        private bool done;

        public void Init(Exiled.API.Features.Pickups.Projectiles.Projectile proj, Exiled.API.Features.Player shooter, bool aimed)
        {
            projectile = proj;
            attacker = shooter;
            ads = aimed;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision != null && collision.collider != null)
                TryDoor(collision.collider);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryDoor(other);
        }

        private void TryDoor(Collider collider)
        {
            if (done || collider == null)
                return;

            Interactables.Interobjects.DoorUtils.DoorVariant doorVariant =
                collider.GetComponentInParent<Interactables.Interobjects.DoorUtils.DoorVariant>();

            if (doorVariant == null)
                return;

            done = true;
            Vector3 position = transform.position;
            SpecDebug.Log("ГРП: контакт с дверью, мгновенная детонация");

            try
            {
                Door door = Door.Get(doorVariant);
                BreakableDoor breakable = door as BreakableDoor;

                if (breakable != null && !breakable.IsDestroyed)
                    breakable.Break();
            }
            catch (Exception e)
            {
                SpecDebug.Log("ГРП: поломка двери err " + e.Message);
            }

            foreach (Player target in Player.List)
            {
                if (target == null || !target.IsAlive)
                    continue;

                float distance = Vector3.Distance(target.Position, position);

                if (distance > 10f)
                    continue;

                float damage = distance <= 5f ? 30f : Mathf.Lerp(30f, 2f, (distance - 5f) / 5f);

                try
                {
                    target.Hurt(attacker, damage, DamageType.Explosion);
                    SpecDebug.Log("ГРП: урон " + damage.ToString("0") + " -> " + target.Nickname + " (" + distance.ToString("0.0") + " м)");
                }
                catch (Exception e)
                {
                    SpecDebug.Log("ГРП: урон err " + e.Message);
                }
            }

            try
            {
                Map.ExplodeEffect(position, ProjectileType.FragGrenade);
            }
            catch (Exception e)
            {
                SpecDebug.Log("ГРП: эффект err " + e.Message);
            }

            if (ads)
                SpecAudio.PlayAt(position, "granatomet1.ogg", 5f, 20f);

            try
            {
                if (projectile != null)
                    projectile.Destroy();
                else
                    Destroy(gameObject);
            }
            catch
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (done)
                return;

            done = true;

            if (ads)
                SpecAudio.PlayAt(transform.position, "granatomet1.ogg", 5f, 20f);
        }
    }
}