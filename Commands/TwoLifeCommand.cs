using System;
using CommandSystem;
using EventHUD.Cube;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class TwoLifeCommand : ICommand
    {
        public string Command => "2life";
        public string[] Aliases => new[] { "secondlife", "2л" };
        public string Description => ".2life - взять вторую жизнь (нужен SCP-500 в руках), .2life stat - статистика";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);
            if (player == null)
            {
                response = "Команда доступна только игрокам.";
                return false;
            }

            if (SecondLifeSystem.Instance == null)
            {
                response = "Система второй жизни не запущена.";
                return false;
            }

            if (arguments.Count >= 1)
            {
                string a = arguments.At(0).Trim().ToLowerInvariant();
                if (a == "stat" || a == "стат")
                {
                    response = SecondLifeSystem.Instance.GetStat(player);
                    return true;
                }
            }

            return SecondLifeSystem.Instance.TryActivate(player, out response);
        }
    }
}