namespace EventHUD.Radio
{
    public static class RadioTeamExtensions
    {
        public static string GetDisplayName(this RadioTeam team) => team switch
        {
            RadioTeam.Unknown  => "?",
            RadioTeam.Facility => "Комплекс",
            RadioTeam.Security => "СБ",
            RadioTeam.Mtf      => "МОГ",
            RadioTeam.Chaos    => "ПХ",
            RadioTeam.Custom   => "Свободная",
            _                  => "?"
        };

        public static string GetColor(this RadioTeam team) => team switch
        {
            RadioTeam.Unknown  => "#FF5757",
            RadioTeam.Facility => "#FFFF7C",
            RadioTeam.Security => "#5B6370",
            RadioTeam.Mtf      => "#0096FF",
            RadioTeam.Chaos    => "#026100",
            RadioTeam.Custom   => "#FFFFFF",
            _                  => "#FFFFFF"
        };

        public static float GetFrequency(this RadioTeam team, Config c) => team switch
        {
            RadioTeam.Unknown  => c.RadioFreqUnknown,
            RadioTeam.Facility => c.RadioFreqFacility,
            RadioTeam.Security => c.RadioFreqSecurity,
            RadioTeam.Mtf      => c.RadioFreqMtf,
            RadioTeam.Chaos    => c.RadioFreqChaos,
            _                  => c.RadioFreqUnknown
        };
    }
}
 