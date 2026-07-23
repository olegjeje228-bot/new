using System;
using System.Collections.Generic;
using CommandSystem;
using EventHUD.Util;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class CallCommand : ICommand
    {
        private static readonly Dictionary<string, DateTime> _cooldowns = new();

        public string Command => "call";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Отправить запрос администрации";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null)
            {
                response = "Команда доступна только игрокам.";
                return false;
            }

            if (_cooldowns.TryGetValue(player.UserId, out var lastUse) &&
                (DateTime.UtcNow - lastUse).TotalSeconds < 5)
            {
                response = "Подождите 5 секунд перед повторным использованием.";
                return false;
            }

            string reason = arguments.Count > 0
                ? string.Join(" ", arguments).Trim()
                : string.Empty;

            if (string.IsNullOrEmpty(reason))
            {
                response = "Укажите причину вызова.";
                return false;
            }

            if (!TextGuard.IsSafePlain(reason, 100))
            {
                response = "Недопустимые символы или слишком длинный текст.";
                return false;
            }

            _cooldowns[player.UserId] = DateTime.UtcNow;

            string alert = $"[{player.Id}] [{player.Nickname}] отправил .call, причина: {reason}";

            foreach (var admin in Player.List)
            {
                if (!admin.ReferenceHub.serverRoles.RemoteAdmin)
                    continue;

                admin.Broadcast(5, $"<color=yellow>{alert}</color>");
                admin.SendConsoleMessage(alert, "yellow");
            }

            DiscordWebhookService.SendCallAlert($"[CALL] {player.Nickname} ({player.UserId}): {reason}");

            response = "Запрос отправлен администрации.";
            return true;
        }
    }
}
