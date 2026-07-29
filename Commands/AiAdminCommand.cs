using System;
using System.Collections.Generic;
using CommandSystem;
using Exiled.API.Features;
using MEC;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class AiAdminCommand : ICommand
    {
        public string Command => "ai";
        public string[] Aliases => new[] { "helper", "claude", "deepseek" };
        public string Description => "ИИ помощник администрации (Claude Fable 5)";

        private static readonly Dictionary<string, DateTime> Cooldowns = new Dictionary<string, DateTime>();

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);
            if (player == null || !player.ReferenceHub.serverRoles.RemoteAdmin)
            {
                response = "Команда доступна только администрации с RA.";
                return false;
            }

            if (arguments.Count == 0)
            {
                response = "Использование: ai <запрос>. Например: ai заспавни пх с пистолетом и броником";
                return false;
            }

            var cfg = Plugin.Instance.Config;
            if (Cooldowns.TryGetValue(player.UserId, out DateTime last))
            {
                double wait = cfg.AiCooldownAdmin - (DateTime.UtcNow - last).TotalSeconds;
                if (wait > 0)
                {
                    response = "Подождите " + Math.Ceiling(wait) + " сек.";
                    return false;
                }
            }
            Cooldowns[player.UserId] = DateTime.UtcNow;

            string question = Util.TextGuard.SoftSanitize(string.Join(" ", arguments), 1000);
            if (string.IsNullOrWhiteSpace(question))
            {
                response = "Запрос пустой после фильтрации.";
                return false;
            }

            Timing.RunCoroutine(Ai.AiService.AskAdminRoutine(player, question));
            response = "Запрос принят, ответ придет в RA и в консоль (~).";
            return true;
        }
    }
}
