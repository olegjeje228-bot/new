using System;
using CommandSystem;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public sealed class EscapeCommand : ICommand
    {
        public string Command => "escape";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Запрет на выход.";

        private static bool _escapeEnabled = true;

        public static bool IsEscapeEnabled => _escapeEnabled;

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            string arg = arguments.Count > 0
                ? arguments.At(0).ToLowerInvariant()
                : string.Empty;

            if (arg == "on")
            {
                _escapeEnabled = true;
                response = "Побег разрешён.";
                return true;
            }

            if (arg == "off")
            {
                _escapeEnabled = false;
                response = "Побег запрещён.";
                return true;
            }

            if (string.IsNullOrEmpty(arg))
            {
                _escapeEnabled = !_escapeEnabled;
                response = _escapeEnabled ? "Побег разрешён." : "Побег запрещён.";
                return true;
            }

            response = "Использование: escape [on/off]";
            return false;
        }

        public static void Reset()
        {
            _escapeEnabled = true;
        }
    }
}