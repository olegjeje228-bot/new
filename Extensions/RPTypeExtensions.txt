using EventHUD.Enums;

namespace EventHUD.Extensions
{
    public static class RPTypeExtensions
    {
        public static string GetColor(this RPType type) => type switch
        {
            RPType.NONRP    => "#00FF00",
            RPType.LIGHTRP  => "#90D5FF",
            RPType.FUNRP    => "#FFFFFF",
            RPType.MEDIUMRP => "#FFFF00",
            RPType.HARDRP   => "#FF0000",
            RPType.FULLRP   => "#950606",
            _               => "#FFFFFF"
        };

        public static string GetShortName(this RPType type) => type switch
        {
            RPType.NONRP    => "NRP",
            RPType.LIGHTRP  => "LRP",
            RPType.FUNRP    => "FUNRP",
            RPType.MEDIUMRP => "MRP",
            RPType.HARDRP   => "HRP",
            RPType.FULLRP   => "FRP",
            _               => type.ToString()
        };

        /// <summary>
        /// Множитель времени лечения/осмотра в зависимости от RP-уровня.
        /// LRP/FUNRP/NONRP = 0.5x (в 2 раза быстрее)
        /// MRP = 0.66x (в 1.5 раза быстрее)
        /// HRP/FRP = 1.0x
        /// </summary>
        public static float GetTimeMultiplier(this RPType type) => type switch
        {
            RPType.LIGHTRP  => 0.5f,
            RPType.FUNRP    => 0.5f,
            RPType.NONRP    => 0.5f,
            RPType.MEDIUMRP => 0.66f,
            RPType.HARDRP   => 1.0f,
            RPType.FULLRP   => 1.0f,
            _               => 1.0f
        };

        /// <summary>Лёгкий РП — менее серьёзные последствия (NONRP/LIGHTRP/FUNRP).</summary>
        public static bool IsLightRp(this RPType type) =>
            type == RPType.NONRP || type == RPType.LIGHTRP || type == RPType.FUNRP;

        /// <summary>
        /// Бонус к защите брони (доля) на лёгких РП — броня спасает сильнее.
        /// </summary>
        public static float GetArmorReductionBonus(this RPType type) => type switch
        {
            RPType.NONRP    => 0.20f,
            RPType.LIGHTRP  => 0.15f,
            RPType.FUNRP    => 0.15f,
            RPType.MEDIUMRP => 0.08f,
            RPType.HARDRP   => 0f,
            RPType.FULLRP   => 0f,
            _               => 0f
        };

        /// <summary>
        /// Разрешает ли РП выжить при хедшоте в боевом бронике.
        /// На серьёзных РП (HARD/FULL) боевой шлем не спасает от хедшота.
        /// </summary>
        public static bool CombatSurvivesHeadshot(this RPType type) => type switch
        {
            RPType.NONRP    => true,
            RPType.LIGHTRP  => true,
            RPType.FUNRP    => true,
            RPType.MEDIUMRP => true,
            _               => false
        };

        public static bool TryParse(string input, out RPType type)
        {
            type = RPType.NONRP;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            switch (input.Trim().ToUpperInvariant())
            {
                case "NRP":
                case "NONRP":
                    type = RPType.NONRP;   return true;
                case "LRP":
                case "LIGHTRP":
                    type = RPType.LIGHTRP; return true;
                case "FUNRP":
                    type = RPType.FUNRP;   return true;
                case "MRP":
                case "MEDIUMRP":
                    type = RPType.MEDIUMRP; return true;
                case "HRP":
                case "HARDRP":
                    type = RPType.HARDRP;  return true;
                case "FRP":
                case "FULLRP":
                    type = RPType.FULLRP;  return true;
                default:
                    return false;
            }
        }
    }
}
 