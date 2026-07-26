using System;
using System.Collections.Generic;
using System.Text;
using Exiled.API.Features;

namespace EventHUD.Medicine
{
    public static class MedkitHudSegment
    {
        private const int MaxVisible = 3;
        private const int MaxCharsPerItem = 25;

        // FIXED: now takes targetPlayer explicitly, so healing other shows patient's injuries, not doctor's
        public static string Build(Player doctor, Player targetPlayer, MedkitInventory kit, Config c, float voffset)
        {
            if (kit == null) return BuildEmpty(c, voffset, null);

            if (!MedicalStorage.TryGet(targetPlayer.UserId, out var medState))
                return BuildEmpty(c, voffset, kit);

            bool isHealingOther = doctor.Id != targetPlayer.Id;
            var menuState = MedkitStorage.GetOrCreate(doctor.UserId);
            var items = MedkitMenuBuilder.Build(medState, c, kit, isHealingOther);

            if (items.Count == 0)
                return BuildEmpty(c, voffset, kit);

            if (menuState.SelectedIndex >= items.Count)
                menuState.SelectedIndex = 0;
            if (menuState.SelectedIndex < 0)
                menuState.SelectedIndex = items.Count - 1;

            if (menuState.IsHealing && menuState.HealingItem != null)
                return BuildHealing(menuState, c, voffset);

            if (menuState.ShowInventory)
                return BuildInventoryView(c, voffset, kit);

            return BuildConveyor(items, menuState.SelectedIndex, c, voffset, kit);
        }

        // Legacy overload for self-heal (keeps old calls compiling)
        public static string Build(Player player, Config c, float voffset)
        {
            if (player.CurrentItem == null || player.CurrentItem.Type != ItemType.Medkit)
                return string.Empty;
            ushort serial = player.CurrentItem.Serial;
            var kit = MedkitInventoryStorage.GetOrCreate(serial);
            return Build(player, player, kit, c, voffset);
        }

        private static string BuildConveyor(List<MedkitMenuItem> items, int selected, Config c, float voffset, MedkitInventory kit)
        {
            var sb = new StringBuilder();
            sb.Append($"<voffset={voffset}em><indent={c.MedicineHudIndent}%>");
            string kitShort = kit.Type switch
            {
                MedkitType.Civilian => "Гражд",
                MedkitType.Industrial => "Рабоч",
                MedkitType.Military => "Воен",
                MedkitType.Paramedic => "Парам",
                _ => "Апт"
            };
            sb.Append($"<color=#888888>{kitShort}: </color>");
            var visible = GetVisibleItems(items, selected);
            for (int i = 0; i < visible.Count; i++)
            {
                if (i > 0) sb.Append("<color=#555555>|</color>");
                var (item, isSelected) = visible[i];
                sb.Append(FormatItemShort(item, isSelected));
            }
            return sb.ToString();
        }

        private static List<(MedkitMenuItem item, bool isSelected)> GetVisibleItems(List<MedkitMenuItem> items, int selected)
        {
            var result = new List<(MedkitMenuItem, bool)>();
            if (items.Count <= MaxVisible)
            {
                for (int i = 0; i < items.Count; i++) result.Add((items[i], i == selected));
                return result;
            }
            int prev = (selected - 1 + items.Count) % items.Count;
            int next = (selected + 1) % items.Count;
            result.Add((items[prev], false));
            result.Add((items[selected], true));
            result.Add((items[next], false));
            return result;
        }

        private static string FormatItemShort(MedkitMenuItem item, bool isSelected)
        {
            if (item.IsInventoryView)
            {
                string clr = isSelected ? "#00FF00" : "#888888";
                return $"<color={clr}>[Содерж.]</color>";
            }
            if (item.IsBack)
            {
                string clr = isSelected ? "#FF5555" : "#AA4444";
                return $"<color={clr}>← Назад</color>";
            }
            string shortName = ShortenName(item.DisplayName);
            if (!item.CanHeal)
            {
                // Серым + причина, если есть место
                string reason = string.IsNullOrEmpty(item.CannotHealReason) ? "" : $" [{item.CannotHealReason}]";
                // Обрезаем чтобы влезло
                int maxLen = MaxCharsPerItem - reason.Length;
                if (maxLen < 5) reason = "";
                if (shortName.Length > maxLen && maxLen > 0)
                    shortName = shortName.Substring(0, maxLen);
                return $"<color=#888888>{shortName}{reason}</color>";
            }
            string color = item.IsFirstAid ? "#FFAA00" : (isSelected ? "#00FF00" : "#888888");
            string prefix = item.IsFirstAid ? "ПП " : "";
            return $"<color={color}>{prefix}{shortName} {item.TimeLabel}</color>";
        }

