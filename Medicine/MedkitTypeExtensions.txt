namespace EventHUD.Medicine
{
    public static class MedkitTypeExtensions
    {
        public static string GetDisplayName(this MedkitType t) => t switch
        {
            MedkitType.Civilian   => "Гражданская",
            MedkitType.Industrial => "Рабочая",
            MedkitType.Military   => "Военная (IFAK)",
            MedkitType.Paramedic  => "Парамедика",
            _                     => "?"
        };

        public static string GetColor(this MedkitType t) => t switch
        {
            MedkitType.Civilian   => "#88CC88",
            MedkitType.Industrial => "#CCAA44",
            MedkitType.Military   => "#CC4444",
            MedkitType.Paramedic  => "#4488FF",
            _                     => "#FFFFFF"
        };
    }
}
 