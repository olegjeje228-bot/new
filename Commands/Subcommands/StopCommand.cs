using System;
using CommandSystem;
using Exiled.API.Features;

namespace EventHUD.Commands.Subcommands
{
    public class StopCommand : ICommand
    {
        public string Command => "stop";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "ev stop";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);
            bool success = EventManager.Instance.Stop(player, out string message);
            response = message;
            return success;
        }
    }
}
 