        private static string ShortenName(string name)
        {
            if (name.Length <= MaxCharsPerItem) return name;
            name = name.Replace("Кровотечение", "Кров.")
                       .Replace("Огнестрел", "Огн.")
                       .Replace("Ножевая", "Нож.")
                       .Replace("Химическая", "Хим.")
                       .Replace("Коррозия", "Корр.")
                       .Replace("Перелом", "Перел.")
                       .Replace("Передоз адреналином", "П.Адр.")
                       .Replace("Передоз обезболом", "П.Обезб.")
                       .Replace("Остановка сердца", "Ост.серд.");
            if (name.Length > MaxCharsPerItem) name = name.Substring(0, MaxCharsPerItem - 2) + "..";
            return name;
        }

        private static string BuildHealing(MedkitMenuState menuState, Config c, float voffset)
        {
            var item = menuState.HealingItem;
            if (item.Stages.Count == 0) return string.Empty;
            int stageIdx = menuState.CurrentStage;
            if (stageIdx >= item.Stages.Count) stageIdx = item.Stages.Count - 1;
            var stage = item.Stages[stageIdx];
            float elapsed = menuState.HealElapsed;
            float total = stage.Duration;
            float progress = Math.Min(elapsed / Math.Max(total, 0.1f), 1f);
            int filled = (int)(progress * 10);
            string bar = new string('█', filled) + new string('░', 10 - filled);
            string label = item.Stages.Count > 1 ? $"{stageIdx + 1}/{item.Stages.Count} {stage.Name}" : stage.Name;
            return $"<voffset={voffset}em><indent={c.MedicineHudIndent}%><color=#00FF00>{label} {bar} {elapsed:0}/{total:0}с</color>";
        }

        private static string BuildInventoryView(Config c, float voffset, MedkitInventory kit)
        {
            var items = new List<string>();
            if (kit.Bandages > 0) items.Add($"Б:{kit.Bandages}");
            if (kit.Tourniquets > 0) items.Add($"Ж:{kit.Tourniquets}");
            if (kit.HemostaticPads > 0) items.Add($"Г:{kit.HemostaticPads}");
            if (kit.ColdPacks > 0) items.Add($"Х:{kit.ColdPacks}");
            if (kit.Splints > 0) items.Add($"Ш:{kit.Splints}");
            if (kit.Antiseptic > 0) items.Add($"А:{kit.Antiseptic}");
            if (kit.Saline > 0) items.Add($"Ф:{kit.Saline}");
            if (kit.Painkillers > 0) items.Add($"О:{kit.Painkillers}");
            if (kit.Morphine > 0) items.Add($"М:{kit.Morphine}");
            if (kit.Adrenaline > 0) items.Add($"Ад:{kit.Adrenaline}");
            if (kit.Atropine > 0) items.Add($"Ат:{kit.Atropine}");
            if (kit.DefibCharges > 0) items.Add($"Д:{kit.DefibCharges}");
            if (kit.SurgicalKits > 0) items.Add($"Хр:{kit.SurgicalKits}");
            if (kit.OcclusivePads > 0) items.Add($"Ок:{kit.OcclusivePads}");
            var sb = new StringBuilder();
            sb.Append($"<voffset={voffset}em><indent={c.MedicineHudIndent}%>");
            sb.Append($"<color=red>←Назад</color> <color=#555555>|</color> ");
            sb.Append($"<color={kit.Type.GetColor()}>{kit.Type.GetDisplayName()}</color>");
            if (items.Count == 0) { sb.Append(" <color=#888888>Пусто</color>"); return sb.ToString(); }

            // Максимум 2 строки содержимого, потом +N
            int perLine = 7;
            int maxLines = 2;
            int maxShown = perLine * maxLines;
            int shown = Math.Min(items.Count, maxShown);

            float lineVoff = voffset - 1f;
            for (int i = 0; i < shown; i += perLine)
            {
                int count = Math.Min(perLine, shown - i);
                sb.Append($"<voffset={lineVoff}em><indent={c.MedicineHudIndent}%>");
                sb.Append(string.Join(" ", items.GetRange(i, count)));
                lineVoff -= 1f;
            }
            if (items.Count > maxShown) sb.Append($" +{items.Count - maxShown}");
            return sb.ToString();
        }

        private static string BuildEmpty(Config c, float voffset, MedkitInventory kit)
        {
            return $"<voffset={voffset}em><indent={c.MedicineHudIndent}%> <color=#888888>Нечего лечить</color>";
        }
    }
}
