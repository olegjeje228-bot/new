using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerEvents = Exiled.Events.Handlers.Player;

namespace EventHUD.Logging
{
    public static class GameLogHandlers
    {
        public static void Register()
        {
            PlayerEvents.Verified += OnVerified;
            PlayerEvents.Left += OnLeft;
            PlayerEvents.Hurting += OnHurting;
            PlayerEvents.Died += OnDied;
            PlayerEvents.Banning += OnBanning;
            PlayerEvents.Kicking += OnKicking;
        }

        public static void Unregister()
        {
            PlayerEvents.Verified -= OnVerified;
            PlayerEvents.Left -= OnLeft;
            PlayerEvents.Hurting -= OnHurting;
            PlayerEvents.Died -= OnDied;
            PlayerEvents.Banning -= OnBanning;
            PlayerEvents.Kicking -= OnKicking;
        }

        private static string Tag(Player p) =>
            p == null ? "[?][?]" : $"[{p.UserId}][{p.Nickname}]";

        private static string DamageName(Exiled.API.Features.DamageHandlers.DamageHandlerBase handler)
        {
            try { return handler?.Type.ToString() ?? "?"; }
            catch { return "?"; }
        }

        // ── Коннекты ──
        private static void OnVerified(VerifiedEventArgs ev) =>
            GameLogService.Game.Add($"[{ev.Player.UserId}] [{ev.Player.Nickname}] Подключился к серверу");

        private static void OnLeft(LeftEventArgs ev) =>
            GameLogService.Game.Add($"[{ev.Player.UserId}] [{ev.Player.Nickname}] Отключился от сервера");

        // ── Урон ──
        private static void OnHurting(HurtingEventArgs ev)
        {
            // Логируем только урон от игрока игроку (без падений, распада, ловушек)
            if (ev.Attacker == null || ev.Player == null || ev.Attacker == ev.Player)
                return;

            GameLogService.Game.Add(
                $"[Урон] {Tag(ev.Attacker)} нанёс {ev.Amount:0} урона [{ev.Player.Nickname}] с помощью ({DamageName(ev.DamageHandler)})");
        }

        // ── Киллы ──
        private static void OnDied(DiedEventArgs ev)
        {
            if (ev.Attacker == null || ev.Attacker == ev.Player)
                GameLogService.Game.Add($"[Килл] {Tag(ev.Player)} погиб ({DamageName(ev.DamageHandler)})");
            else
                GameLogService.Game.Add($"[Килл] {Tag(ev.Attacker)} убил {Tag(ev.Player)} с помощью ({DamageName(ev.DamageHandler)})");
        }

        // ── Модерация: бан / кик (события EXILED) ──
        private static void OnBanning(BanningEventArgs ev)
        {
            string admin = ev.Player == null ? "[SERVER][Консоль]" : Tag(ev.Player);
            GameLogService.Moderation.Add(
                $"Администратор {admin} Забанил игрока {Tag(ev.Target)} на {FormatDuration(ev.Duration)} по причине: {ev.Reason}");
        }

        private static void OnKicking(KickingEventArgs ev)
        {
            string admin = ev.Player == null ? "[SERVER][Консоль]" : Tag(ev.Player);
            GameLogService.Moderation.Add(
                $"Администратор {admin} Кикнул игрока {Tag(ev.Target)} по причине: {ev.Reason}");
        }

        // ── Модерация: муты (события в EXILED нет — ловим RA-команду) ──
        public static void TryLogModerationCommand(CommandSender sender, string command)
        {
            try
            {
                string[] parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    return;

                string verb;
                switch (parts[0].ToLowerInvariant())
                {
                    case "mute": verb = "Замутил"; break;
                    case "unmute": verb = "Размутил"; break;
                    case "imute": verb = "Замутил (интерком)"; break;
                    case "iunmute": verb = "Размутил (интерком)"; break;
                    default: return;
                }

                var names = new List<string>();
                foreach (string token in parts[1].Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    Player target = int.TryParse(token, out int id) ? Player.Get(id) : Player.Get(token);
                    names.Add(target == null ? $"[{token}]" : $"[{target.UserId}][{target.Nickname}]");
                }

                if (names.Count == 0)
                    return;

                string admin = $"[{sender?.SenderId ?? "SERVER"}][{sender?.Nickname ?? "Консоль"}]";
                string msg = names.Count == 1
                    ? $"Администратор {admin} {verb} игрока {names[0]}"
                    : $"Администратор {admin} {verb} {names.Count} игроков {string.Join(", ", names)}";

                GameLogService.Moderation.Add(msg);
            }
            catch { }
        }

        private static string FormatDuration(long seconds)
        {
            if (seconds <= 0) return "навсегда";
            var t = TimeSpan.FromSeconds(seconds);
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours} ч {t.Minutes} мин";
            if (t.TotalMinutes >= 1) return $"{(int)t.TotalMinutes} мин";
            return $"{t.Seconds} сек";
        }
    }
}