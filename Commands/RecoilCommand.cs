using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class RecoilCommand : ICommand
    {
        public string Command => "recoil";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Реалистичная отдача: recoil all/<id.id.id> on/off";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 2)
            {
                response = "Использование: recoil all/<id.id.id> on/off";
                return false;
            }

            string target = arguments.At(0).ToLowerInvariant();
            bool enable = arguments.At(1).ToLowerInvariant() == "on";

            if (target == "all")
            {
                Recoil.RecoilSystem.GlobalEnabled = enable;
                response = enable
                    ? "Отдача включена для всех Д-классов и учёных."
                    : "Отдача для всех выключена.";
                return true;
            }

            var names = new System.Collections.Generic.List<string>();
            foreach (string token in target.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Player p = int.TryParse(token, out int id) ? Player.Get(id) : null;
                if (p == null)
                    continue;

                if (enable)
                    Recoil.RecoilSystem.Individual.Add(p.UserId);
                else
                    Recoil.RecoilSystem.Individual.Remove(p.UserId);

                names.Add(p.Nickname);
            }

            response = names.Count == 0
                ? "Игроки не найдены."
                : $"Отдача {(enable ? "включена" : "выключена")}: {string.Join(", ", names)}";
            return names.Count > 0;
        }
    }
}