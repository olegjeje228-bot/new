using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using MEC;

namespace EventHUD.Discord
{
    /// <summary>
    /// Пишет текущий онлайн в JSON-файл для дискорд-бота.
    /// </summary>
    public class OnlineStatusService
    {
        private readonly Config _config;
        private CoroutineHandle _handle;

        public OnlineStatusService(Config config)
        {
            _config = config;
        }

        public void Start()
        {
            if (!_config.OnlineStatusEnabled)
                return;
            _handle = Timing.RunCoroutine(Loop());
        }

        public void Stop()
        {
            Timing.KillCoroutines(_handle);
        }

        private IEnumerator<float> Loop()
        {
            while (true)
            {
                try
                {
                    Write();
                }
                catch (Exception e)
                {
                    Log.Debug("[OnlineStatus] Не удалось записать файл: " + e.Message);
                }

                yield return Timing.WaitForSeconds(Math.Max(3f, _config.OnlineStatusInterval));
            }
        }

        private void Write()
        {
            int online = Player.List.Count(p => !p.IsHost && !p.IsNPC);
            int max = Server.MaxPlayerCount;
            long updated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            string ip = "";
            int port = 0;
            string round = "false";

            try { ip = Server.IpAddress ?? ""; } catch { }
            try { port = Server.Port; } catch { }
            try { round = Round.InProgress ? "true" : "false"; } catch { }

            string path = string.IsNullOrWhiteSpace(_config.OnlineStatusFilePath)
                ? Path.Combine(Paths.Configs, "EventHUD-Online.json")
                : _config.OnlineStatusFilePath;

            File.WriteAllText(path,
                "{\"online\":" + online + ",\"max\":" + max +
                ",\"ip\":\"" + (ip ?? "") + "\",\"port\":" + port +
                ",\"round\":" + round + ",\"updated\":" + updated + "}");
        }
    }
}