using System;
using System.Collections.Generic;
using System.IO;
using CommandSystem;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class CallAdminCommand : ICommand
    {
        private static readonly Dictionary<string, DateTime> _cooldowns = new Dictionary<string, DateTime>();
        private const double CooldownSeconds = 180.0;
        private const int MaxReasonLength = 100;
        private static readonly string OutboxPath = Path.Combine(Paths.Configs, "EventHUD-CallAdmin.txt");

        public string Command => "calladmin";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Пингует администратора в дискорд канале, просьба не абузить.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null)
            {
                response = "Команда доступна только игрокам.";
                return false;
            }

            if (arguments.Count == 0)
            {
                response = "Usage: .calladmin причина";
                return false;
            }

            if (_cooldowns.TryGetValue(player.UserId, out var last))
            {
                double left = CooldownSeconds - (DateTime.UtcNow - last).TotalSeconds;
                if (left > 0)
                {
                    response = $"Подождите {Math.Ceiling(left)} сек. перед повторным вызовом.";
                    return false;
                }
            }

            string reason = string.Join(" ", arguments);
            if (reason.Length > MaxReasonLength)
            {
                response = $"Причина слишком длинная (макс. {MaxReasonLength} символов).";
                return false;
            }

            reason = reason.Replace("|", "/").Replace("\n", " ").Replace("\r", " ").Replace("`", "'");
            string nickname = player.Nickname.Replace("|", "/").Replace("`", "'");

            try
            {
                File.AppendAllText(OutboxPath, $"{nickname}|{reason}\n");
            }
            catch (Exception e)
            {
                Log.Error($"[CallAdmin] Ошибка записи вызова: {e.Message}");
                response = "Не удалось отправить вызов.";
                return false;
            }

            _cooldowns[player.UserId] = DateTime.UtcNow;
            response = "Администрация вызвана. Не абузьте команду.";
            return true;
        }
    }
}