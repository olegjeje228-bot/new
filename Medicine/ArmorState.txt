using System;
using System.Collections.Generic;

namespace EventHUD.Medicine
{
    public class ArmorState
    {
        public ArmorType Type { get; set; } = ArmorType.None;

        public float MaxDurability { get; set; }

        public float Durability { get; set; }

        public float BaseAbsorption { get; set; }

        /// <summary>
        /// 1 — отдельный лёгкий шлем цел.
        /// 0 — шлем пробит.
        /// Для обычных бронежилетов не используется.
        /// </summary>
        public int HelmetDurability { get; set; }

        public List<DateTime> BodyStunStacks { get; } =
            new List<DateTime>();

        public bool HasAsphyxia { get; set; }

        public bool HasLungBruise { get; set; }

        public DateTime? Scp106TouchTime { get; set; }

        public bool IsBroken
        {
            get
            {
                if (Type == ArmorType.Helmet)
                    return HelmetDurability <= 0;

                return Durability <= 0f;
            }
        }

        public float EffectiveAbsorption
        {
            get
            {
                if (Type == ArmorType.Helmet || Durability <= 0f)
                    return 0f;

                if (MaxDurability <= 0f)
                    return 0f;

                float percentage = Durability / MaxDurability;

                if (percentage > 0.7f)
                    return BaseAbsorption;

                if (percentage > 0.3f)
                    return BaseAbsorption - 5f;

                return BaseAbsorption - 15f;
            }
        }

        public string DurabilityStatus
        {
            get
            {
                if (Type == ArmorType.Helmet)
                {
                    return HelmetDurability > 0
                        ? "<color=#4CAF50>Цел</color>"
                        : "<color=#F44336>Пробит</color>";
                }

                if (Durability <= 0f)
                    return "<color=#FF0000>Разрушена</color>";

                if (MaxDurability <= 0f)
                    return "<color=#FF0000>Разрушена</color>";

                float percentage = Durability / MaxDurability;

                if (percentage > 0.7f)
                    return "<color=#00FF00>Цела</color>";

                if (percentage > 0.3f)
                    return "<color=#FFAA00>Повреждена</color>";

                return "<color=#FF4444>Критическая</color>";
            }
        }

        public void CleanExpiredStacks()
        {
            DateTime now = DateTime.UtcNow;

            BodyStunStacks.RemoveAll(
                timestamp => (now - timestamp).TotalSeconds >= 10.0);

            if (BodyStunStacks.Count < 4)
                HasLungBruise = false;

            if (BodyStunStacks.Count < 5)
                HasAsphyxia = false;
        }

        public int ActiveStacks
        {
            get
            {
                CleanExpiredStacks();
                return BodyStunStacks.Count;
            }
        }

        public void AddStun()
        {
            BodyStunStacks.Add(DateTime.UtcNow);

            if (ActiveStacks >= 5)
                HasAsphyxia = true;
            else if (ActiveStacks >= 4)
                HasLungBruise = true;
        }

        public void Reset()
        {
            Type = ArmorType.None;
            MaxDurability = 0f;
            Durability = 0f;
            BaseAbsorption = 0f;
            HelmetDurability = 0;

            BodyStunStacks.Clear();

            HasAsphyxia = false;
            HasLungBruise = false;
            Scp106TouchTime = null;
        }

        public void SetType(ArmorType type)
        {
            Type = type;
            MaxDurability = type.GetMaxDurability();
            Durability = MaxDurability;
            BaseAbsorption = type.GetBaseAbsorption();
            HelmetDurability = type == ArmorType.Helmet ? 1 : 0;
        }
    }

    public static class ArmorStorage
    {
        private static readonly Dictionary<string, ArmorState> States =
            new Dictionary<string, ArmorState>();

        public static ArmorState GetOrCreate(string userId)
        {
            if (!States.TryGetValue(userId, out ArmorState state))
            {
                state = new ArmorState();
                States[userId] = state;
            }

            return state;
        }

        public static bool TryGet(
            string userId,
            out ArmorState state)
        {
            return States.TryGetValue(userId, out state);
        }

        public static void Remove(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return;
            States.Remove(userId);
        }

        public static void ClearAll()
        {
            States.Clear();
        }
    }

    public static class ArmorItemDurabilityStorage
    {
        private static readonly Dictionary<ushort, float> Durability =
            new Dictionary<ushort, float>();

        private static readonly Dictionary<ushort, int> HelmetDurability =
            new Dictionary<ushort, int>();

        public static void Set(
            ushort serial,
            float durability,
            int helmetDurability)
        {
            Durability[serial] = durability;
            HelmetDurability[serial] = helmetDurability;
        }

        // Оставлено для совместимости со старым кодом.
        public static void Set(ushort serial, float durability)
        {
            Durability[serial] = durability;
        }

        public static bool TryGet(
            ushort serial,
            out float durability)
        {
            return Durability.TryGetValue(serial, out durability);
        }

        public static bool TryGetHelmetDurability(
            ushort serial,
            out int helmetDurability)
        {
            return HelmetDurability.TryGetValue(
                serial,
                out helmetDurability);
        }

        public static void ClearAll()
        {
            Durability.Clear();
            HelmetDurability.Clear();
        }
    }
}