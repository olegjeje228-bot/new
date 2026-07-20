using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using RemoteAdmin;

namespace EventHUD.Ai
{
    public static class AiActionExecutor
    {
        public const string Marker = "[CMD]";

        private static readonly HashSet<string> FullAdmOnly =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cinfo", "setname", "clearinv" };

        private static readonly string[] HardBlocked =
        {
            "ban", "oban", "unban", "kick", "mute", "unmute", "config",
            "warhead", "detonation", "roundrestart", "restartround", "stopnextround",
            "setgroup", "permissions",
        };

        public static string Process(Player requester, string answer, AiPermissionLevel level, bool adminConsole = false)
        {
            var cfg = Plugin.Instance.Config;
            int maxCommands = adminConsole ? cfg.AiAdminMaxCommands : cfg.AiMaxCommandsPerAnswer;
            int executed = 0;

            var sb = new StringBuilder();
            foreach (string rawLine in answer.Split('\n'))
            {
                string line = rawLine.Trim();
                if (!line.StartsWith(Marker, StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine(rawLine);
                    continue;
                }

                string query = line.Substring(Marker.Length).Trim();
                if (query.Length == 0)
                    continue;

                if (executed >= maxCommands)
                {
                    sb.AppendLine("Отклонено: " + query + " (лимит команд за один ответ)");
                    continue;
                }

                executed++;
                sb.AppendLine(Execute(requester, query, level, adminConsole));
            }

            return sb.ToString().TrimEnd();
        }

        private static string Execute(Player requester, string query, AiPermissionLevel level, bool adminConsole)
        {
            if (IsBlocked(query))
                return "Отклонено: " + query + " (команда запрещена для ИИ)";

            string[] parts = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLowerInvariant();
            var cfg = Plugin.Instance.Config;

            if (!adminConsole)
            {
                if (FullAdmOnly.Contains(cmd) && level < AiPermissionLevel.FullAdm)
                    return "Отклонено: " + query + " (нужен режим fulladm)";
                if (level == AiPermissionLevel.Adm && !cfg.AiAdmCommands.Contains(cmd))
                    return "Отклонено: " + query + " (команда не входит в ai_adm_commands)";
                if (level < AiPermissionLevel.Adm)
                    return "Отклонено: " + query + " (режим adm выключен)";
            }

            try
            {
                string builtIn = TryBuiltIn(requester, cmd, parts, query, level, adminConsole);
                if (builtIn != null)
                    return builtIn;

                if (!adminConsole && level < AiPermissionLevel.FullAdm)
                    return "Отклонено: " + query + " (неизвестная команда, нужен fulladm)";

                string raResult = CommandProcessor.ProcessQuery(query, ServerConsole.Scs);
                if (raResult != null && raResult.Length > 200)
                    raResult = raResult.Substring(0, 200);
                return "RA: " + query + " => " + raResult;
            }
            catch (Exception e)
            {
                return "Отклонено: " + query + " (" + e.Message + ")";
            }
        }

        private static bool IsBlocked(string query)
        {
            string lower = " " + query.ToLowerInvariant() + " ";
            foreach (string bad in HardBlocked)
                if (lower.Contains(" " + bad + " ") || lower.StartsWith(" " + bad + " "))
                    return true;
            foreach (string bad in Plugin.Instance.Config.AiBlockedCommands)
                if (!string.IsNullOrWhiteSpace(bad) && lower.Contains(bad.ToLowerInvariant()))
                    return true;
            return false;
        }

        private static List<Player> ResolveTargets(Player requester, string token)
        {
            var result = new List<Player>();
            if (string.IsNullOrEmpty(token))
                return result;

            if (token.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                result.AddRange(Player.List.Where(p => !p.IsHost));
                return result;
            }

            if (token.Equals("me", StringComparison.OrdinalIgnoreCase) || token.Equals("я", StringComparison.OrdinalIgnoreCase))
            {
                if (requester != null)
                    result.Add(requester);
                return result;
            }

            foreach (string piece in token.Split(','))
            {
                if (int.TryParse(piece, out int id))
                {
                    var byId = Player.Get(id);
                    if (byId != null)
                        result.Add(byId);
                }
                else
                {
                    var byName = Player.List.FirstOrDefault(p =>
                        p.Nickname != null && p.Nickname.IndexOf(piece, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (byName != null)
                        result.Add(byName);
                }
            }
            return result;
        }

        private static string TryBuiltIn(Player requester, string cmd, string[] parts, string query, AiPermissionLevel level, bool adminConsole)
        {
            var cfg = Plugin.Instance.Config;

            switch (cmd)
            {
                case "give":
                {
                    if (parts.Length < 3) return "Отклонено: " + query + " (нужно: give <цели> <ItemType>)";
                    if (!Enum.TryParse(parts[2], true, out ItemType item))
                        return "Отклонено: " + query + " (неизвестный ItemType)";
                    var targets = ResolveTargets(requester, parts[1]);
                    if (targets.Count == 0) return "Отклонено: " + query + " (цель не найдена)";
                    foreach (var t in targets) t.AddItem(item);
                    return "Выполнено: выдан " + item + " (" + targets.Count + " игрок(ов))";
                }

                case "ammo":
                {
                    if (parts.Length < 4) return "Отклонено: " + query + " (нужно: ammo <цели> <9|556|762|12|44> <кол-во>)";
                    AmmoType ammo;
                    switch (parts[2].ToLowerInvariant().TrimStart('.'))
                    {
                        case "9": case "9mm": case "9x19": ammo = AmmoType.Nato9; break;
                        case "556": case "5.56": ammo = AmmoType.Nato556; break;
                        case "762": case "7.62": ammo = AmmoType.Nato762; break;
                        case "12": case "12g": case "12gauge": ammo = AmmoType.Ammo12Gauge; break;
                        case "44": ammo = AmmoType.Ammo44Cal; break;
                        default: return "Отклонено: " + query + " (тип патронов: 9, 556, 762, 12, 44)";
                    }
                    if (!ushort.TryParse(parts[3], out ushort count) || count == 0 || count > 500)
                        return "Отклонено: " + query + " (количество 1-500)";
                    var targets = ResolveTargets(requester, parts[1]);
                    if (targets.Count == 0) return "Отклонено: " + query + " (цель не найдена)";
                    foreach (var t in targets) t.AddAmmo(ammo, count);
                    return "Выполнено: " + count + " патронов " + ammo + " (" + targets.Count + " игрок(ов))";
                }

                case "kit":
                {
                    if (parts.Length < 3) return "Отклонено: " + query + " (нужно: kit <цели> <название>)";
                    if (!cfg.AiKits.TryGetValue(parts[2].ToLowerInvariant(), out var items))
                        return "Отклонено: " + query + " (набор не найден, есть: " + string.Join(", ", cfg.AiKits.Keys) + ")";
                    var targets = ResolveTargets(requester, parts[1]);
                    if (targets.Count == 0) return "Отклонено: " + query + " (цель не найдена)";
                    foreach (var t in targets)
                        foreach (ItemType it in items)
                            t.AddItem(it);
                    return "Выполнено: набор " + parts[2] + " (" + targets.Count + " игрок(ов))";
                }

                case "effect":
                {
                    if (parts.Length < 3) return "Отклонено: " + query + " (нужно: effect <цели> <EffectType> [сила] [секунды])";
                    if (!Enum.TryParse(parts[2], true, out EffectType effect))
                        return "Отклонено: " + query + " (неизвестный EffectType)";
                    byte intensity = 1;
                    float duration = 10f;
                    if (parts.Length >= 4) byte.TryParse(parts[3], out intensity);
                    if (parts.Length >= 5) float.TryParse(parts[4], out duration);
                    if (intensity == 0) intensity = 1;
                    if (duration <= 0 || duration > 600) duration = 10f;
                    var targets = ResolveTargets(requester, parts[1]);
                    if (targets.Count == 0) return "Отклонено: " + query + " (цель не найдена)";
                    foreach (var t in targets) t.EnableEffect(effect, intensity, duration);
                    return "Выполнено: эффект " + effect + " (" + targets.Count + " игрок(ов))";
                }

                case "heal":
                {
                    if (parts.Length < 2) return "Отклонено: " + query + " (нужно: heal <цели>)";
                    var targets = ResolveTargets(requester, parts[1]);
                    if (targets.Count == 0) return "Отклонено: " + query + " (цель не найдена)";
                    foreach (var t in targets)
                    {
                        t.Health = t.MaxHealth;
                        t.DisableAllEffects();
                    }
                    return "Выполнено: вылечено " + targets.Count + " игрок(ов)";
                }

                case "forceclass":
                case "fc":
                {
                    if (parts.Length < 3) return "Отклонено: " + query + " (нужно: forceclass <цели> <роль> [all|noinv|nospawn|clean])";
                    if (!Enum.TryParse(parts[2], true, out RoleTypeId role))
                        return "Отклонено: " + query + " (неизвестная роль)";
                    RoleSpawnFlags flags = RoleSpawnFlags.All;
                    if (parts.Length >= 4)
                    {
                        switch (parts[3].ToLowerInvariant())
                        {
                            case "noinv": flags = RoleSpawnFlags.UseSpawnpoint; break;
                            case "nospawn": flags = RoleSpawnFlags.AssignInventory; break;
                            case "clean": case "none": flags = RoleSpawnFlags.None; break;
                        }
                    }
                    var targets = ResolveTargets(requester, parts[1]);
                    if (targets.Count == 0) return "Отклонено: " + query + " (цель не найдена)";
                    if (!adminConsole && level == AiPermissionLevel.Adm)
                        targets = targets.Where(t => t == requester).ToList();
                    if (targets.Count == 0) return "Отклонено: " + query + " (adm может форсить только себя)";
                    foreach (var t in targets) t.Role.Set(role, flags);
                    return "Выполнено: роль " + role + " (" + targets.Count + " игрок(ов))";
                }

                case "clearinv":
                {
                    if (parts.Length < 2) return "Отклонено: " + query + " (нужно: clearinv <цели>)";
                    var targets = ResolveTargets(requester, parts[1]);
                    if (targets.Count == 0) return "Отклонено: " + query + " (цель не найдена)";
                    foreach (var t in targets) t.ClearInventory();
                    return "Выполнено: инвентарь очищен (" + targets.Count + " игрок(ов))";
                }

                case "setname":
                {
                    if (parts.Length < 3) return "Отклонено: " + query + " (нужно: setname <цели> <имя> или setname <цели> reset)";
                    string name = string.Join(" ", parts.Skip(2));
                    var targets = ResolveTargets(requester, parts[1]);
                    if (targets.Count == 0) return "Отклонено: " + query + " (цель не найдена)";
                    if (name.Equals("reset", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var t in targets) t.DisplayNickname = null;
                        return "Выполнено: ники сброшены (" + targets.Count + ")";
                    }
                    name = Util.TextGuard.SoftSanitize(name, 40);
                    if (string.IsNullOrWhiteSpace(name)) return "Отклонено: " + query + " (имя пустое после фильтрации)";
                    foreach (var t in targets) t.DisplayNickname = name;
                    return "Выполнено: ник " + name + " (" + targets.Count + " игрок(ов))";
                }

                case "cinfo":
                {
                    if (parts.Length < 3) return "Отклонено: " + query + " (нужно: cinfo <цели> <текст>)";
                    string text = string.Join(" ", parts.Skip(2));
                    text = adminConsole
                        ? Util.TextGuard.SoftSanitize(text, 400)
                        : (Util.TextGuard.IsSafePlain(text, 60) ? text : null);
                    if (string.IsNullOrWhiteSpace(text)) return "Отклонено: " + query + " (текст не прошел проверку)";
                    var targets = ResolveTargets(requester, parts[1]);
                    if (targets.Count == 0) return "Отклонено: " + query + " (цель не найдена)";
                    foreach (var t in targets) t.CustomInfo = text;
                    return "Выполнено: cinfo (" + targets.Count + " игрок(ов))";
                }

                case "cassie":
                {
                    if (parts.Length < 2) return "Отклонено: " + query + " (нужно: cassie <английские слова>)";
                    string words = Util.TextGuard.SoftSanitize(string.Join(" ", parts.Skip(1)), 400);
                    Exiled.API.Features.Cassie.Message(words, isHeld: false, isNoisy: true, isSubtitles: true);
                    return "Выполнено: cassie";
                }

                case "cassieadv":
                {
                    string rest = string.Join(" ", parts.Skip(1));
                    int sep = rest.IndexOf('|');
                    if (sep < 1) return "Отклонено: " + query + " (нужно: cassieadv <слова> | <субтитры>)";
                    string words = Util.TextGuard.SoftSanitize(rest.Substring(0, sep).Trim(), 400);
                    string subtitles = Util.TextGuard.SoftSanitize(rest.Substring(sep + 1).Trim(), 1500);
                    Exiled.API.Features.Cassie.MessageTranslated(words, subtitles, isHeld: false, isNoisy: true, isSubtitles: true);
                    return "Выполнено: cassieadv";
                }

                default:
                    return null;
            }
        }
    }
}
