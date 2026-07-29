using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Features;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Строка "Состояние: ..." в карточке игрока.
    /// Вся вёрстка через voffset, без br.
    /// </summary>
    public static class MedicineHudSegment
    {
        private const int MaxPerLine = 3;

        /// <summary>
        /// Строит HUD-сегмент медицины с динамическим voffset.
        /// </summary>
        /// <param name="player">Игрок</param>
        /// <param name="c">Конфиг</param>
        /// <param name="voffset1">Voffset первой строки</param>
        /// <param name="voffset2">Voffset второй строки (если нужна)</param>
        public static string Build(Player player, Config c, float voffset1, float voffset2, out int linesUsed)
        {
            if (!MedicalStorage.TryGet(player.UserId, out var state))
            {
                linesUsed = 1;
                return BuildNormal(c, voffset1);
            }

            if (!state.HasAnything)
            {
                linesUsed = 1;
                return BuildNormal(c, voffset1);
            }

            // Собираем все элементы для отображения
            var items = new List<HudItem>();

            // Глобальные состояния (кроме Normal)
            foreach (var cond in state.Conditions)
            {
                if (cond == GlobalCondition.Normal)
                    continue;

                items.Add(new HudItem
                {
                    Priority = cond.GetPriority(),
                    Text     = $"<color={cond.GetColor()}>{cond.GetShortName()}</color>"
                });
            }

            // Локальные травмы
            foreach (var injury in state.Injuries)
            {
                items.Add(new HudItem
                {
                    Priority = injury.Type.GetPriority(),
                    Text     = injury.ToHudString()
                });
            }

            // Сортируем по приоритету (самые критичные первые)
            items.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            if (items.Count == 0)
            {
                linesUsed = 1;
                return BuildNormal(c, voffset1);
            }

            // ── Строка 1 ──
            int line1Count = System.Math.Min(items.Count, MaxPerLine);
            var line1Items = items.Take(line1Count).ToList();
            int remaining  = items.Count - line1Count;

            var sb = new StringBuilder();
            sb.Append($"<voffset={voffset1}em><indent={c.MedicineHudIndent}%>");
            sb.Append($"{c.MedicineHudLabel}: ");
            sb.Append(string.Join(" | ", line1Items.Select(i => i.Text)));

            // ── Строка 2 — если нужна ──
            if (remaining > 0)
            {
                int line2Count = System.Math.Min(remaining, MaxPerLine);
                var line2Items = items.Skip(line1Count).Take(line2Count).ToList();
                int leftover   = items.Count - line1Count - line2Count;

                sb.Append($"<voffset={voffset2}em><indent={c.MedicineHudIndent}%>");
                sb.Append(string.Join(" | ", line2Items.Select(i => i.Text)));

                if (leftover > 0)
                    sb.Append($" | <color=#888888>+{leftover}</color>");
            }

            linesUsed = remaining > 0 ? 2 : 1;
            return sb.ToString();
        }

        private static string BuildNormal(Config c, float voffset)
        {
            return $"<voffset={voffset}em><indent={c.MedicineHudIndent}%>" +
                   $"{c.MedicineHudLabel}: <color=#ebffea>Норма</color>";
        }

        private class HudItem
        {
            public int    Priority;
            public string Text;
        }
    }
}
 