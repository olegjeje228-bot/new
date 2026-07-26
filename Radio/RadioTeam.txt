namespace EventHUD.Radio
{
    public enum RadioTeam
    {
        Unknown,   // "?" — дефолт (Д-класс, случайные рации на полу)
        Facility,  // Комплекс общая
        Security,  // СБ
        Mtf,       // МОГ
        Chaos,     // ПХ
        Custom     // свободная частота (10–390), задаётся через SSS
    }
}
 