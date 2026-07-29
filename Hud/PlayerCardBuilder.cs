using EventHUD.Medicine;
using EventHUD.Rpm;
using Exiled.API.Features;

namespace EventHUD.Hud
{
    public static class PlayerCardBuilder
    {
        public static string Build(Player player, Config c)
        {
            bool medicineEnabled = RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine);

            string displayName = string.IsNullOrEmpty(player.DisplayNickname) ? player.Nickname : player.DisplayNickname;
            string cInfoValue = string.IsNullOrEmpty(player.CustomInfo) ? "—" : player.CustomInfo;

            // ── Порядок строк (сверху вниз) ──
            // 1. Заголовок / [id] [name] / CInfo
            // 2. Рация (выше аптечки на 1 строку)
            // 3. Бронежилет
            // 4. Состояние (медицина)
            // 5. Аптечка (внизу, ниже всех)

            // Рация — всегда на своей позиции
            string radioSegment = RadioHudSegment.Build(player, c, c.RadioWaveVoffset);
            bool hasRadio = !string.IsNullOrEmpty(radioSegment);

            // Если рации нет — поднимаем всё на 1 строку выше
            float currentVoffset = hasRadio ? c.RadioWaveVoffset - 1f : c.RadioWaveVoffset;

            string bottomSegment = string.Empty;

            if (medicineEnabled)
            {
                bool hasMedkitInHand = player.CurrentItem != null && player.CurrentItem.Type == ItemType.Medkit;

                // ── Бронежилет (строка 3) ──
                string armorSegment = ArmorHudSegment.Build(player, c, currentVoffset);
                if (!string.IsNullOrEmpty(armorSegment))
                {
                    bottomSegment += armorSegment;
                    currentVoffset -= 1f; // следующая строка ниже
                }

                // ── Состояние / медицина (строка 4) ──
                // Показываем всегда (даже "Норма"), если медицина включена
                float medVoffset1 = currentVoffset;
                float medVoffset2 = currentVoffset - 1f;
                bottomSegment += MedicineHudSegment.Build(player, c, medVoffset1, medVoffset2, out int medLines);
                currentVoffset -= medLines; // столько строк, сколько реально заняла медицина

                // ── Аптечка (строка 5, на 1 строку ниже состояния) ──
                if (hasMedkitInHand)
                {
                    ushort serial = player.CurrentItem.Serial;
                    var kit = MedkitInventoryStorage.GetOrCreate(serial);
                    bottomSegment += MedkitHudSegment.Build(player, player, kit, c, currentVoffset);
                }
            }

            string result = $"<size={c.BaseFontSize}><voffset={c.TitleVoffset}em><align=left><indent={c.TitleIndent}%><color={c.TitleColor}><b>{c.TitleText}" +
                   $"<voffset={c.NicknameVoffset}em><indent={c.NicknameIndent}%></color>" +
                   $"<size={c.NicknameFontSize}> [{player.Id}] [{displayName}]</size>" +
                   $"<voffset={c.RoleLabelVoffset}em><indent={c.RoleLabelIndent}%>{c.RoleLabelText}: " +
                   $"<voffset={c.RoleValueVoffset}em>" +
                   $"<size={c.RoleValueFontSize}><color={c.RoleValueColor}>\"{cInfoValue}\"</color></size>" +
                   radioSegment + bottomSegment;

            // SCP-049 HUD (инвентарь)
            if (Plugin.Instance?.Scp049 != null)
            {
                string scp049Hud = Plugin.Instance.Scp049.BuildHud(player, currentVoffset);
                if (!string.IsNullOrEmpty(scp049Hud))
                    result += scp049Hud;
            }

            return result;
        }
    }
}
