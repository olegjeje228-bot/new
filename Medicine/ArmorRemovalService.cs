// FIXED for EXILED 9.14.2
// Если у тебя namespace SCPSLSERVER — замени EventHUD -> SCPSLSERVER
using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using MEC;
using UnityEngine;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Снятие пробитого бронежилета.
    /// Самому: лёгкий 3с, боевой 4с, тяжёлый 6с, танковый 10с.
    /// Другой человек помогает: время / 3.
    /// </summary>
    public class ArmorRemovalState
    {
        public bool IsRemoving;
        public float Elapsed;
        public float Duration;
        public Player Helper; // null = сам снимает
    }

    public static class ArmorRemovalStorage
    {
        private static readonly Dictionary<string, ArmorRemovalState> _states = new();

        public static ArmorRemovalState GetOrCreate(string userId)
        {
            if (!_states.TryGetValue(userId, out var s))
            {
                s = new ArmorRemovalState();
                _states[userId] = s;
            }
            return s;
        }

        public static bool TryGet(string userId, out ArmorRemovalState s) =>
            _states.TryGetValue(userId, out s);

        public static void Remove(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return;
            _states.Remove(userId);
        }
        public static void ClearAll() => _states.Clear();
    }

    public static class ArmorRemovalHelper
    {
        public static float GetRemovalTime(ArmorType type, bool hasHelper)
        {
            float baseTime = type switch
            {
                ArmorType.Light => 3f,
                ArmorType.Combat => 4f,
                ArmorType.Heavy => 6f,
                ArmorType.Tank => 10f,
                _ => 2f
            };
            return hasHelper ? baseTime / 3f : baseTime;
        }

        public static void StartRemoval(Player victim, Player helper = null)
        {
            var armor = ArmorStorage.GetOrCreate(victim.UserId);
            if (armor.Type == ArmorType.None)
                return;

            var removal = ArmorRemovalStorage.GetOrCreate(victim.UserId);
            if (removal.IsRemoving)
                return;

            removal.IsRemoving = true;
            removal.Elapsed = 0;
            removal.Duration = GetRemovalTime(armor.Type, helper != null);
            removal.Helper = helper;

            Timing.RunCoroutine(RemovalCoroutine(victim, removal));
        }

        private static IEnumerator<float> RemovalCoroutine(Player victim, ArmorRemovalState state)
        {
            while (state.Elapsed < state.Duration)
            {
                yield return Timing.WaitForSeconds(0.5f);
                state.Elapsed += 0.5f;

                if (victim == null || !victim.IsAlive)
                {
                    state.IsRemoving = false;
                    yield break;
                }

                if (state.Helper != null && (!state.Helper.IsAlive ||
                    Vector3.Distance(victim.Position, state.Helper.Position) > 2f))
                {
                    state.Helper = null;
                    var armor = ArmorStorage.GetOrCreate(victim.UserId);
                    state.Duration = GetRemovalTime(armor.Type, false);
                }
            }

            var armorState = ArmorStorage.GetOrCreate(victim.UserId);
            armorState.Reset();

            // Дропаем ванильный предмет брони
            // EXILED 9: Player.Items = IEnumerable<Item>, Item.Type = ItemType
            foreach (Item item in victim.Items)
            {
                if (item.Type == ItemType.ArmorLight ||
                    item.Type == ItemType.ArmorCombat ||
                    item.Type == ItemType.ArmorHeavy)
                {
                    victim.RemoveItem(item);
                    break;
                }
            }

            state.IsRemoving = false;
            ArmorRemovalStorage.Remove(victim.UserId);
        }
    }
}
