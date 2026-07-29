using System;
using CommandSystem;
using EventHUD.Rpm;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class FullRpCommand : ICommand
    {
        public string Command => "fullrp";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Вкл/выкл FullRP (лечение через бинды)";
        public bool SanitizeResponse => false;

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            string arg = arguments.Count > 0 ? arguments.At(0).ToLowerInvariant() : "";
            if (arg != "on" && arg != "off")
            {
                response = "Использование: .fullrp on / .fullrp off";
                return false;
            }

            bool enable = arg == "on";
            if (FullRpState.IsEnabled == enable)
            {
                response = enable ? "FullRP уже включён." : "FullRP уже выключен.";
                return true;
            }

            FullRpState.IsEnabled = enable;
            if (enable)
                FullRpState.ResetConfirmations();

            Rpm.FullRpSss.Refresh();

            foreach (var pl in Player.List)
            {
                if (pl.IsNPC) continue;
                Hud.HudNoticeService.Show(pl, enable
                    ? "<color=#FFC107>FullRP включён — лечение через бинды</color>"
                    : "<color=#4CAF50>FullRP выключен — лечение обычным использованием аптечки</color>", 4f);
            }

            response = enable ? "FullRP: ON" : "FullRP: OFF";
            return true;
        }
    }
}