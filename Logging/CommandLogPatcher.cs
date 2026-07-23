using System;
using HarmonyLib;
using RemoteAdmin;

namespace EventHUD.Logging // подставить фактический namespace проекта
{
    public static class CommandLogPatcher
    {
        private static Harmony _harmony;

        private static string _pendingUserId;
        private static string _pendingCommand;

        private static string _lastRaLine;
        private static DateTime _lastRaTime;

        public static void Register()
        {
            try
            {
                _harmony = new Harmony("eventhud.commandlog");

                // Обе перегрузки ProcessQuery: string-версия внутри вызывает segment-версию,
                // патчим обе явно, дубликат отсекаем в RaPostfix.
                var raSegment = AccessTools.Method(typeof(CommandProcessor), nameof(CommandProcessor.ProcessQuery),
                    new[] { typeof(ArraySegment<string>), typeof(CommandSender) });
                var raString = AccessTools.Method(typeof(CommandProcessor), nameof(CommandProcessor.ProcessQuery),
                    new[] { typeof(string), typeof(CommandSender) });

                if (raSegment != null)
                    _harmony.Patch(raSegment, postfix: new HarmonyMethod(typeof(CommandLogPatcher), nameof(RaPostfix)));
                if (raString != null)
                    _harmony.Patch(raString, postfix: new HarmonyMethod(typeof(CommandLogPatcher), nameof(RaPostfix)));

                DebugFileLog.Write($"[CommandLog] RA-патчи: segment={raSegment != null}, string={raString != null}");

                _harmony.Patch(
                    AccessTools.Method(typeof(QueryProcessor), nameof(QueryProcessor.ProcessGameConsoleQuery)),
                    prefix: new HarmonyMethod(typeof(CommandLogPatcher), nameof(ConsolePrefix)));

                _harmony.Patch(
                    AccessTools.Method(typeof(GameConsoleTransmission), nameof(GameConsoleTransmission.SendToClient)),
                    postfix: new HarmonyMethod(typeof(CommandLogPatcher), nameof(ConsoleReplyPostfix)));

                Exiled.API.Features.Log.Info("[CommandLog] Патчи логирования команд установлены.");
                DebugFileLog.Write("[CommandLog] Патчи установлены OK.");
            }
            catch (Exception e)
            {
                Exiled.API.Features.Log.Error($"[CommandLog] Не удалось установить патчи: {e}");
                DebugFileLog.Write($"[CommandLog] ОШИБКА установки патчей: {e}");
            }
        }

        public static void Unregister()
        {
            try
            {
                _harmony?.UnpatchAll("eventhud.commandlog");
            }
            catch { }
            _harmony = null;
        }

        // ── RA-команды: принимает и string, и ArraySegment<string> ──
        private static void RaPostfix(object[] __args, string __result)
        {
            try
            {
                if (__args == null || __args.Length < 2)
                    return;

                string command = __args[0] is ArraySegment<string> seg
                    ? string.Join(" ", seg)
                    : __args[0] as string;

                if (string.IsNullOrWhiteSpace(command))
                    return;

                command = command.Trim();

                if (command.StartsWith("$") ||
                    command.StartsWith("REQUEST_DATA", StringComparison.OrdinalIgnoreCase))
                    return;

                var sender = __args[1] as CommandSender;
                string line = $"{sender?.SenderId ?? "SERVER"} {command}";

                // Одна команда проходит обе перегрузки подряд — дубликат не логируем
                if (line == _lastRaLine && (DateTime.UtcNow - _lastRaTime).TotalSeconds < 1)
                    return;

                _lastRaLine = line;
                _lastRaTime = DateTime.UtcNow;

                CommandLogService.Log(sender?.SenderId ?? "SERVER", command, __result);
                GameLogHandlers.TryLogModerationCommand(sender, command);
            }
            catch { }
        }

        // ── Консоль игрока: точку клиент отрезает, фильтра по "." больше нет ──
        private static void ConsolePrefix(QueryProcessor __instance, object[] __args)
        {
            try
            {
                string query = __args != null && __args.Length > 0 ? __args[0] as string : null;

                if (string.IsNullOrWhiteSpace(query))
                {
                    _pendingUserId = null;
                    return;
                }

                var player = Exiled.API.Features.Player.Get(__instance.gameObject);
                _pendingUserId = player?.UserId ?? "unknown";
                _pendingCommand = "." + query.Trim(); // возвращаем точку для читаемости
            }
            catch
            {
                _pendingUserId = null;
            }
        }

        // ── Консоль игрока: первый ответ после команды ──
        private static void ConsoleReplyPostfix(object[] __args)
        {
            try
            {
                if (_pendingUserId == null || __args == null)
                    return;

                string text = null;
                foreach (var arg in __args)
                {
                    if (arg is string s) { text = s; break; }
                }

                CommandLogService.Log(_pendingUserId, _pendingCommand, text ?? "-");
                _pendingUserId = null;
                _pendingCommand = null;
            }
            catch { }
        }
    }
}