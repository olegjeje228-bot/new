using System;
using System.Globalization;
using System.Linq;
using CommandSystem;
using EventHUD.Hud;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class HintBroadcastCommand : ICommand
    {
        public string Command => "hbc";
        public string[] Aliases => new[] { "hintbroadcast", "hbroadcast" };
        public string Description => "hbc [durationSeconds] <text>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission("eventhud.manage"))
            {
                response = "Нет прав.";
                return false;
            }

            if (arguments.Count == 0)
            {
                response = "hbc [durationSeconds] <text>";
                return false;
            }

            float duration = 10f;
            int textStartIndex = 0;

            if (TryParseFloat(arguments.At(0), out float parsedDuration))
            {
                duration = Math.Max(0.1f, parsedDuration);
                textStartIndex = 1;
            }

            if (arguments.Count <= textStartIndex)
            {
                response = "После времени сам текст";
                return false;
            }

            string text = string.Join(" ", arguments.Skip(textStartIndex))
                .Replace("\\n", "\n");

            int recipients = 0;
            foreach (Player player in Player.List)
            {
                HudNoticeService.Show(player, text, duration);
                recipients++;
            }

            response = $"Hint отправлен {recipients} игрок(ам) на {duration.ToString("0.##", CultureInfo.InvariantCulture)} сек.";
            return true;
        }

        private static bool TryParseFloat(string input, out float value)
        {
            return float.TryParse(input.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
 