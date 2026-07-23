namespace EventHUD.Medicine
{
    public static class ArmorTypeExtensions
    {
        public static string GetDisplayName(this ArmorType type)
        {
            return type switch
            {
                ArmorType.Helmet => "Лёгкий шлем",
                ArmorType.Light => "Лёгкий",
                ArmorType.Combat => "Боевой",
                ArmorType.Heavy => "Тяжёлый",
                ArmorType.Tank => "Танковый",
                _ => "Нет"
            };
        }

        public static float GetMaxDurability(this ArmorType type)
        {
            return type switch
            {
                // Шлем использует HelmetDurability, а не общую прочность.
                ArmorType.Helmet => 1f,
                ArmorType.Light => 30f,
                ArmorType.Combat => 90f,
                ArmorType.Heavy => 150f,
                ArmorType.Tank => 210f,
                _ => 0f
            };
        }

        public static float GetBaseAbsorption(this ArmorType type)
        {
            return type switch
            {
                ArmorType.Helmet => 0f,
                ArmorType.Light => 30f,
                ArmorType.Combat => 45f,
                ArmorType.Heavy => 60f,
                ArmorType.Tank => 75f,
                _ => 0f
            };
        }

        public static float GetIntactDamageReduction(this ArmorType type)
        {
            return type switch
            {
                ArmorType.Helmet => 0f,
                ArmorType.Light => 0.45f,
                ArmorType.Combat => 0.62f,
                ArmorType.Heavy => 0.78f,
                ArmorType.Tank => 0.90f,
                _ => 0f
            };
        }

        public static float GetArmorShield(this ArmorType type)
        {
            return type switch
            {
                ArmorType.Helmet => 0f,
                ArmorType.Light => 20f,
                ArmorType.Combat => 40f,
                ArmorType.Heavy => 60f,
                ArmorType.Tank => 80f,
                _ => 0f
            };
        }

        public static float GetLimbMultiplier(this ArmorType type)
        {
            return type switch
            {
                ArmorType.Tank => 0.5f,
                _ => 0f
            };
        }

        public static bool HasHelmet(this ArmorType type)
        {
            return type switch
            {
                ArmorType.Helmet => true,
                ArmorType.Combat => true,
                ArmorType.Heavy => true,
                ArmorType.Tank => true,
                _ => false
            };
        }

        public static float GetHelmetThreshold(this ArmorType type)
        {
            return type switch
            {
                // Для отдельного шлема порог не используется:
                // он обрабатывается раньше общей логики.
                ArmorType.Helmet => 0f,
                ArmorType.Combat => 38f,
                ArmorType.Heavy => 50f,
                ArmorType.Tank => 20f,
                _ => 0f
            };
        }
    }
}