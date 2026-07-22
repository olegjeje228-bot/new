namespace EventHUD.Medicine
{
    public static class GlobalConditionExtensions
    {
        public static string GetColor(this GlobalCondition c) => c switch
        {
            GlobalCondition.Normal              => "#ebffea",
            GlobalCondition.Adrenaline          => "#afffa3",
            GlobalCondition.AdrenalineOverdose  => "#d0d100",
            GlobalCondition.Painkiller          => "#4cc800",
            GlobalCondition.PainkillerOverdose  => "#d0d100",
            GlobalCondition.BleedingLight       => "#9d2f00",
            GlobalCondition.BleedingMedium      => "#cb0000",
            GlobalCondition.BleedingHeavy       => "#ee0000",
            GlobalCondition.Concussion          => "#2e0093",
            GlobalCondition.ConcussionSevere    => "#ec48be",
            GlobalCondition.CardiacArrest       => "#ce0808",
            GlobalCondition.LethalHeadshot     => "#FF0000",
            GlobalCondition.Under1853          => "#4CAF50",
            GlobalCondition.Poisoned           => "#8BC34A",
            _                                   => "#FFFFFF"
        };

        /// <summary>
        /// Имя для HUD — понятное игроку, без сокращений.
        /// </summary>
        public static string GetShortName(this GlobalCondition c) => c switch
        {
            GlobalCondition.Normal              => "Норма",
            GlobalCondition.Adrenaline          => "Адреналин",
            GlobalCondition.AdrenalineOverdose  => "Передоз адреналином",
            GlobalCondition.Painkiller          => "Обезболивающее",
            GlobalCondition.PainkillerOverdose  => "Передоз обезболом",
            GlobalCondition.BleedingLight       => "Кровотечение (кап.)",
            GlobalCondition.BleedingMedium      => "Кровотечение (вен.)",
            GlobalCondition.BleedingHeavy       => "Кровотечение (арт.)",
            GlobalCondition.Concussion          => "Контузия",
            GlobalCondition.ConcussionSevere    => "Сильная контузия",
            GlobalCondition.CardiacArrest       => "Остановка сердца",
            GlobalCondition.LethalHeadshot     => "Смертельное ранение",
            GlobalCondition.Under1853          => "Под 1853",
            GlobalCondition.Poisoned           => "Отравление",
            _                                   => "?"
        };

        public static string GetFullName(this GlobalCondition c) => c switch
        {
            GlobalCondition.Normal              => "Норма",
            GlobalCondition.Adrenaline          => "Под адреналином",
            GlobalCondition.AdrenalineOverdose  => "Передоз адреналином",
            GlobalCondition.Painkiller          => "Под обезболивающим",
            GlobalCondition.PainkillerOverdose  => "Передоз обезболивающим",
            GlobalCondition.BleedingLight       => "Лёгкое кровотечение (капиллярное)",
            GlobalCondition.BleedingMedium      => "Среднее кровотечение (венозное)",
            GlobalCondition.BleedingHeavy       => "Сильное кровотечение (артериальное)",
            GlobalCondition.Concussion          => "Контузия",
            GlobalCondition.ConcussionSevere    => "Сильная контузия",
            GlobalCondition.CardiacArrest       => "Остановка сердца",
            GlobalCondition.LethalHeadshot     => "Смертельное ранение в голову",
            _                                   => "?"
        };

        /// <summary>
        /// Приоритет для сортировки в HUD — чем выше, тем критичнее, показываем первым.
        /// </summary>
        public static int GetPriority(this GlobalCondition c) => c switch
        {
            GlobalCondition.LethalHeadshot     => 110,
            GlobalCondition.CardiacArrest       => 100,
            GlobalCondition.BleedingHeavy       => 90,
            GlobalCondition.ConcussionSevere    => 80,
            GlobalCondition.BleedingMedium      => 70,
            GlobalCondition.AdrenalineOverdose  => 65,
            GlobalCondition.PainkillerOverdose  => 65,
            GlobalCondition.Concussion          => 60,
            GlobalCondition.BleedingLight       => 50,
            GlobalCondition.Adrenaline          => 20,
            GlobalCondition.Painkiller          => 20,
            GlobalCondition.Poisoned           => 85,
            GlobalCondition.Under1853          => 20,
            GlobalCondition.Normal              => 0,
            _                                   => 0
        };
    }
}
 