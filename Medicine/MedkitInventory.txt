using System;
using System.Collections.Generic;
using System.Text;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Содержимое конкретной аптечки (привязано к serial предмета).
    /// Расходуется при лечении.
    /// </summary>
    public class MedkitInventory
    {
        public MedkitType Type { get; set; }

        // ── Расходники (кол-во оставшихся) ──
        public int Bandages       { get; set; } // Бинты
        public int Tourniquets    { get; set; } // Жгуты
        public int HemostaticPads { get; set; } // Гемостатические салфетки
        public int ColdPacks     { get; set; } // Холодовые пакеты
        public int Splints       { get; set; } // Шины
        public int Antiseptic    { get; set; } // Антисептик (промывания)
        public int Painkillers   { get; set; } // Обезболивающее (таблетки)
        public int Morphine      { get; set; } // Морфин (инъекции) — только военная
        public int Adrenaline    { get; set; } // Адреналин (ампулы) — только парамедик
        public int Atropine      { get; set; } // Атропин (против передоза) — только парамедик
        public int DefibCharges  { get; set; } // Заряды дефибриллятора — только парамедик
        public int SurgicalKits  { get; set; } // Хирургические наборы (извлечение пули) — только парамедик
        public int OcclusivePads { get; set; } // Окклюзионные повязки (грудь) — только военная
        public int Saline        { get; set; } // Физраствор (промывание)

        /// <summary>
        /// Первая помощь — временная повязка при кровотечении.
        /// Требует 1 бинт, работает только если еще не применялась.
        /// </summary>
        public bool CanFirstAid(PlayerMedicalState state, out string reason)
        {
            reason = null;
            if (state.FirstAidUsed)
            {
                reason = "ПП уже оказана";
                return false;
            }
            var bleed = state.GetBleedingLevel();
            if (!bleed.HasValue)
            {
                reason = "Нет кровотечения";
                return false;
            }
            if (Bandages <= 0)
            {
                reason = "Нет бинтов";
                return false;
            }
            return true;
        }

        public void ConsumeFirstAid()
        {
            if (Bandages > 0) Bandages--;
        }

        /// <summary>
        /// Проверяет, можно ли вылечить данную травму этой аптечкой (есть ли расходники).
        /// </summary>
        public bool CanTreat(GlobalCondition? global, LocalInjuryType? local, out string reason)
        {
            reason = null;

            // ── Глобальные ──
            if (global.HasValue)
            {
                switch (global.Value)
                {
                    case GlobalCondition.BleedingLight:
                        if (Bandages <= 0 && HemostaticPads <= 0)
                        { reason = "Нет бинтов"; return false; }
                        return true;

                    case GlobalCondition.BleedingMedium:
                        if (Bandages <= 0)
                        { reason = "Нет бинтов"; return false; }
                        if (Type == MedkitType.Civilian)
                        { reason = "Нужен жгут"; return false; }
                        return true;

                    case GlobalCondition.BleedingHeavy:
                        if (Tourniquets <= 0)
                        { reason = "Нет жгутов"; return false; }
                        if (Bandages <= 0)
                        { reason = "Нет бинтов"; return false; }
                        return true;

                    case GlobalCondition.AdrenalineOverdose:
                    case GlobalCondition.PainkillerOverdose:
                        if (Type == MedkitType.Paramedic)
                        {
                            if (Atropine <= 0)
                            { reason = "Нет атропина"; return false; }
                            return true;
                        }
                        if (Antiseptic <= 0 && Saline <= 0)
                        { reason = "Нет р-ра для промывания"; return false; }
                        return true;

                    case GlobalCondition.CardiacArrest:
                        if (Type != MedkitType.Paramedic)
                        { reason = "Нужна аптечка парамедика"; return false; }
                        if (DefibCharges <= 0)
                        { reason = "Дефибриллятор разряжен"; return false; }
                        return true;

                    case GlobalCondition.Concussion:
                    case GlobalCondition.ConcussionSevere:
                        reason = "Проходит само";
                        return false;

                    default:
                        return false;
                }
            }

            // ── Локальные ──
            if (local.HasValue)
            {
                switch (local.Value)
                {
                    case LocalInjuryType.Bruise:
                        // Ушиб можно лечить холодовыми пакетами ИЛИ бинтами
                        if (ColdPacks <= 0 && Bandages <= 0)
                        { reason = "Нет холодовых пакетов"; return false; }
                        return true;

                    case LocalInjuryType.Gunshot:
                        if (Type == MedkitType.Civilian)
                        { reason = "Нет инструментов"; return false; }
                        if (Bandages <= 0)
                        { reason = "Нет бинтов"; return false; }
                        return true;

                    case LocalInjuryType.Stab:
                        if (Bandages <= 0)
                        { reason = "Нет бинтов"; return false; }
                        return true;

                    case LocalInjuryType.Chemical:
                        if (Saline <= 0 && Antiseptic <= 0)
                        { reason = "Нет раствора"; return false; }
                        return true;

                    case LocalInjuryType.Burn:
                        if (Type == MedkitType.Military)
                        { reason = "Нет противоожоговых"; return false; }
                        if (Bandages <= 0)
                        { reason = "Нет бинтов"; return false; }
                        return true;

                    case LocalInjuryType.Fracture:
                        if (Splints <= 0)
                        { reason = "Нет шин"; return false; }
                        return true;

                    case LocalInjuryType.Corrosion:
                        reason = "Невозможно вылечить";
                        return false;

                    default:
                        return false;
                }
            }

            return false;
        }

        private int Dec(int v) => v > 0 ? v - 1 : 0;

        /// <summary>
        /// Потратить расходники после успешного лечения.
        /// </summary>
        public void Consume(GlobalCondition? global, LocalInjuryType? local)
        {
            if (global.HasValue)
            {
                switch (global.Value)
                {
                    case GlobalCondition.BleedingLight:
                        if (HemostaticPads > 0) HemostaticPads--;
                        else Bandages = Dec(Bandages);
                        break;
                    case GlobalCondition.BleedingMedium:
                        Bandages = Dec(Bandages);
                        break;
                    case GlobalCondition.BleedingHeavy:
                        Tourniquets = Dec(Tourniquets);
                        Bandages = Dec(Bandages);
                        break;
                    case GlobalCondition.AdrenalineOverdose:
                    case GlobalCondition.PainkillerOverdose:
                        if (Atropine > 0) Atropine--;
                        else if (Saline > 0) Saline--;
                        else Antiseptic = Dec(Antiseptic);
                        break;
                    case GlobalCondition.CardiacArrest:
                        DefibCharges = Dec(DefibCharges);
                        Adrenaline = Dec(Adrenaline);
                        break;
                }
            }

            if (local.HasValue)
            {
                switch (local.Value)
                {
                    case LocalInjuryType.Bruise:
                        if (ColdPacks > 0) ColdPacks--;
                        else Bandages = Dec(Bandages);
                        break;
                    case LocalInjuryType.Gunshot:
                        Bandages = Dec(Bandages);
                        SurgicalKits = Dec(SurgicalKits);
                        break;
                    case LocalInjuryType.Stab:
                        Bandages = Dec(Bandages);
                        break;
                    case LocalInjuryType.Chemical:
                        if (Saline > 0) Saline--;
                        else Antiseptic = Dec(Antiseptic);
                        break;
                    case LocalInjuryType.Burn:
                        Bandages = Dec(Bandages);
                        break;
                    case LocalInjuryType.Fracture:
                        Splints = Dec(Splints);
                        break;
                }
            }

            // Clamp — защита от отрицательных значений
            Bandages = Math.Max(0, Bandages);
            Tourniquets = Math.Max(0, Tourniquets);
            HemostaticPads = Math.Max(0, HemostaticPads);
            ColdPacks = Math.Max(0, ColdPacks);
            Splints = Math.Max(0, Splints);
            Antiseptic = Math.Max(0, Antiseptic);
            Saline = Math.Max(0, Saline);
            Painkillers = Math.Max(0, Painkillers);
            Morphine = Math.Max(0, Morphine);
            Adrenaline = Math.Max(0, Adrenaline);
            Atropine = Math.Max(0, Atropine);
            DefibCharges = Math.Max(0, DefibCharges);
            SurgicalKits = Math.Max(0, SurgicalKits);
            OcclusivePads = Math.Max(0, OcclusivePads);
        }

        /// <summary>
        /// Текст содержимого для HUD [В аптечке].
        /// </summary>
        public string ToHudString()
        {
            var sb = new StringBuilder();
            sb.Append($"<color={Type.GetColor()}>{Type.GetDisplayName()}</color>: ");

            var parts = new List<string>();
            if (Bandages > 0)       parts.Add($"Бинты:{Bandages}");
            if (Tourniquets > 0)    parts.Add($"Жгуты:{Tourniquets}");
            if (HemostaticPads > 0) parts.Add($"Гемост:{HemostaticPads}");
            if (ColdPacks > 0)     parts.Add($"Холод:{ColdPacks}");
            if (Splints > 0)       parts.Add($"Шины:{Splints}");
            if (Antiseptic > 0)    parts.Add($"Антис:{Antiseptic}");
            if (Saline > 0)        parts.Add($"Физр-р:{Saline}");
            if (Painkillers > 0)   parts.Add($"Обезбол:{Painkillers}");
            if (Morphine > 0)      parts.Add($"Морфин:{Morphine}");
            if (Adrenaline > 0)    parts.Add($"Адрен:{Adrenaline}");
            if (Atropine > 0)      parts.Add($"Атроп:{Atropine}");
            if (DefibCharges > 0)  parts.Add($"Дефибр:{DefibCharges}");
            if (SurgicalKits > 0)  parts.Add($"Хирург:{SurgicalKits}");
            if (OcclusivePads > 0) parts.Add($"Оккл:{OcclusivePads}");

            sb.Append(parts.Count > 0 ? string.Join(" | ", parts) : "Пусто");
            return sb.ToString();
        }

        // ═══════════════════════════════════════
        // Фабрика — создание по типу
        // ═══════════════════════════════════════

        public static MedkitInventory Create(MedkitType type)
        {
            return type switch
            {
                MedkitType.Civilian => new MedkitInventory
                {
                    Type           = MedkitType.Civilian,
                    Bandages       = 2,
                    HemostaticPads = 2,
                    Antiseptic     = 1,
                    ColdPacks      = 0,
                    Splints        = 0,
                    Tourniquets    = 0,
                    Painkillers    = 1,
                    Saline         = 0,
                },
                MedkitType.Industrial => new MedkitInventory
                {
                    Type           = MedkitType.Industrial,
                    Bandages       = 4,
                    HemostaticPads = 3,
                    Tourniquets    = 1,
                    ColdPacks      = 2,
                    Splints        = 1,
                    Antiseptic     = 1,
                    Painkillers    = 1,
                    Saline         = 1,
                },
                MedkitType.Military => new MedkitInventory
                {
                    Type           = MedkitType.Military,
                    Bandages       = 3,
                    Tourniquets    = 2,
                    HemostaticPads = 2,
                    OcclusivePads  = 2,
                    Morphine       = 1,
                    ColdPacks      = 0,
                    Splints        = 0,
                    Antiseptic     = 0,
                    Saline         = 0,
                },
                MedkitType.Paramedic => new MedkitInventory
                {
                    Type           = MedkitType.Paramedic,
                    Bandages       = 10,
                    Tourniquets    = 4,
                    HemostaticPads = 4,
                    ColdPacks      = 3,
                    Splints        = 2,
                    Antiseptic     = 3,
                    Saline         = 1,
                    Painkillers    = 2,
                    Adrenaline     = 5,
                    Atropine       = 5,
                    DefibCharges   = 3,
                    SurgicalKits   = 6,
                    OcclusivePads  = 4,
                    Morphine       = 2,
                },
                _ => new MedkitInventory { Type = MedkitType.Civilian }
            };
        }
    }
}
 