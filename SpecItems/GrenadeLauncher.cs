namespace EventHUD.SpecItems
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Exiled.API.Enums;
    using Exiled.API.Features;
    using Exiled.API.Features.Attributes;
    using Exiled.API.Features.Items;
    using Exiled.API.Features.Pickups.Projectiles;
    using Exiled.API.Features.Spawn;
    using Exiled.CustomItems.API.Features;
    using MEC;
    using UnityEngine;

    [CustomItem(ItemType.MicroHID)]
    public sealed class GrenadeLauncher : CustomItem
    {
        private const string BroadcastOverheat = "<color=red>Гранатомёт перегрелся... охлаждение: {0} сек</color>";

        private const string BroadcastLag = "<color=red>Гранатомёт сломался... починка:  5 секунд</color>";

        private readonly Dictionary<ushort, State> states = new Dictionary<ushort, State>();

        private static bool dumped;

        private CoroutineHandle loop;

        private float globalBlockUntil;

        public override uint Id { get; set; } = 3;

        public override string Name { get; set; } = "Гранатомёт";

        public override string Description { get; set; } = "Зажми стрельбу - летит очередь гранат. Перегревается.";

        public override ItemType Type { get; set; } = ItemType.MicroHID;

        public override float Weight { get; set; } = 3f;

        public override SpawnProperties SpawnProperties { get; set; } = new SpawnProperties
        {
            Limit = 0,
            DynamicSpawnPoints = new List<DynamicSpawnPoint>(),
        };

        public int MaxPerPlayer { get; set; } = 3;

        public float FastRate { get; set; } = 5f;

        public float FastPhaseSeconds { get; set; } = 5f;

        public float SlowRate { get; set; } = 2f;

        public int HeatLimit { get; set; } = 50;

        public float OverheatSeconds { get; set; } = 15f;

        public float HeatResetIdleSeconds { get; set; } = 6f;

        public float GrenadeSpeed { get; set; } = 10f;

        public float GrenadeFuse { get; set; } = 2.5f;

        public float MinTps { get; set; } = 10f;

        public float LagPauseSeconds { get; set; } = 5f;

        protected override void SubscribeEvents()
        {
            base.SubscribeEvents();
            loop = Timing.RunCoroutine(Loop(), "eventhud-grenadelauncher");
        }

        protected override void UnsubscribeEvents()
        {
            Timing.KillCoroutines(loop);
            states.Clear();
            base.UnsubscribeEvents();
        }

        protected override void OnAcquired(Player player, Item item, bool displayMessage)
        {
            base.OnAcquired(player, item, displayMessage);

            int count = 0;

            foreach (Item owned in player.Items)
            {
                if (Check(owned))
                    count++;
            }

            if (count <= MaxPerPlayer)
                return;

            player.RemoveItem(item);
            player.ShowHint("<color=red>Максимум " + MaxPerPlayer + " гранатомёта в инвентаре</color>", 4f);
            SpecDebug.Log("ГРАНАТОМЁТ: отказ выдачи " + player.Nickname + ", уже есть " + MaxPerPlayer);
        }

        private IEnumerator<float> Loop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(0.05f);

                float now = Time.time;

                try
                {
                    if (Server.Tps < MinTps && now >= globalBlockUntil)
                    {
                        globalBlockUntil = now + LagPauseSeconds;
                        Map.Broadcast((ushort)LagPauseSeconds, BroadcastLag);
                        SpecDebug.Log("ГРАНАТОМЁТ: TPS " + Server.Tps.ToString("0.0") + ", пауза " + LagPauseSeconds + "с");
                    }

                    if (now < globalBlockUntil)
                        continue;

                    foreach (Player player in Player.List)
                    {
                        if (player is null || !player.IsAlive)
                            continue;

                        Item current = player.CurrentItem;

                        if (current is null || !Check(current))
                            continue;

                        Tick(player, current, now);
                    }
                }
                catch (Exception e)
                {
                    SpecDebug.Log("ГРАНАТОМЁТ ошибка цикла: " + e.Message);
                }
            }
        }

        private void Tick(Player player, Item item, float now)
        {
            State state;

            if (!states.TryGetValue(item.Serial, out state))
            {
                state = new State();
                states[item.Serial] = state;
            }

            if (now < state.OverheatUntil)
            {
                int left = Mathf.CeilToInt(state.OverheatUntil - now);

                if (left != state.LastShownSecond)
                {
                    state.LastShownSecond = left;
                    player.Broadcast(1, string.Format(BroadcastOverheat, left), global::Broadcast.BroadcastFlags.Normal, true);
                }

                return;
            }

            bool firing = IsFiring(item);

            if (!firing)
            {
                if (state.FiringSince > 0f && now - state.LastShot > HeatResetIdleSeconds)
                {
                    state.FiringSince = 0f;
                    state.Heat = 0;
                }

                return;
            }

            if (state.FiringSince <= 0f)
                state.FiringSince = now;

            float held = now - state.FiringSince;
            float rate = held <= FastPhaseSeconds ? FastRate : SlowRate;
            float interval = 1f / rate;

            if (now - state.LastShot < interval)
                return;

            state.LastShot = now;
            state.Heat++;

            Fire(player);

            if (state.Heat < HeatLimit)
                return;

            state.Heat = 0;
            state.FiringSince = 0f;
            state.OverheatUntil = now + OverheatSeconds;
            state.LastShownSecond = -1;
            SpecDebug.Log("ГРАНАТОМЁТ: перегрев у " + player.Nickname);
        }

        private void Fire(Player player)
        {
            try
            {
                Vector3 direction = player.CameraTransform.forward;
                Projectile projectile = player.ThrowGrenade(ProjectileType.FragGrenade, false).Projectile;

                if (projectile is null)
                    return;

                projectile.Position = player.CameraTransform.position + (direction * 0.8f);

                TimeGrenadeProjectile timed = projectile as TimeGrenadeProjectile;

                if (!(timed is null))
                    timed.FuseTime = GrenadeFuse;

                projectile.GameObject.AddComponent<NoPhysicsProjectile>();

                Rigidbody body = projectile.GameObject.GetComponent<Rigidbody>();

                if (!(body is null))
                {
                    body.useGravity = true;
                    body.velocity = direction * GrenadeSpeed;
                }
            }
            catch (Exception e)
            {
                SpecDebug.Log("ГРАНАТОМЁТ ошибка выстрела: " + e.Message);
            }
        }

        private static bool IsFiring(Item item)
        {
            try
            {
                object baseItem = item.Base;

                if (baseItem is null)
                    return false;

                System.Type type = baseItem.GetType();
                bool result = false;

                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!property.PropertyType.IsEnum || property.GetIndexParameters().Length > 0)
                        continue;

                    object value = null;

                    try
                    {
                        value = property.GetValue(baseItem, null);
                    }
                    catch
                    {
                        continue;
                    }

                    if (value is null)
                        continue;

                    string text = value.ToString();

                    if (!dumped)
                        SpecDebug.Log("MICROHID DUMP prop " + property.Name + " = " + text);

                    if (Looks(text))
                        result = true;
                }

                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!field.FieldType.IsEnum)
                        continue;

                    object value = null;

                    try
                    {
                        value = field.GetValue(baseItem);
                    }
                    catch
                    {
                        continue;
                    }

                    if (value is null)
                        continue;

                    string text = value.ToString();

                    if (!dumped)
                        SpecDebug.Log("MICROHID DUMP field " + field.Name + " = " + text);

                    if (Looks(text))
                        result = true;
                }

                if (!dumped)
                {
                    dumped = true;
                    SpecDebug.Log("MICROHID DUMP тип = " + type.FullName + " (если стрельба не ловится - пришли эти строки)");
                }

                return result;
            }
            catch
            {
                return false;
            }
        }

        private static bool Looks(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            if (text.IndexOf("Firing", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (text.IndexOf("Fired", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (text.IndexOf("Shooting", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        private sealed class State
        {
            public float FiringSince;

            public float LastShot;

            public int Heat;

            public float OverheatUntil;

            public int LastShownSecond = -1;
        }
    }
}