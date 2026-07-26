namespace EventHUD.SpecItems
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Exiled.API.Enums;
    using Exiled.API.Features;
    using Exiled.API.Features.Attributes;
    using Exiled.API.Features.Items;
    using Exiled.API.Features.Pickups.Projectiles;
    using Exiled.API.Features.Spawn;
    using Exiled.CustomItems.API.Features;
    using Exiled.Events.EventArgs.Player;
    using InventorySystem.Items.MicroHID.Modules;
    using MEC;
    using UnityEngine;

    [CustomItem(ItemType.MicroHID)]
    public sealed class GrenadeLauncher : CustomItem
    {
        private const float GrenadeSpeed = 10f;
        private const float FastInterval = 0.2f;
        private const float SlowInterval = 0.5f;
        private const float FastPhaseDuration = 5f;
        private const int MaxPerMinute = 50;
        private const float OverheatSeconds = 15f;
        private const float TpsBrokenSeconds = 5f;
        private const int MaxInInventory = 3;

        private static readonly Dictionary<string, float> SessionStart = new Dictionary<string, float>();
        private static readonly Dictionary<string, float> NextShot = new Dictionary<string, float>();
        private static readonly Dictionary<string, Queue<float>> ShotTimes = new Dictionary<string, Queue<float>>();
        private static readonly Dictionary<string, float> OverheatUntil = new Dictionary<string, float>();

        private readonly HashSet<string> firingNow = new HashSet<string>();

        private CoroutineHandle loop;

        private float brokenUntil;

        public override uint Id { get; set; } = 3;

        public override ItemType Type { get; set; } = ItemType.MicroHID;

        public override string Name { get; set; } = "Гранатомёт";

        public override string Description { get; set; } = "МикроХИД, стреляющий гранатами";

        public override float Weight { get; set; } = 25f;

        public override SpawnProperties SpawnProperties { get; set; } = new SpawnProperties();

        protected override void SubscribeEvents()
        {
            Exiled.Events.Handlers.Player.ChangingMicroHIDState += OnChangingPhase;
            Exiled.Events.Handlers.Player.Hurting += OnHurting;
            loop = Timing.RunCoroutine(FireLoop());
            base.SubscribeEvents();
            SpecDebug.Log("ГРАНАТОМЁТ: SubscribeEvents");
        }

        protected override void UnsubscribeEvents()
        {
            Exiled.Events.Handlers.Player.ChangingMicroHIDState -= OnChangingPhase;
            Exiled.Events.Handlers.Player.Hurting -= OnHurting;
            Timing.KillCoroutines(loop);
            firingNow.Clear();
            base.UnsubscribeEvents();
            SpecDebug.Log("ГРАНАТОМЁТ: UnsubscribeEvents");
        }

        protected override void OnAcquired(Player player, Item item, bool displayMessage)
        {
            base.OnAcquired(player, item, displayMessage);

            if (player == null || item == null)
                return;

            ushort serial = item.Serial;

            Timing.CallDelayed(0.5f, () =>
            {
                try
                {
                    if (player == null || !player.IsConnected)
                        return;

                    int count = 0;

                    foreach (Item it in player.Items)
                    {
                        if (Check(it))
                            count++;
                    }

                    SpecDebug.Log("МИКРОХИД: у " + player.Nickname + " гранатомётов в инвентаре: " + count);

                    if (count <= MaxInInventory)
                        return;

                    Item extra = null;

                    foreach (Item it in player.Items)
                    {
                        if (it != null && it.Serial == serial)
                        {
                            extra = it;
                            break;
                        }
                    }

                    if (extra != null)
                    {
                        player.RemoveItem(extra);
                        player.ShowHint("<color=red>Максимум 3 гранатомёта в инвентаре</color>", 3f);
                        SpecDebug.Log("МИКРОХИД: лимит, удалён лишний serial " + serial);
                    }
                }
                catch (System.Exception e)
                {
                    SpecDebug.Log("МИКРОХИД лимит err: " + e.Message);
                }
            });
        }

        private void OnChangingPhase(ChangingMicroHIDStateEventArgs ev)
        {
            if (ev.Player == null || ev.MicroHID == null || !Check(ev.MicroHID))
                return;

            string id = ev.Player.UserId ?? ev.Player.Nickname;
            SpecDebug.Log("МИКРОХИД " + ev.Player.Nickname + " фаза -> " + ev.NewPhase);

            if (ev.NewPhase == MicroHidPhase.Firing)
            {
                float now = Time.time;

                if (brokenUntil > now)
                {
                    ev.IsAllowed = false;
                    return;
                }

                float until;

                if (OverheatUntil.TryGetValue(id, out until) && until > now)
                {
                    ev.IsAllowed = false;
                    return;
                }

                firingNow.Add(id);
                SessionStart[id] = now;
                NextShot[id] = now;
            }
            else
            {
                firingNow.Remove(id);
            }
        }

        private void OnHurting(HurtingEventArgs ev)
        {
            if (ev.Attacker == null || ev.DamageHandler == null)
                return;

            if (ev.DamageHandler.Type != DamageType.MicroHid)
                return;

            if (!Check(ev.Attacker.CurrentItem))
                return;

            ev.IsAllowed = false;
        }

        private IEnumerator<float> FireLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(0.05f);

                try
                {
                    Tick();
                }
                catch (Exception e)
                {
                    SpecDebug.Log("МИКРОХИД loop err: " + e.Message);
                }
            }
        }

        private void Tick()
        {
            float now = Time.time;

            if (brokenUntil > now)
                return;

            if (Server.Tps <= 10f && firingNow.Count > 0)
            {
                brokenUntil = now + TpsBrokenSeconds;
                firingNow.Clear();
                Map.Broadcast(5, "<color=red>Гранатомёт сломался... починка:  5 секунд</color>", global::Broadcast.BroadcastFlags.Normal, true);
                SpecDebug.Log("МИКРОХИД: TPS " + Server.Tps.ToString("0.0") + ", пауза 5 сек");
                return;
            }

            if (firingNow.Count == 0)
                return;

            List<string> ids = firingNow.ToList();

            foreach (string id in ids)
            {
                Player player = Player.List.FirstOrDefault(p => (p.UserId ?? p.Nickname) == id);

                if (player == null || !player.IsAlive || !Check(player.CurrentItem))
                {
                    firingNow.Remove(id);
                    continue;
                }

                MicroHid micro = player.CurrentItem as MicroHid;

                if (micro != null)
                {
                    try { micro.Energy = 1f; } catch { }
                }

                float next;
                NextShot.TryGetValue(id, out next);

                if (now < next)
                    continue;

                Queue<float> q;

                if (!ShotTimes.TryGetValue(id, out q))
                {
                    q = new Queue<float>();
                    ShotTimes[id] = q;
                }

                while (q.Count > 0 && now - q.Peek() > 60f)
                    q.Dequeue();

                if (q.Count >= MaxPerMinute)
                {
                    OverheatUntil[id] = now + OverheatSeconds;
                    firingNow.Remove(id);
                    Timing.RunCoroutine(OverheatBroadcast(player, id));
                    SpecDebug.Log("МИКРОХИД: перегрев у " + player.Nickname);
                    continue;
                }

                float started;
                SessionStart.TryGetValue(id, out started);

                float interval = now - started <= FastPhaseDuration ? FastInterval : SlowInterval;
                NextShot[id] = now + interval;
                q.Enqueue(now);
                FireGrenade(player);
            }
        }

        private IEnumerator<float> OverheatBroadcast(Player player, string id)
        {
            while (player != null && player.IsConnected)
            {
                float until;

                if (!OverheatUntil.TryGetValue(id, out until))
                    yield break;

                float remain = until - Time.time;

                if (remain <= 0f)
                {
                    player.Broadcast(2, "<color=green>МикроХИД остыл</color>", global::Broadcast.BroadcastFlags.Normal, true);
                    yield break;
                }

                player.Broadcast(1, "<color=orange>МикроХИД перегрелся! Ждать: " + Mathf.CeilToInt(remain) + " сек</color>", global::Broadcast.BroadcastFlags.Normal, true);
                yield return Timing.WaitForSeconds(1f);
            }
        }

        private void FireGrenade(Player player)
        {
            try
            {
                Projectile projectile = player.ThrowGrenade(ProjectileType.FragGrenade, false).Projectile;

                TimeGrenadeProjectile timed = projectile as TimeGrenadeProjectile;

                if (timed != null)
                    timed.FuseTime = 3f;

                Vector3 direction = player.CameraTransform.forward;
                projectile.Position = player.CameraTransform.position + direction * 0.7f;

                Rigidbody body = projectile.GameObject.GetComponent<Rigidbody>();

                if (body != null)
                {
                    body.velocity = direction * GrenadeSpeed;
                    body.angularVelocity = Vector3.zero;
                }

                projectile.GameObject.AddComponent<NoPhysicsProjectile>();
            }
            catch (Exception e)
            {
                SpecDebug.Log("МИКРОХИД выстрел err: " + e.Message);
            }
        }
    }
}