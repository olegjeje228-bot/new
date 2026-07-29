using System;
using System.Collections.Generic;
using System.Linq;
using EventHUD.Enums;
using EventHUD.Extensions;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Элемент меню аптечки — одна лечимая (или нелечимая) травма.
    /// </summary>
    public class MedkitMenuItem
    {
        public string DisplayName;
        public string Procedure;
        public float  TotalTime;
        public string TimeLabel;
        public bool   CanHeal;
        public string CannotHealReason;
        public bool   IsInventoryView;  // true = это пункт [В аптечке]
        public bool   IsFirstAid;      // true = первая помощь (временная повязка)
        public bool   IsBack;          // true = кнопка "Назад" (вернуться к выбору аптечки)

        public GlobalCondition? GlobalTarget;
        public LocalInjuryType? LocalTarget;
        public BodyPart?        BodyPartTarget;

        public List<HealStage> Stages = new List<HealStage>();
    }

    public class HealStage
    {
        public string Name;
        public float  Duration;
    }

    public class MedkitMenuState
    {
        public int  SelectedIndex;
        public bool IsHealing;
        public int  CurrentStage;
        public float HealElapsed;
        public DateTime HealStartedAt;
        public MedkitMenuItem HealingItem;
        public bool ShowInventory; // true = сейчас показываем содержимое

        public void Reset()
        {
            SelectedIndex = 0;
            IsHealing     = false;
            CurrentStage  = 0;
            HealElapsed   = 0;
            HealStartedAt = default;
            HealingItem   = null;
            ShowInventory = false;
        }
    }

    /// <summary>
    /// Генерирует список пунктов меню аптечки.
    /// Последний пункт — [В аптечке] (показывает содержимое).
    /// Учитывает расходники — если нет нужного материала, CanHeal=false.
    /// </summary>
    public static class MedkitMenuBuilder
    {
        public static List<MedkitMenuItem> Build(PlayerMedicalState state, Config c, MedkitInventory kit, bool isHealingOther = false)
        {
            var items = new List<MedkitMenuItem>();

            // Получаем RP-множитель времени
            float timeMult = 1f;
            try
            {
                var rp = EventManager.Instance?.Session?.RpType ?? RPType.HARDRP;
                timeMult = rp.GetTimeMultiplier();
            }
            catch { }

            // Кнопка "Назад" — только при лечении другого, всегда первая в списке
            if (isHealingOther)
            {
                items.Add(new MedkitMenuItem
                {
                    DisplayName = "← Назад",
                    Procedure = "",
                    TotalTime = 0,
                    TimeLabel = "",
                    CanHeal = true,
                    IsBack = true,
                    Stages = new List<HealStage>()
                });
            }

            // ── Глобальные состояния ──
            foreach (var cond in state.Conditions.OrderByDescending(x => x.GetPriority()))
            {
                if (cond == GlobalCondition.Normal)
                    continue;

                var item = BuildGlobalItem(cond, c, kit, state, timeMult);
                if (item != null)
                    items.Add(item);

                // Первая помощь для кровотечений, если нет полных расходников
                if ((cond == GlobalCondition.BleedingLight ||
                     cond == GlobalCondition.BleedingMedium ||
                     cond == GlobalCondition.BleedingHeavy) && item != null && !item.CanHeal)
                {
                    var fa = BuildFirstAidItem(state, kit, timeMult);
                    if (fa != null) items.Add(fa);
                }
            }

            // ── Локальные травмы ──
            foreach (var inj in state.Injuries.OrderByDescending(x => x.Type.GetPriority()))
            {
                var item = BuildLocalItem(inj, c, kit, timeMult);
                if (item != null)
                    items.Add(item);
            }

            // Если есть кровотечение но нет пункта лечения (например все условия отфильтрованы),
            // всё равно добавить первую помощь если можно
            if (state.GetBleedingLevel().HasValue && !items.Any(i => i.GlobalTarget.HasValue && i.GlobalTarget.ToString().StartsWith("Bleeding")))
            {
                var fa = BuildFirstAidItem(state, kit, timeMult);
                if (fa != null && !items.Any(x => x.IsFirstAid))
                    items.Add(fa);
            }

            // ── Последний пункт: [В аптечке] ──
            items.Add(new MedkitMenuItem
            {
                DisplayName      = "[В аптечке]",
                Procedure        = "",
                TotalTime        = 0,
                TimeLabel        = "",
                CanHeal          = false,
                CannotHealReason = "",
                IsInventoryView  = true
            });

            return items;
        }

        private static MedkitMenuItem BuildFirstAidItem(PlayerMedicalState state, MedkitInventory kit, float timeMult)
        {
            if (kit == null) return null;
            string reason;
            if (!kit.CanFirstAid(state, out reason))
                return null;

            var bleed = state.GetBleedingLevel();
            string bleedName = bleed?.ToString() ?? "кровотечение";
            float baseTime = 3f;
            float scaled = baseTime * timeMult;
            var stage = new HealStage { Name = "Врем. повязка", Duration = scaled };

            return new MedkitMenuItem
            {
                DisplayName = "Первая помощь",
                Procedure = "Врем. повязка",
                TotalTime = scaled,
                TimeLabel = $"[{scaled:0.#}с]",
                CanHeal = true,
                CannotHealReason = null,
                IsFirstAid = true,
                GlobalTarget = bleed,
                Stages = new List<HealStage> { stage }
            };
        }

        private static MedkitMenuItem BuildGlobalItem(GlobalCondition cond, Config c, MedkitInventory kit, PlayerMedicalState state, float timeMult)
        {
            // Проверяем расходники
            string noReason = null;
            bool hasSupplies = kit?.CanTreat(cond, null, out noReason) ?? true;

            switch (cond)
            {
                case GlobalCondition.AdrenalineOverdose:
                    return MakeItem("Передоз адреналином", "Промывание", 10f, 
                        hasSupplies, noReason, cond, null, null, timeMult,
                        new HealStage { Name = "Промывание", Duration = 10f });

                case GlobalCondition.PainkillerOverdose:
                    return MakeItem("Передоз обезболом", "Промывание", 10f,
                        hasSupplies, noReason, cond, null, null, timeMult,
                        new HealStage { Name = "Промывание", Duration = 10f });

                case GlobalCondition.BleedingLight:
                    return MakeItem("Кровотечение (кап.)", "Перевязка", 5f,
                        hasSupplies, noReason, cond, null, null, timeMult,
                        new HealStage { Name = "Перевязка", Duration = 5f });

                case GlobalCondition.BleedingMedium:
                    return MakeItem("Кровотечение (вен.)", "Перевязка", 13f,
                        hasSupplies, noReason, cond, null, null, timeMult,
                        new HealStage { Name = "Перевязка", Duration = 13f });

                case GlobalCondition.BleedingHeavy:
                    return MakeItem("Кровотечение (арт.)", "Жгут + Лечение", 0f,
                        hasSupplies, noReason, cond, null, null, timeMult,
                        new HealStage { Name = "Жгут", Duration = 5f },
                        new HealStage { Name = "Лечение", Duration = 10f });

                case GlobalCondition.Concussion:
                case GlobalCondition.ConcussionSevere:
                    return new MedkitMenuItem
                    {
                        DisplayName = cond.GetShortName(),
                        CanHeal = false,
                        CannotHealReason = "Проходит само",
                        GlobalTarget = cond
                    };

                case GlobalCondition.CardiacArrest:
                    bool can = hasSupplies;
                    float t = can ? 15f * timeMult : 0f;
                    return new MedkitMenuItem
                    {
                        DisplayName = "Остановка сердца",
                        CanHeal = can,
                        CannotHealReason = can ? null : (noReason ?? "Нужен парамедик"),
                        Procedure = can ? "Дефибриллятор" : "",
                        TotalTime = t,
                        TimeLabel = can ? $"[{t:0.#}с]" : "",
                        GlobalTarget = cond,
                        Stages = can
                            ? new List<HealStage> { new HealStage { Name = "Реанимация", Duration = t } }
                            : new List<HealStage>()
                    };

                default:
                    return null;
            }
        }

        private static MedkitMenuItem BuildLocalItem(LocalInjury inj, Config c, MedkitInventory kit, float timeMult)
        {
            string partName = inj.Part.GetShortName();
            string noReason = null;
            bool hasSupplies = kit?.CanTreat(null, inj.Type, out noReason) ?? true;

            switch (inj.Type)
            {
                case LocalInjuryType.Bruise:
                    return MakeItem($"Ушиб ({partName})", "Компресс", 0f,
                        hasSupplies, noReason, null, inj.Type, inj.Part, timeMult,
                        new HealStage { Name = "Компресс", Duration = 4f },
                        new HealStage { Name = "Заморозка", Duration = 10f },
                        new HealStage { Name = "Держать", Duration = 60f });

                case LocalInjuryType.Gunshot:
                    return MakeItem($"Огнестрел ({partName})", "Извлечение + перевязка", 10f,
                        hasSupplies, noReason, null, inj.Type, inj.Part, timeMult,
                        new HealStage { Name = "Извлечение + перевязка", Duration = 10f });

                case LocalInjuryType.Stab:
                    return MakeItem($"Ножевая ({partName})", "Обработка + перевязка", 6f,
                        hasSupplies, noReason, null, inj.Type, inj.Part, timeMult,
                        new HealStage { Name = "Обработка + перевязка", Duration = 6f });

                case LocalInjuryType.Chemical:
                    return MakeItem($"Химическая ({partName})", "Нейтрализация", 25f,
                        hasSupplies, noReason, null, inj.Type, inj.Part, timeMult,
                        new HealStage { Name = "Нейтрализация", Duration = 25f });

                case LocalInjuryType.Burn:
                    return MakeItem($"Ожог ({partName})", "Обработка ожога", 0f,
                        hasSupplies, noReason, null, inj.Type, inj.Part, timeMult,
                        new HealStage { Name = "Снять урон", Duration = 2f },
                        new HealStage { Name = "Обработка", Duration = 20f });

                case LocalInjuryType.Fracture:
                    return MakeItem($"Перелом ({partName})", "Наложить шину", 10f,
                        hasSupplies, noReason, null, inj.Type, inj.Part, timeMult,
                        new HealStage { Name = "Наложить шину", Duration = 10f });

                case LocalInjuryType.Corrosion:
                    return new MedkitMenuItem
                    {
                        DisplayName = "Коррозия",
                        CanHeal = false,
                        CannotHealReason = "Невозможно вылечить",
                        LocalTarget = inj.Type,
                        BodyPartTarget = inj.Part
                    };

                default:
                    return null;
            }
        }

        private static MedkitMenuItem MakeItem(
            string name, string procedure, float fallbackTime,
            bool canHeal, string noReason,
            GlobalCondition? global, LocalInjuryType? local, BodyPart? part,
            float timeMult,
            params HealStage[] stages)
        {
            // Масштабируем стадии по RP
            var scaledStages = new List<HealStage>();
            float total = 0f;
            foreach (var s in stages)
            {
                float d = s.Duration * timeMult;
                scaledStages.Add(new HealStage { Name = s.Name, Duration = d });
                total += d;
            }
            if (total <= 0.01f) total = fallbackTime * timeMult;

            // Генерируем TimeLabel
            string timeLabel = "";
            if (canHeal && total > 0)
            {
                if (scaledStages.Count <= 1)
                    timeLabel = $"[{total:0.#}с]";
                else
                    timeLabel = "[" + string.Join("+", scaledStages.Select(x => $"{x.Duration:0.#}с")) + "]";
            }

            return new MedkitMenuItem
            {
                DisplayName      = name,
                Procedure        = canHeal ? procedure : "",
                TotalTime        = canHeal ? total : 0,
                TimeLabel        = timeLabel,
                CanHeal          = canHeal,
                CannotHealReason = canHeal ? null : noReason,
                GlobalTarget     = global,
                LocalTarget      = local,
                BodyPartTarget   = part,
                Stages           = canHeal ? scaledStages : new List<HealStage>(),
                IsFirstAid       = false
            };
        }
    }
}
