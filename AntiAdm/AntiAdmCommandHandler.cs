using System;
using System.Collections.Generic;
using System.Linq;
using EventHUD.Hud;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using LabApi.Features.Enums;

namespace EventHUD.AntiAdm
{
    /// <summary>
    /// Урезанная версия AntiAdm: только лимит дамми (AA-01)
    /// и запрет связывания дамми (AA-02). Всё остальное убрано.
    /// </summary>
    public class AntiAdmCommandHandler
    {
        private readonly Config _config;

        public AntiAdmCommandHandler(Config config)
        {
            _config = config;
        }

        // Заглушки — их вызывают другие файлы, удалять нельзя
        public void Reset() { }
        public void CleanupPlayer(string userId) { }
        public void OnDummyDeath() { }
        public bool IsDummyBlocked => false;

        private static bool IsDummy(Player player) => player != null && player.IsNPC;

        // Поддерживает цели: *, "5." / "5.8.12.", UserID, точный ник
        private static bool TargetIsDummy(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            if (raw == "*")
                return Player.List.Any(IsDummy);

            string[] chunks = raw.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            bool allIds = chunks.Length > 0;
            var players = new List<Player>();
            foreach (var chunk in chunks)
            {
                if (!int.TryParse(chunk, out int id)) { allIds = false; break; }
                var p = Player.Get(id);
                if (p != null) players.Add(p);
            }
            if (allIds)
                return players.Any(IsDummy);

            var found = Player.List.FirstOrDefault(p =>
                string.Equals(p.UserId, raw, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Nickname, raw, StringComparison.OrdinalIgnoreCase));

            return found != null && IsDummy(found);
        }

        public void OnSendingValidCommand(SendingValidCommandEventArgs ev)
        {
            if (!_config.AntiAdmEnabled) return;
            if (ev.Type != CommandType.RemoteAdmin) return;
            if (string.IsNullOrEmpty(ev.Query)) return;

            string[] parts = ev.Query.Split(' ');
            if (parts.Length == 0) return;

            string cmd = parts[0].ToLowerInvariant();
            string[] args = parts.Length > 1 ? parts.Skip(1).ToArray() : Array.Empty<string>();

            // ── AA-01: лимит дамми (макс 8, из конфига) ──
            if (cmd == "dummy" && args.Length > 0 && args[0].ToLowerInvariant() == "spawn")
            {
                int currentDummies = Player.List.Count(IsDummy);
                if (currentDummies >= _config.AntiAdmMaxDummies)
                {
                    Deny(ev, "AA-01");
                    return;
                }
            }

            // ── AA-02: нельзя связывать дамми ──
            if (cmd == "dummy" && args.Length > 0 && args[0].ToLowerInvariant() == "bind")
            {
                Deny(ev, "AA-02");
                return;
            }

            if ((cmd == "disarm" || cmd == "handcuff" || cmd == "cuff") && args.Length > 0)
            {
                if (TargetIsDummy(args[0]))
                {
                    Deny(ev, "AA-02");
                    return;
                }
            }
        }

        // Запрет наручников на дамми прямо в игре (AA-02)
        public void OnHandcuffing(HandcuffingEventArgs ev)
        {
            if (!_config.AntiAdmEnabled) return;
            if (ev.Target == null) return;

            if (IsDummy(ev.Target))
            {
                ev.IsAllowed = false;
                HudNoticeService.Show(ev.Player, "<color=red>Отказ [AA-02]</color>", 2f);
            }
        }

        private void Deny(SendingValidCommandEventArgs ev, string id)
        {
            ev.IsAllowed = false;
            ev.Response = $"<color=red>Отказ [{id}]</color>";
        }
    }
}