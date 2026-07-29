using System;
using System.Text.RegularExpressions;

namespace EventHUD.Extensions
{
    public static class RichTextExtensions
    {
        private static readonly Regex TagRegex =
            new Regex("<.*?>", RegexOptions.Compiled);

        public static string StripTags(this string input) =>
            string.IsNullOrEmpty(input) ? string.Empty : TagRegex.Replace(input, string.Empty);

        public static int VisibleLength(this string input) =>
            input.StripTags().Length;

        public static (int r, int g, int b) HexToRgb(this string hex)
        {
            hex = hex.TrimStart('#');
            return (
                Convert.ToInt32(hex.Substring(0, 2), 16),
                Convert.ToInt32(hex.Substring(2, 2), 16),
                Convert.ToInt32(hex.Substring(4, 2), 16)
            );
        }
    }
}
 