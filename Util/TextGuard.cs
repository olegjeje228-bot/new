using System.Text;
using System.Text.RegularExpressions;

namespace EventHUD.Util
{
    public static class TextGuard
    {
        private const string AllowedExtra = " \n\t.,:;!?()[]{}\"'«»`~@#$%^&*+-_=/\\|№█\u2014\u2013\u2026";

        private static readonly Regex TagRegex = new Regex(@"<([^<>]{0,64})>", RegexOptions.Compiled);

        private static readonly Regex AiTag = new Regex(
            @"^/?(b|i|color(=#?[0-9a-zA-Z]{1,10})?)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex HudTag = new Regex(
            @"^/?(b|i|color(=#?[0-9a-zA-Z]{1,10})?|alpha=#[0-9a-fA-F]{2}|align=\w{1,10}|size=\d{1,3}|voffset=-?\d{1,3}(\.\d{1,2})?em|indent=-?\d{1,3}(\.\d{1,2})?%|space=-?\d{1,3}(\.\d{1,2})?em)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SizeTagRegex = new Regex("<\\s*size\\s*=\\s*([^<>]{0,12})\\s*>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsSafePlain(string input, int maxLen)
        {
            if (string.IsNullOrEmpty(input))
                return true;
            if (input.Length > maxLen)
                return false;

            foreach (char c in input)
            {
                if (c == '<' || c == '>')
                    return false;
                if (!IsAllowedChar(c))
                    return false;
            }

            return true;
        }

        public static bool IsSafeRich(string input, int maxLen, bool allowLayout)
        {
            if (string.IsNullOrEmpty(input))
                return true;
            if (input.Length > maxLen)
                return false;

            foreach (char c in input)
            {
                if (c == '<' || c == '>')
                    continue;
                if (!IsAllowedChar(c))
                    return false;
            }

            int open = 0;
            foreach (char c in input)
            {
                if (c == '<') { open++; if (open > 1) return false; }
                else if (c == '>') { open--; if (open < 0) return false; }
            }
            if (open != 0)
                return false;

            var re = allowLayout ? HudTag : AiTag;
            foreach (Match m in TagRegex.Matches(input))
            {
                if (!re.IsMatch(m.Groups[1].Value.Trim()))
                    return false;
            }

            return true;
        }

        public static string SoftSanitize(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            string result = SizeTagRegex.Replace(input, m =>
            {
                string raw = m.Groups[1].Value.Trim();
                if (float.TryParse(raw, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float v)
                    && v >= 0f && v <= 40f)
                    return "<size=" + raw + ">";
                return string.Empty;
            });

            var sb = new StringBuilder(result.Length);
            foreach (char c in result)
            {
                if (char.IsControl(c) && c != '\n' && c != '\t') continue;
                if (c >= '\u200B' && c <= '\u200F') continue;
                if (c >= '\u202A' && c <= '\u202E') continue;
                if (c >= '\u2060' && c <= '\u206F') continue;
                if (c == '\uFEFF') continue;
                if (c >= '\u3000' && c <= '\u30FF') continue;
                if (c >= '\u4E00' && c <= '\u9FFF') continue;
                if (c >= '\uAC00' && c <= '\uD7AF') continue;
                if (char.IsSurrogate(c)) continue;
                sb.Append(c);
            }

            string cleaned = sb.ToString();
            if (cleaned.Length > maxLength)
                cleaned = cleaned.Substring(0, maxLength);
            return cleaned;
        }

        private static bool IsAllowedChar(char c)
        {
            if (c >= 'a' && c <= 'z') return true;
            if (c >= 'A' && c <= 'Z') return true;
            if (c >= '0' && c <= '9') return true;
            if (c >= '\u0400' && c <= '\u04FF') return true;
            return AllowedExtra.IndexOf(c) >= 0;
        }
    }
}
