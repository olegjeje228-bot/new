using System;
using CommandSystem;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class HelicopterCommand : ICommand
    {
        public string Command => "helicopter";
        public string[] Aliases => new[] { "heli" };
        public string Description => "Вызывает вертолёт МОГ (только эффект, без спавна игроков).";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Respawn.SummonNtfChopper();
            response = "Вертолёт вызван.";
            return true;
        }
    }

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class CarCommand : ICommand
    {
        public string Command => "car";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Вызывает машину ПХ (только эффект, без спавна игроков).";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Respawn.SummonChaosInsurgencyVan();
            response = "Машина ПХ вызвана.";
            return true;
        }
    }
}
