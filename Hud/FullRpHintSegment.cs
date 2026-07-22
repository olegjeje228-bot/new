using EventHUD.Rpm;
using Exiled.API.Features;

namespace EventHUD.Hud
{
    public static class FullRpHintSegment
    {
        public static string Build(Player player, Config config)
        {
            if (!FullRpState.IsEnabled) return string.Empty;
            if (player == null || !player.IsAlive) return string.Empty;
            if (FullRpState.IsConfirmed(player.UserId)) return string.Empty;

            return
                $"<voffset={config.FullRpHintVoffset}em>" +
                $"<indent={config.MedicineHudIndent}%>" +
                "<size=29><color=#FFC107>Внимание! Пожалуйста, забиндите всё что надо для РП в " +
                "<b>Settings > Server-specific</b> и нажмите «Я всё подтвердил». Это надо для РП.</color></size>";
        }
    }
}