using System;
using CommandSystem;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class RastCommand : ICommand
    {
        public string Command => "rast";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Растяжки: rast spawn | rast grab | rast del [номер|all] | rast list";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);

            string sub = arguments.Count > 0 ? arguments.At(0).ToLowerInvariant() : null;

            // spawn — без прав, для всех админов
            if (sub == "spawn")
            {
                if (player == null)
                {
                    response = "Ставить растяжку можно только из игры.";
                    return false;
                }

                bool ok = Tripwire.TripwireSystem.Place(player, out int id, out string error);
                response = ok ? $"Растяжка #{id} установлена." : error;
                return ok;
            }

            switch (sub)
            {
                case "grab":
                {
                    if (player == null)
                    {
                        response = "Эта команда работает только из игры.";
                        return false;
                    }

                    response = Tripwire.TripwireSystem.Grab(player);
                    return true;
                }

                case "del":
                {
                    // rast del all — удалить всё
                    if (arguments.Count >= 2)
                    {
                        string arg = arguments.At(1).ToLowerInvariant();
                        if (arg == "all" || arg == "все")
                        {
                            Tripwire.TripwireSystem.ClearAll();
                            response = "Все растяжки удалены.";
                            return true;
                        }

                        if (!int.TryParse(arg, out int id))
                        {
                            response = "Номер растяжки должен быть числом. Список: rast list";
                            return false;
                        }

                        bool ok = Tripwire.TripwireSystem.Remove(id);
                        response = ok ? $"Растяжка #{id} удалена." : $"Растяжка #{id} не найдена. Список: rast list";
                        return ok;
                    }

                    if (player == null)
                    {
                        response = "Без номера удалять можно только из игры (по взгляду).";
                        return false;
                    }

                    bool okLook = Tripwire.TripwireSystem.RemoveByLook(player, out int removedId);
                    response = okLook
                        ? $"Растяжка #{removedId} удалена."
                        : "Ты не смотришь ни на одну растяжку (до 15м). Или удали по номеру: rast del <число>";
                    return okLook;
                }

                case "list":
                    response = Tripwire.TripwireSystem.ListWires();
                    return true;

                default:
                    response = "Использование: rast spawn | rast grab | rast del [номер|all] | rast list";
                    return false;
            }
        }
    }
}