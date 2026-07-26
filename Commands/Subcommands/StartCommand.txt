using System;
using System.Linq;
using CommandSystem;
using EventHUD.Enums;
using EventHUD.Extensions;
using Exiled.API.Features;

namespace EventHUD.Commands.Subcommands
{
    public class StartCommand : ICommand
    {
        public string   Command     => "start";
        public string[] Aliases     => Array.Empty<string>();
        public string   Description => "ev start [RP] [название]";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player  player    = Player.Get(sender);
            RPType? rpType    = null;
            string  eventName = null;

            if (arguments.Count >= 1 && RPTypeExtensions.TryParse(arguments.At(0), out var parsed))
            {
                rpType = parsed;

                if (arguments.Count >= 2)
                {
                    eventName = string.Join(" ", arguments.Skip(1));
                    int maxLen = Plugin.Instance.Config.MaxEventNameLength;
                    if (eventName.Length > maxLen)
                    {
                        response = $"Название слишком длинное! Максимум {maxLen} символов (сейчас {eventName.Length}).";
                        return false;
                    }
                }
            }

            bool success = EventManager.Instance.Start(player, rpType, eventName, out string message);
            response = message;
            return success;
        }
    }
}
 