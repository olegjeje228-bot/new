using System;
using CommandSystem;
using Exiled.Permissions.Extensions;
using EventHUD.Cube;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public sealed class CubCommand : ICommand
    {
        public string Command => "cub";

        // Намеренно много вариантов и частых опечаток.
        public string[] Aliases => new[]
        {
            "cube", "kub", "kyb", "cubik", "cubick",
            "kubik", "kybik", "qub", "qube", "kube",
            "cubb", "cuba", "cubes", "kubus", "cubeevent",
            "cube_event", "cube-event", "cubeevent", "kubevent", "kybevent",
            "куб", "кубик", "кубикон", "кубивент", "кубивент",
            "куб_ивент", "куб-ивент", "ивенткуб", "ивент_куб", "ивент-куб",
            "кубэвент", "кубэвент", "кубevent", "cubивент", "kubивент",
            "кубонь", "кубон", "кубофф", "кубиквкл", "кубиквыкл",
            "кубэ", "кубе", "кубикevent", "кубик_event", "кубик-event",
            "cub_on", "cub_off", "kub_on", "kub_off", "kyb_on"
        };

        public string Description =>
            "Включение и выключение автоматической генерации лута для ивента «Куб».";

        public bool Execute(
            ArraySegment<string> arguments,
            ICommandSender sender,
            out string response)
        {
            if (!sender.CheckPermission("eventhud.manage"))
            {
                response = "Недостаточно прав. Требуется eventhud.manage.";
                return false;
            }

            if (arguments.Count < 1)
            {
                response =
                    "Использование: cub on/off\n" +
                    "Также поддерживаются: Куб on, kub on, kyb off.";
                return false;
            }

            string action = arguments.At(0).Trim().ToLowerInvariant();

            switch (action)
            {
                case "on":
                case "1":
                case "enable":
                case "start":
                case "вкл":
                case "включить":
                case "старт":
                    CubeLootSystem.Instance?.Enable();
                    response = "Система лута ивента «Куб» включена.";
                    return true;

                case "off":
                case "0":
                case "disable":
                case "stop":
                case "выкл":
                case "выключить":
                case "стоп":
                    CubeLootSystem.Instance?.Disable(removeSpawnedLoot: true);
                    response = "Система лута ивента «Куб» выключена. Созданный ею лут удалён.";
                    return true;

                case "status":
                case "state":
                case "статус":
                    CubeLootSystem system = CubeLootSystem.Instance;
                    response = system == null
                        ? "Система Куба не зарегистрирована."
                        : system.GetStatus();
                    return true;

                case "log":
                case "logs":
                case "лог":
                case "шансы":
                case "chance":
                case "chances":
                    response = CubeLootSystem.Instance?.BuildCurrentKubLog()
                        ?? "Система Куба не зарегистрирована.";
                    return true;

                case "luck":
                case "удача":
                {
                    if (arguments.Count < 2 ||
                        !int.TryParse(arguments.At(1), out int luck))
                    {
                        response =
                            $"Текущая удача: {CubeLootSystem.Instance?.CurrentLuck ?? 0}/1000000\n" +
                            "Использование: kub luck 0-1000000";
                        return true;
                    }

                    luck = Math.Max(0, Math.Min(1000000, luck));
                    CubeLootSystem.Instance?.SetLuck(luck);
                    response = $"Удача Куба установлена: {luck}/1000000";
                    return true;
                }

                case "list":
                case "список":
                {
                    if (arguments.Count < 2)
                    {
                        response = CubeLootSystem.Instance?.BuildItemChanceList(null)
                            ?? "Система Куба не зарегистрирована.";
                        return true;
                    }

                    if (!int.TryParse(arguments.At(1), out int section) ||
                        section < 0 || section > 8)
                    {
                        response = "Раздел от 0 до 8. Или просто: kub list";
                        return false;
                    }

                    response = CubeLootSystem.Instance?.BuildItemChanceList(section)
                        ?? "Система Куба не зарегистрирована.";
                    return true;
                }

                case "item":
                case "предмет":
                {
                    if (arguments.Count < 2 ||
                        !int.TryParse(arguments.At(1), out int id))
                    {
                        response = "Использование: kub item ID";
                        return false;
                    }

                    if (CubeLootSystem.Instance == null)
                    {
                        response = "Система Куба не зарегистрирована.";
                        return false;
                    }

                    return CubeLootSystem.Instance.QueueItem(id, out response);
                }

                default:
                    response = "Неизвестный аргумент. Использование: cub on/off/status/log/luck/list/item";
                    return false;
            }
        }
    }
}