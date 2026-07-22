using System;
using CommandSystem;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class FfCommand : ICommand
    {
        public string Command => "ff";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Включить/выключить friendly fire: ff on|off";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 1)
            {
                response = $"Использование: ff on|off. Сейчас friendly fire: {(Server.FriendlyFire ? "on" : "off")}";
                return false;
            }

            switch (arguments.At(0).ToLowerInvariant())
            {
                case "on":
                case "1":
                    Server.FriendlyFire = true;
                    response = "Friendly fire ВКЛЮЧЁН.";
                    return true;

                case "off":
                case "0":
                    Server.FriendlyFire = false;
                    response = "Friendly fire ВЫКЛЮЧЕН.";
                    return true;

                default:
                    response = "Использование: ff on|off";
                    return false;
            }
        }
    }
}