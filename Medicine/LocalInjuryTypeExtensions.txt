namespace EventHUD.Medicine
{
    public static class LocalInjuryTypeExtensions
    {
        public static string GetColor(this LocalInjuryType t) => t switch
        {
            LocalInjuryType.Bruise    => "#996600",
            LocalInjuryType.Gunshot   => "#990000",
            LocalInjuryType.Stab      => "#aa2200",
            LocalInjuryType.Chemical  => "#669900",
            LocalInjuryType.Corrosion => "#2d4a1e",
            LocalInjuryType.Burn      => "#cc4400",
            LocalInjuryType.Fracture  => "#660066",
            _                         => "#FFFFFF"
        };

        /// <summary>
        /// Понятное имя для HUD — без сокращений.
        /// </summary>
        public static string GetShortName(this LocalInjuryType t) => t switch
        {
            LocalInjuryType.Bruise    => "Ушиб",
            LocalInjuryType.Gunshot   => "Огнестрел",
            LocalInjuryType.Stab      => "Ножевая",
            LocalInjuryType.Chemical  => "Химическая",
            LocalInjuryType.Corrosion => "Коррозия",
            LocalInjuryType.Burn      => "Ожог",
            LocalInjuryType.Fracture  => "Перелом",
            _                         => "?"
        };

        public static string GetFullName(this LocalInjuryType t) => t switch
        {
            LocalInjuryType.Bruise    => "Ушиб",
            LocalInjuryType.Gunshot   => "Огнестрельная",
            LocalInjuryType.Stab      => "Ножевая",
            LocalInjuryType.Chemical  => "Химическая",
            LocalInjuryType.Corrosion => "Коррозия",
            LocalInjuryType.Burn      => "Ожоговая",
            LocalInjuryType.Fracture  => "Перелом",
            _                         => "?"
        };

        /// <summary>
        /// Приоритет для сортировки в HUD.
        /// </summary>
        public static int GetPriority(this LocalInjuryType t) => t switch
        {
            LocalInjuryType.Corrosion => 95,
            LocalInjuryType.Fracture  => 75,
            LocalInjuryType.Gunshot   => 70,
            LocalInjuryType.Stab      => 60,
            LocalInjuryType.Burn      => 55,
            LocalInjuryType.Chemical  => 50,
            LocalInjuryType.Bruise    => 30,
            _                         => 0
        };
    }
}
 