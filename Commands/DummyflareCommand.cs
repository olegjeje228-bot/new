using System;
using CommandSystem;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class DummyflareCommand : ICommand
    {
        public string Command => "dummyflare";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Включает/отключает спавн дамми при одном игроке. Использование: dummyflare on/off";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);
            if (player == null)
            {
                response = "Только для игроков.";
                return false;
            }

            if (arguments.Count < 1)
            {
                bool currentState = Plugin.Instance?.AloneDummy?.IsEnabled ?? false;
                response = $"dummyflare: {(currentState ? "включён" : "выключен")}. Использование: dummyflare on/off";
                return false;
            }

            string arg = arguments.At(0).ToLowerInvariant();

            switch (arg)
            {
                case "on":
                    Plugin.Instance.AloneDummy.IsEnabled = true;
                    response = "dummyflare включён. Дамми будет спавниться при 1 игроке во время ивента.";
                    return true;

                case "off":
                    Plugin.Instance.AloneDummy.IsEnabled = false;
                    response = "dummyflare выключен.";
                    return true;

                default:
                    response = "Использование: dummyflare on/off";
                    return false;
            }
        }
    }
}