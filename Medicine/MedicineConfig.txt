using System.ComponentModel;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Все настройки модуля медицины — добавляются в основной Config.cs как секция.
    /// Здесь описаны как отдельный partial-подобный блок для читаемости,
    /// но фактически поля должны быть в Config.cs.
    /// Этот файл — справочник значений по умолчанию.
    /// </summary>
    public static class MedicineDefaults
    {
        // ── Адреналин ──
        public const int AdrenalineOverdoseThreshold   = 3;    // 3+ за жизнь = передоз
        public const float OverdoseEffectDuration       = 25f;  // Blurred/Slowness/Asphyxiated длительность
        public const byte OverdoseSlownessIntensity     = 55;
        public const byte OverdoseAsphyxiatedIntensity  = 25;

        // ── Обезболивающее ──
        public const int PainkillerOverdoseThreshold    = 4;    // 4+ за жизнь = передоз

        // ── Капиллярное кровотечение ──
        public const float LightBleedGrenadeMaxDamage   = 10f;  // Граната < 10 = капиллярное
        public const float LightBleedFallMaxDamage      = 15f;  // Падение < 15 = капиллярное
        public const float LightBleedConcussedDuration  = 5f;
        public const float LightBleedBurstDps           = 2f;   // −2 HP/сек
        public const float LightBleedBurstDuration      = 5f;   // 5 сек бурной фазы (итого −10)
        public const float LightBleedPassiveDps         = 1f;   // −1 HP / 10 сек
        public const float LightBleedPassiveInterval    = 10f;

        // ── Венозное кровотечение ──
        public const float MediumBleedGrenadeMinDamage  = 15f;
        public const float MediumBleedGrenadeMaxDamage  = 50f;
        public const float MediumBleedConcussedDuration = 10f;
        public const float MediumBleedDeafenedDuration  = 15f;
        public const byte  MediumBleedBlindnessIntensity = 40;
        public const float MediumBleedBlindnessDuration = 30f;
        public const float MediumBleedBurstDps          = 6f;   // −6 HP/сек
        public const float MediumBleedBurstDuration     = 5f;   // 5 сек (итого −30)
        public const float MediumBleedPassiveDps        = 3f;   // −3 HP / 10 сек
        public const float MediumBleedPassiveInterval   = 10f;

        // ── Артериальное кровотечение ──
        public const float HeavyBleedConcussedDuration  = 10f;
        public const float HeavyBleedDeafenedDuration   = 15f;
        public const byte  HeavyBleedBlindnessIntensity = 40;
        public const float HeavyBleedBlindnessDuration  = 30f;
        public const float HeavyBleedBurstDps           = 2f;   // −2 HP/сек (но 30 сек!)
        public const float HeavyBleedBurstDuration      = 30f;  // 30 сек (итого −60)
        public const float HeavyBleedPassiveDps         = 5f;   // −5 HP / 10 сек
        public const float HeavyBleedPassiveInterval    = 10f;

        // ── Контузия ──
        public const float ConcussionFlashRadius        = 15f;
        public const float ConcussionGrenadeRadius      = 30f;
        public const float SevereConcussionRadius       = 5f;

        // ── Коррозия SCP-106 ──
        public const float CorrosionProximityTime       = 3f;   // 3 сек рядом с 106
        public const float CorrosionStainedDuration     = 3000f;
        public const byte  CorrosionBlindnessIntensity  = 45;
        public const float CorrosionConcussedDuration   = 3000f;

        // ── Химическая SCP-244 ──
        public const float Scp244ProximityTime          = 10f;
        public const float ChemicalConcussedDuration    = 60f;

        // ── Ожог Micro-HID ──
        public const float BurnDuration                 = 10f;

        // ── Перелом ──
        public const byte  FractureSlownessIntensity    = 70;
        public const float FractureConcussedDuration    = 30f;

        // ── Тики ──
        public const float InjuryTickInterval           = 1f;   // Проверка каждую секунду

        // ── HUD ──
        public const float MedicineHudVoffset           = 58f;
        public const float MedicineHudVoffset2          = 57f;  // Вторая строка если >2 травм
        public const float MedicineHudIndent            = -29.5f;
        public const string MedicineHudLabel            = "Состояние";
        public const int MaxHudItemsPerLine             = 3;
        public const int MaxHudLines                    = 2;
    }
}
 