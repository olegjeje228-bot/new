using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using MEC;

namespace EventHUD.Discord
{
    /// <summary>
    /// Читает команды от дискорд-бота из файла и выполняет их.
    /// </summary>
    public class BotCommandService
    {
        private readonly Config _config;
        private CoroutineHandle _handle;

        public BotCommandService(Config config)
        {
            _config = config;
        }

        public void Start()
        {
            if (!_config.BotCommandEnabled)
                return;
            _handle = Timing.RunCoroutine(Loop());
        }

        public void Stop()
        {
            Timing.KillCoroutines(_handle);
        }

        private string FilePath => string.IsNullOrWhiteSpace(_config.BotCommandFilePath)
            ? Path.Combine(Paths.Configs, "EventHUD-BotCommand.txt")
            : _config.BotCommandFilePath;

        private IEnumerator<float> Loop()
        {
            while (true)
            {
                try
                {
                    Poll();
                }
                catch (Exception e)
                {
                    Log.Debug("[BotCommand] " + e.Message);
                }

                yield return Timing.WaitForSeconds(3f);
            }
        }

        private void Poll()
        {
            string path = FilePath;
            if (!File.Exists(path))
                return;

            string raw = File.ReadAllText(path).Trim();
            File.Delete(path);

            if (raw.StartsWith("ban|"))
            {
                HandleBan(raw);
                return;
            }

            string cmd = raw.ToLowerInvariant();
            switch (cmd)
            {
                case "rr":
                    Log.Info("[BotCommand] Рестарт раунда по команде из Discord");
                    Round.Restart(false);
                    break;

                case "sr":
                    Log.Info("[BotCommand] Полный рестарт сервера по команде из Discord");
                    Server.Restart();
                    break;

                default:
                    Log.Warn("[BotCommand] Неизвестная команда: " + cmd);
                    break;
            }
        }

        private void HandleBan(string raw)
        {
            // формат: ban|цель|минуты|причина
            string[] parts = raw.Split(new[] { '|' }, 4);
            if (parts.Length < 4)
                return;

            string target = parts[1].Trim();
            long minutes = long.TryParse(parts[2], out long m) ? m : 0;
            string reason = parts[3].Trim();
            if (minutes <= 0)
                minutes = 60L * 24 * 365 * 100; // "навсегда" = 100 лет

            bool isIp = target.Contains(".");
            string id = isIp ? target : (target.Contains("@") ? target : target + "@steam");

            BanHandler.IssueBan(new BanDetails
            {
                OriginalName = "Discord ban",
                Id = id,
                IssuanceTime = DateTime.UtcNow.Ticks,
                Expires = DateTime.UtcNow.AddMinutes(minutes).Ticks,
                Reason = reason,
                Issuer = "Discord",
            }, isIp ? BanHandler.BanType.IP : BanHandler.BanType.UserId);

            // если игрок сейчас онлайн — выкинуть
            foreach (var p in Player.List)
            {
                if ((isIp && p.IPAddress == target) || (!isIp && p.UserId == id))
                    p.Kick(reason);
            }

            Log.Info($"[BotCommand] Бан: {id} на {minutes} мин, причина: {reason}");
        }
    }
}