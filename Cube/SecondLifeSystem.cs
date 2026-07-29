using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using UnityEngine;

namespace EventHUD.Cube
{
    public class SecondLifeSystem
    {
        public static SecondLifeSystem Instance { get; private set; }

        // Хук для куба: в CubeLootSystem.Register() добавь строку
        //   SecondLifeSystem.CanUse = p => /* твоя проверка участника куба */;
        // Пока не подключено - команда доступна всем.
        public static Func<Player, bool> CanUse = _ => true;

        private class State
        {
            public int LivesLeft;
            public int TotalLives;
            public readonly List<(float time, Vector3 pos)> Trail = new List<(float, Vector3)>();
            public bool PendingRevive;

            public RoleTypeId Role;
            public Vector3 RevivePos;
            public float MaxHealth;
            public float Ahp;
            public readonly List<Item> Items = new List<Item>();
            public Dictionary<AmmoType, ushort> Ammo = new Dictionary<AmmoType, ushort>();
            public List<(EffectType type, byte intensity, float duration)> Effects =
                new List<(EffectType, byte, float)>();
        }

        private readonly Dictionary<string, State> _states = new Dictionary<string, State>();
        private CoroutineHandle _trailHandle;

        public void Register()
        {
            Instance = this;
            Exiled.Events.Handlers.Player.Dying += OnDying;
            Exiled.Events.Handlers.Player.Died += OnDied;
            Exiled.Events.Handlers.Server.RoundStarted += ClearAll;
            _trailHandle = Timing.RunCoroutine(TrailLoop());
        }

        public void Unregister()
        {
            Exiled.Events.Handlers.Player.Dying -= OnDying;
            Exiled.Events.Handlers.Player.Died -= OnDied;
            Exiled.Events.Handlers.Server.RoundStarted -= ClearAll;
            Timing.KillCoroutines(_trailHandle);
            Instance = null;
        }

        private void ClearAll() => _states.Clear();

        private State Get(string userId)
        {
            if (!_states.TryGetValue(userId, out State s))
                _states[userId] = s = new State();
            return s;
        }

        // ==== Публичное API для команды ====

        public bool TryActivate(Player p, out string response)
        {
            if (!CanUse(p))
            {
                response = "Вторая жизнь доступна только на кубе.";
                return false;
            }

            Item held = p.CurrentItem;
            if (held == null || held.Type != ItemType.SCP500)
            {
                response = "Возьмите SCP-500 в руки.";
                return false;
            }

            p.RemoveItem(held);
            State st = Get(p.UserId);
            st.LivesLeft++;
            st.TotalLives++;

            response = $"Вторая жизнь активирована. Осталось: {st.LivesLeft}";
            return true;
        }

        public string GetStat(Player p)
        {
            State st = Get(p.UserId);
            return $"Осталось: {st.LivesLeft}\nВсего жизней: {st.TotalLives}";
        }

        // ==== Позиция 3 секунды назад ====

        private IEnumerator<float> TrailLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(0.5f);
                float now = Time.time;

                foreach (Player p in Player.List)
                {
                    if (p == null || !p.IsAlive) continue;
                    if (!_states.TryGetValue(p.UserId, out State st) || st.LivesLeft <= 0) continue;

                    st.Trail.Add((now, p.Position));
                    while (st.Trail.Count > 0 && now - st.Trail[0].time > 5f)
                        st.Trail.RemoveAt(0);
                }
            }
        }

        private static Vector3 PosSecondsAgo(State st, float seconds, Vector3 fallback)
        {
            float target = Time.time - seconds;
            for (int i = 0; i < st.Trail.Count; i++)
                if (st.Trail[i].time >= target)
                    return st.Trail[i].pos;
            return st.Trail.Count > 0 ? st.Trail[0].pos : fallback;
        }

        // ==== Смерть и возрождение ====

        private void OnDying(DyingEventArgs ev)
        {
            if (ev.Player == null) return;
            if (!_states.TryGetValue(ev.Player.UserId, out State st) || st.LivesLeft <= 0 || st.PendingRevive)
                return;

            Player p = ev.Player;

            st.Role = p.Role.Type;
            st.RevivePos = PosSecondsAgo(st, 3f, p.Position);
            st.MaxHealth = p.MaxHealth;
            st.Ahp = p.ArtificialHealth;

            st.Ammo = new Dictionary<AmmoType, ushort>();
            foreach (var kv in p.Ammo)
                st.Ammo[kv.Key.GetAmmoType()] = kv.Value;

            st.Effects = new List<(EffectType, byte, float)>();
            foreach (var e in p.ActiveEffects)
            {
                try { st.Effects.Add((e.GetEffectType(), e.Intensity, e.Duration)); }
                catch { }
            }

            // Прячем предметы ТЕМИ ЖЕ экземплярами - серийники сохранятся,
            // содержимое рюкзака (оно по серийнику броника) не потеряется.
            st.Items.Clear();
            foreach (Item it in p.Items.ToList())
            {
                p.RemoveItem(it, false);
                st.Items.Add(it);
            }
            ev.ItemsToDrop.Clear(); // на землю ничего не падает

            st.LivesLeft--;
            st.PendingRevive = true;
        }

        private void OnDied(DiedEventArgs ev)
        {
            if (ev.Player == null) return;
            if (!_states.TryGetValue(ev.Player.UserId, out State st) || !st.PendingRevive)
                return;

            Player p = ev.Player;
            Timing.CallDelayed(0.5f, () =>
            {
                st.PendingRevive = false;
                p.Role.Set(st.Role, SpawnReason.Revived, RoleSpawnFlags.None);

                Timing.CallDelayed(0.4f, () =>
                {
                    try
                    {
                        p.Position = st.RevivePos;
                        p.ClearInventory();

                        foreach (Item it in st.Items)
                            p.AddItem(it);
                        st.Items.Clear();

                        p.MaxHealth = st.MaxHealth;
                        p.Health = st.MaxHealth;
                        if (st.Ahp > 0)
                            p.ArtificialHealth = st.Ahp;

                        foreach (var kv in st.Ammo)
                            p.SetAmmo(kv.Key, kv.Value);

                        foreach (var ef in st.Effects)
                        {
                            try
                            {
                                p.EnableEffect(ef.type, ef.duration);
                                p.ChangeEffectIntensity(ef.type, ef.intensity, ef.duration);
                            }
                            catch { }
                        }

                        p.Broadcast(6, "<color=green>Вторая жизнь! Вы возрождены.</color>");
                    }
                    catch (Exception e)
                    {
                        Log.Warn("[2Life] Ошибка возрождения: " + e.Message);
                    }
                });
            });
        }
    }
}