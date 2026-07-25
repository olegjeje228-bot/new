using System;
using CommandSystem;
using EventHUD.Elevator;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public sealed class ElevatCommand : ICommand
    {
        public string Command => "elevat";

        public string[] Aliases =>
            new[]
            {
                "elevevat",
                "elevator",
            };

        public string Description =>
    "Система лифтов: elevat on|off|restore";

        public bool Execute(
            ArraySegment<string> arguments,
            ICommandSender sender,
            out string response)
        {
            if (!sender.CheckPermission("eventhud.manage"))
            {
                response =
                    "Недостаточно прав: eventhud.manage";

                return false;
            }

            ElevatorBreakSystem system =
                Plugin.Instance?.ElevatorBreaks;

            if (system == null)
            {
                response =
                    "Система лифтов не инициализирована.";

                return false;
            }

            if (arguments.Count < 1)
            {
                response =
                    $"elevat: {(system.IsEnabled ? "on" : "off")}. " +
                    "Использование: elevat on|off|restore";

                return false;
            }

            string argument =
                arguments.At(0).ToLowerInvariant();

            switch (argument)
            {
                case "on":
                case "1":
                {
                    system.Enable();

                    response =
                        "Система ломания лифтов включена.";

                    return true;
                }

                case "off":
                case "0":
                {
                    system.Disable(true);

                    response =
                        "Система ломания лифтов выключена; " +
                        "лифты восстановлены.";

                    return true;
                }

                case "restore":
                case "repair":
                {
                    int restoredCount =
                        system.RestoreAll();

                    response = restoredCount > 0
                        ? $"Полностью восстановлено лифтов: " +
                          $"{restoredCount}."
                        : "Сломанных лифтов нет.";

                    return true;
                }

                default:
                {
                    response =
                    "Использование: " +
                    "elevat on|off|restore";

                    return false;
                }
            }
        }
    }
}
