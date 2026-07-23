using System;
using CommandSystem;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public sealed class TeslaCommand : ICommand
    {
        public string Command => "tesla";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Выключить или включить телсу";

        private static bool _teslaEnabled = true;

        public static bool IsTeslaEnabled => _teslaEnabled;

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            string arg = arguments.Count > 0
                ? arguments.At(0).ToLowerInvariant()
                : string.Empty;

            if (arg == "on")
            {
                _teslaEnabled = true;
                response = "Tesla включена.";
                return true;
            }

            if (arg == "off")
            {
                _teslaEnabled = false;
                response = "Tesla выключена.";
                return true;
            }

            if (string.IsNullOrEmpty(arg))
            {
                _teslaEnabled = !_teslaEnabled;
                response = _teslaEnabled ? "Tesla включена." : "Tesla выключена.";
                return true;
            }

            response = "Использование: tesla [on/off]";
            return false;
        }

        public static void Reset()
        {
            _teslaEnabled = true;
        }
    }
}