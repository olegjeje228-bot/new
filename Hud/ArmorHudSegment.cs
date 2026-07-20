using EventHUD.Medicine;
using Exiled.API.Features;
using PlayerRoles;

namespace EventHUD.Hud
{
    public static class ArmorHudSegment
    {
        public static string Build(
            Player player,
            Config config,
            float verticalOffset)
        {
            ArmorState armor =
                ArmorStorage.GetOrCreate(player.UserId);

            bool hasArmorItem = false;

            foreach (var item in player.Items)
            {
                if (item.Type == ItemType.ArmorLight ||
                    item.Type == ItemType.ArmorCombat ||
                    item.Type == ItemType.ArmorHeavy)
                {
                    hasArmorItem = true;
                    break;
                }
            }

            if (!hasArmorItem)
            {
                if (armor.Type != ArmorType.None)
                    armor.Reset();

                return string.Empty;
            }

            if (armor.Type == ArmorType.None)
            {
                foreach (var item in player.Items)
                {
                    ArmorType armorType;

                    switch (item.Type)
                    {
                        // Если ArmorLight остаётся бронежилетом,
                        // замени Helmet на Light.
                        case ItemType.ArmorLight:
                            armorType = ArmorType.Helmet;
                            break;

                        case ItemType.ArmorCombat:
                            armorType = ArmorType.Combat;
                            break;

                        case ItemType.ArmorHeavy
                            when player.Role.Type == RoleTypeId.NtfCaptain:
                            armorType = ArmorType.Tank;
                            break;

                        case ItemType.ArmorHeavy:
                            armorType = ArmorType.Heavy;
                            break;

                        default:
                            continue;
                    }

                    armor.SetType(armorType);
                    break;
                }
            }

            if (armor.Type == ArmorType.None)
                return string.Empty;

            if (armor.Type == ArmorType.Helmet)
            {
                string helmetText = armor.HelmetDurability > 0
                    ? "<color=#4CAF50>Шлем: цел</color>"
                    : "<color=#F44336>Шлем: пробит</color>";

                return
                    $"<voffset={verticalOffset}em>" +
                    $"<indent={config.MedicineHudIndent}%>" +
                    helmetText;
            }

            float percentage = armor.MaxDurability > 0f
                ? armor.Durability / armor.MaxDurability * 100f
                : 0f;

            if (percentage < 0f)
                percentage = 0f;

            if (percentage > 100f)
                percentage = 100f;

            string color = InterpolateColor(percentage);

            string percentageText = armor.IsBroken
                ? "Разрушен"
                : $"{percentage:0}%";

            return
                $"<voffset={verticalOffset}em>" +
                $"<indent={config.MedicineHudIndent}%>" +
                $"<color={color}>Состояние бронежилета: " +
                $"{percentageText}</color>";
        }

        private static string InterpolateColor(float percentage)
        {
            float interpolation = percentage / 100f;

            int red =
                (int)(255 + (107 - 255) * interpolation);

            int green =
                (int)(170 * interpolation);

            int blue =
                (int)(204 * interpolation);

            red = ClampByte(red);
            green = ClampByte(green);
            blue = ClampByte(blue);

            return $"#{red:X2}{green:X2}{blue:X2}";
        }

        private static int ClampByte(int value)
        {
            if (value < 0)
                return 0;

            if (value > 255)
                return 255;

            return value;
        }
    }
}