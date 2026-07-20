namespace EventHUD.Medicine
{
    /// <summary>
    /// Глобальные состояния — не привязаны к части тела.
    /// </summary>
    public enum GlobalCondition
    {
        Normal,
        Adrenaline,
        AdrenalineOverdose,
        Painkiller,
        PainkillerOverdose,
        BleedingLight,      // Капиллярное
        BleedingMedium,     // Венозное
        BleedingHeavy,      // Артериальное
        Concussion,
        ConcussionSevere,
        CardiacArrest,
        LethalHeadshot,      // Смертельное ранение в голову (3 сек до смерти)
        Under1853,
        Poisoned
    }
}
 