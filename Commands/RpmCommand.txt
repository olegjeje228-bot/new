using System;
using CommandSystem;
using EventHUD.Rpm;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class RpmCommand : ICommand
    {
        public string Command => "rpm";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "rpm <module/all> <on/off>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 2)
            {
                response = "Использование: rpm <module/all> <on/off>";
                return false;
            }

            string moduleArg = arguments.At(0).ToLowerInvariant();
            string stateArg = arguments.At(1).ToLowerInvariant();

            if (stateArg != "on" && stateArg != "off")
            {
                response = "Второй аргумент должен быть on или off.";
                return false;
            }

            bool enabled = stateArg == "on";

            if (moduleArg == "all")
            {
                RpModuleManager.Instance.SetAll(enabled);
                response = $"Все RP-модули: {(enabled ? "включены" : "выключены")}";
                return true;
            }

            if (!Enum.TryParse<RpModuleType>(moduleArg, true, out var moduleType))
            {
                response = $"Неизвестный модуль: {moduleArg}";
                return false;
            }

            RpModuleManager.Instance.SetEnabled(moduleType, enabled);
            response = $"Модуль {moduleType}: {(enabled ? "включен" : "выключен")}";
            return true;
        }
    }
}
 