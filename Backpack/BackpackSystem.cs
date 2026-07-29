using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using EventHUD.Audio;
using EventHUD.Enums;
using EventHUD.Rpm;
using UnityEngine;

namespace EventHUD.Backpack
{
    public class BackpackSystem
    {
        public static BackpackSystem Instance { get; private set; }

        // Содержимое рюкзаков: серийник броника -> предметы
        private readonly Dictionary<ushort, List<Item>> contents = new Dictionary<ushort, List<Item>>();
        // Спрятанный основной инвентарь на время просмотра рюкзака (ключ — UserId)
        private readonly Dictionary<string, List<Item>> stash = new Dictionary<string, List<Item>>();
        // Кто сейчас в режиме просмотра рюкзака: UserId -> серийник броника
        private readonly Dictionary<string, ushort> viewMode = new Dictionary<string, ushort>();
        private readonly Dictionary<string, float> lastArmorClick = new Dictionary<string, float>();

        private static Config Cfg => Plugin.Instance.Config;

        /// <summary>Рюкзак активен, если включён хотя бы один RP-модуль (ev start/rpm all on) или идёт подготовка.</summary>
        private static bool RpActive
        {
            get
            {
                // Проверяем состояние ивента
                var state = EventManager.Instance.Session.State;
                if (state == EventState.Preparing || state == EventState.Starting || state == EventState.Running)
                    return true;

                // Проверяем, включён ли хоть один RP-модуль (rpm all on или ev start)
                if (RpModuleManager.Instance.IsEnabled(RpModuleType.Radio) ||
                    RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine))
                    return true;

                return false;
            }
        }

        public void Register()
        {
            Instance = this;
            Exiled.Events.Handlers.Player.DroppingItem += OnDroppingItem;
            Exiled.Events.Handlers.Player.PickingUpItem += OnPickingUpItem;
            Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;
            Exiled.Events.Handlers.Server.RoundStarted += ClearAll;
        }

        public void Unregister()
        {
            Exiled.Events.Handlers.Player.DroppingItem -= OnDroppingItem;
            Exiled.Events.Handlers.Player.PickingUpItem -= OnPickingUpItem;
            Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;
            Exiled.Events.Handlers.Server.RoundStarted -= ClearAll;
            Instance = null;
        }

        /// <summary>Принудительно закрыть все открытые рюкзаки (при ev stop / rpm all off / конце раунда).</summary>
        public void CloseAllOpen()
        {
            foreach (string userId in viewMode.Keys.ToList())
            {
                Player p = Player.Get(userId);
                if (p != null)
                    ExitView(p);
            }
        }

        private void ClearAll()
        {
            contents.Clear(); stash.Clear(); viewMode.Clear();
            lastArmorClick.Clear();
        }

        // ==== События ====

        private void OnDroppingItem(DroppingItemEventArgs ev)
        {
            if (!Cfg.BackpackEnabled || !RpActive || ev.Player == null || ev.Item == null) return;
            Player p = ev.Player;

            if (!IsArmorType(ev.Item.Type))
                return; // обычные предметы дропаются как в ванилле

            // двойной клик = реальный сброс броника
            if (lastArmorClick.TryGetValue(p.UserId, out float t) && Time.time - t <= Cfg.BackpackDoubleClickSeconds)
            {
                lastArmorClick.Remove(p.UserId);
                if (viewMode.ContainsKey(p.UserId)) ExitView(p);
                FileLog.Write($"[Backpack] {p.Nickname} сбросил броник {ev.Item.Type} (содержимое внутри)");
                return; // дроп разрешён
            }

            lastArmorClick[p.UserId] = Time.time;
            ev.IsAllowed = false;

            if (viewMode.ContainsKey(p.UserId)) ExitView(p);
            else EnterView(p, ev.Item);
        }

        private void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            if (ev.Player != null && viewMode.ContainsKey(ev.Player.UserId) && IsArmorType(ev.Pickup.Type))
                ev.IsAllowed = false;
        }

        private void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (ev.Player == null || !viewMode.ContainsKey(ev.Player.UserId)) return;

            if (stash.TryGetValue(ev.Player.UserId, out var hidden))
            {
                foreach (Item it in hidden)
                {
                    try { it.CreatePickup(ev.Player.Position); } catch { }
                }
                stash.Remove(ev.Player.UserId);
            }

            viewMode.Remove(ev.Player.UserId);
        }

        // ==== Логика ====

        private void EnterView(Player p, Item armor)
        {
            ushort serial = armor.Serial;
            var hidden = new List<Item>();

            foreach (Item it in p.Items.Where(i => i.Serial != serial).ToList())
            {
                p.RemoveItem(it, false);
                hidden.Add(it);
            }
            stash[p.UserId] = hidden;

            var list = GetContents(serial);
            foreach (Item it in list.ToList())
                p.AddItem(it);
            list.Clear();

            viewMode[p.UserId] = serial;
        }

        private void ExitView(Player p)
        {
            if (!viewMode.TryGetValue(p.UserId, out ushort serial)) return;

            Item armor = p.Items.FirstOrDefault(i => i.Serial == serial);
            int capacity = GetCapacity(armor?.Type ?? ItemType.None);
            var list = GetContents(serial);
            int used = 0;

            foreach (Item it in p.Items.Where(i => i.Serial != serial).ToList())
            {
                int cost = GetCost(it.Type);
                p.RemoveItem(it, false);

                if (!IsForbidden(it.Type) && used + cost <= capacity)
                {
                    list.Add(it);
                    used += cost;
                }
                else
                {
                    try { it.CreatePickup(p.Position); } catch { }
                }
            }

            if (stash.TryGetValue(p.UserId, out var hidden))
            {
                foreach (Item it in hidden)
                    p.AddItem(it);
                stash.Remove(p.UserId);
            }

            viewMode.Remove(p.UserId);
        }

        // ==== Хелперы ====

        private List<Item> GetContents(ushort serial)
        {
            if (!contents.TryGetValue(serial, out var list))
                contents[serial] = list = new List<Item>();
            return list;
        }

        private Item GetArmor(Player p) => p.Items.FirstOrDefault(i => IsArmorType(i.Type));
        private static bool IsArmorType(ItemType t) => Cfg.BackpackCapacity.ContainsKey(t);
        private static bool IsForbidden(ItemType t) => Cfg.BackpackForbidden.Contains(t);
        private static int GetCapacity(ItemType t) => Cfg.BackpackCapacity.TryGetValue(t, out int c) ? c : 0;
        private static int GetCost(ItemType t) => Cfg.BackpackSlotCost.TryGetValue(t, out int c) ? c : 1;
    }
}