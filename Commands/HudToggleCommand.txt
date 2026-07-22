using System;
using CommandSystem;
using EventHUD.Hud;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class HudToggleCommand : ICommand
    {
        public string Command => "hud";
        public string[] Aliases => new[] { "togglehud" };
        public string Description => "Управление HUD. .hud — вкл/выкл, .hud rel — перезагрузить, .hud rem — убрать";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);
            if (player == null)
            {
                response = "Только для игроков.";
                return false;
            }

            string subCmd = arguments.Count > 0 ? arguments.At(0).ToLowerInvariant() : string.Empty;

            switch (subCmd)
            {
                case "rel":
                    HudToggleService.SetReloaded(player.UserId);
                    response = "HUD перезагружен.";
                    return true;

                case "rem":
                    HudToggleService.Toggle(player.UserId);
                    response = "HUD убран.";
                    return true;

                default:
                    bool nowEnabled = HudToggleService.Toggle(player.UserId);
                    response = nowEnabled ? "HUD включён." : "HUD выключен.";
                    return true;
            }
        }
    }
}