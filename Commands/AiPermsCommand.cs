using System;
using CommandSystem;
using Exiled.Permissions.Extensions;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class AiPermsCommand : ICommand
    {
        public string Command => "aiperms";

        public string[] Aliases => new[] { "aiperm", "aip", "aipermission", "aipermissions" };

        public string Description => "Управление правами ИИ (.ai): safe / adm / fulladm.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission("eventhud.aiperms"))
            {
                response = "Недостаточно прав (eventhud.aiperms).";
                return false;
            }

            if (arguments.Count == 0 || arguments.At(0).ToLowerInvariant() == "status")
            {
                response = "Права ИИ: " + Ai.AiPermissions.Status()
                    + "\nИспользование: aip <safe|adm|fulladm> <on|off>";
                return true;
            }

            if (arguments.Count < 2)
            {
                response = "Использование: aip <safe|adm|fulladm> <on|off>";
                return false;
            }

            string mode = arguments.At(0).ToLowerInvariant();
            string state = arguments.At(1).ToLowerInvariant();

            if (state != "on" && state != "off")
            {
                response = "Второй аргумент: on или off.";
                return false;
            }

            if (!Ai.AiPermissions.Set(mode, state == "on"))
            {
                response = "Неизвестный режим. Доступны: safe, adm, fulladm.";
                return false;
            }

            response = $"Режим {mode} теперь {state}. " + Ai.AiPermissions.Status();
            return true;
        }
    }
}
