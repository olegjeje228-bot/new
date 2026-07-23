using System.Text.RegularExpressions;
using EventHUD.Extensions;

namespace EventHUD.Hud
{
    public static class EffectApplier
    {
        public static string ApplyColorOverlay(
            string richText,
            float  progress,
            string targetColorHex)
        {
            if (progress <= 0.001f)
                return richText;

            string hex           = InterpolateColor("FFFFFF", targetColorHex, progress);
            string withoutColors = Regex.Replace(richText, "<color=.*?>|</color>", "");

            return $"<color=#{hex}>{withoutColors}</color>";
        }

        private static string InterpolateColor(string fromHex, string toHex, float t)
        {
            var (r1, g1, b1) = fromHex.HexToRgb();
            var (r2, g2, b2) = toHex.HexToRgb();

            int r = (int)(r1 + (r2 - r1) * t);
            int g = (int)(g1 + (g2 - g1) * t);
            int b = (int)(b1 + (b2 - b1) * t);

            return $"{r:X2}{g:X2}{b:X2}";
        }
    }
}
 