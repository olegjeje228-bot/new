using System;
using System.Collections.Generic;
using System.Linq;
using CommandSystem;
using EventHUD.Radio;
using PlayerRoles;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class RadioWaveCommand : ICommand
    {
        public string Command => "radio";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Ивент-волны рации: radio wave add/remove/list";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var args = arguments.ToArray();

            if (args.Length < 2 || !string.Equals(args[0], "wave", StringComparison.OrdinalIgnoreCase))
            {
                response = "Usage:\nradio wave add global <название>\nradio wave add roles <Role1,Role2,...> <название>\nradio wave remove <название>\nradio wave list";
                return false;
            }

            switch (args[1].ToLowerInvariant())
            {
                case "add": return Add(args, out response);
                case "remove": return Remove(args, out response);
                case "list": return List(out response);
                default:
                    response = "Доступно: add, remove, list.";
                    return false;
            }
        }

        private bool Add(string[] args, out string response)
        {
            if (args.Length < 4)
            {
                response = "Usage: radio wave add global <название> | radio wave add roles <Role1,Role2> <название>";
                return false;
            }

            string scope = args[2].ToLowerInvariant();
            HashSet<RoleTypeId> roles = null;
            int nameStart;

            if (scope == "global")
            {
                nameStart = 3;
            }
            else if (scope == "roles")
            {
                roles = new HashSet<RoleTypeId>();
                int i = 3;

                while (i < args.Length)
                {
                    var parts = args[i].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    bool allParsed = parts.Length > 0;
                    var parsed = new List<RoleTypeId>();

                    foreach (var part in parts)
                    {
                        if (Enum.TryParse(part.Trim(), true, out RoleTypeId role))
                            parsed.Add(role);
                        else { allParsed = false; break; }
                    }

                    if (!allParsed)
                        break;

                    foreach (var r in parsed)
                        roles.Add(r);
                    i++;

                    if (!args[i - 1].EndsWith(","))
                        break;
                }

                if (roles.Count == 0)
                {
                    response = "Не удалось распознать роли. Пример: radio wave add roles NtfSergeant,NtfPrivate МОГ РЯДОВЫЕ";
                    return false;
                }

                nameStart = i;
            }
            else
            {
                response = "Укажи global или roles.";
                return false;
            }

            if (nameStart >= args.Length)
            {
                response = "Укажи название волны.";
                return false;
            }

            string name = string.Join(" ", args.Skip(nameStart)).Trim();

            if (!EventWaveStorage.Add(name, roles, out string error))
            {
                response = error;
                return false;
            }

            response = $"Волна '{name}' добавлена (доступ: {(roles == null ? "все" : string.Join(", ", roles))}). Действует до конца раунда / ev stop.";
            return true;
        }

        private bool Remove(string[] args, out string response)
        {
            if (args.Length < 3)
            {
                response = "Usage: radio wave remove <название>";
                return false;
            }

            string name = string.Join(" ", args.Skip(2)).Trim();

            if (!EventWaveStorage.Remove(name))
            {
                response = $"Волна '{name}' не найдена.";
                return false;
            }

            var config = Plugin.Instance.Config;
            foreach (var st in RadioFrequencyStorage.All)
            {
                if (st.Team == RadioTeam.Event &&
                    string.Equals(st.EventWaveName, name, StringComparison.OrdinalIgnoreCase))
                {
                    st.Team = (st.AllowedTeams != null && st.AllowedTeams.Count > 0)
                        ? st.AllowedTeams[0]
                        : RadioTeam.Unknown;
                    st.EventWaveName = null;
                    st.Frequency = st.Team.GetFrequency(config);
                }
            }

            response = $"Волна '{name}' удалена.";
            return true;
        }

        private bool List(out string response)
        {
            if (EventWaveStorage.Waves.Count == 0)
            {
                response = "Ивент-волн нет.";
                return true;
            }

            var lines = new List<string>();
            foreach (var w in EventWaveStorage.Waves)
                lines.Add($"'{w.Name}' — {(w.Roles == null || w.Roles.Count == 0 ? "все" : string.Join(", ", w.Roles))}");
            response = string.Join("\n", lines);
            return true;
        }
    }
}