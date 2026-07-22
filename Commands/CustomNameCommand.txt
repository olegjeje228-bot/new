using System;
using CommandSystem;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class CustomNameCommand : ICommand
    {
        public string Command => "customname";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Выдаёт РП имя персонажа: .customname <имя>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null)
            {
                response = "Только для игроков.";
                return false;
            }

            if (arguments.Count < 1)
            {
                response = "Использование: .customname <имя персонажа>";
                return false;
            }

            string name = string.Join(" ", arguments);
            int maxLen = Plugin.Instance.Config.MaxRpNameLength;

            if (name.Length > maxLen)
            {
                response = $"Слишком длинное имя! Максимум {maxLen} символов (сейчас {name.Length}).";
                return false;
            }

            // Пишем в то же самое нативное поле, которое использует RA-команда setname.
            // Благодаря этому HUD показывает ОДИНАКОВЫЙ результат независимо от источника.
            player.DisplayNickname = name;

            response = $"РП имя установлено: {name}";
            return true;
        }
    }
}
 