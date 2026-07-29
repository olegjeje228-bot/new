using System;
using System.Linq;
using System.Text;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using EventHUD.Radio;
using UnityEngine;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public sealed class RadioFmAdminCommand : ICommand
    {
        public string Command => "radiofm";

        public string[] Aliases => new[]
        {
            "rfm", "radiof", "radofm", "радиофм", "рфм"
        };

        public string Description =>
            "Управление радио. Подкоманды: spawn, del, list, maxvol, disable, enable, changebat, range";

        private static Config Cfg => Plugin.Instance.Config;

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission("eventhud.manage"))
            {
                response = "Нет прав. Нужен eventhud.manage";
                return false;
            }

            RadioFmSystem system = RadioFmSystem.Instance;

            if (system == null)
            {
                response = "Система радио не запущена.";
                return false;
            }

            if (arguments.Count < 1)
            {
                response = Help();
                return false;
            }

            string sub = arguments.At(0).ToLowerInvariant();

            switch (sub)
            {
                case "spawn":
                    return Spawn(sender, system, out response);

                case "del":
                case "delete":
                case "remove":
                    return Delete(arguments, system, out response);

                case "list":
                    response = system.BuildList();
                    return true;

                case "maxvol":
                case "maxvolume":
                    return MaxVol(arguments, out response);

                case "disable":
                    return SetDisabled(arguments, system, true, out response);

                case "enable":
                    return SetDisabled(arguments, system, false, out response);

                case "changebat":
                case "bat":
                    return ChangeBat(arguments, system, out response);

                case "range":
                    return Range(arguments, system, out response);

                default:
                    response = Help();
                    return false;
            }
        }

        private static string Help()
        {
            return
                "Тутор (админ): radiofm spawn - заспавнить радио перед собой | " +
                "radiofm list - список всех радио | " +
                "radiofm tracks - список треков | " +
                "radiofm del <номер|all> - удалить радио | " +
                "radiofm enable/disable <номер|all> - вкл/откл радио для игроков | " +
                "radiofm changebat <номер|all> - заряд 100% | " +
                "radiofm maxvol <0-5> - макс громкость | " +
                "radiofm range <0-50> - базовый радиус\n\n" +
                "Информация.\n" +
                $"Радио на карте: {RadioFmSystem.Instance?.AllRadios.Count ?? 0} | " +
                $"Треков загружено: {RadioStreamService.TrackList.Count} | " +
                $"Макс. громкость: {Plugin.Instance?.Config.RadioFmMaxVolume ?? 5}";
        }

        private static bool Spawn(ICommandSender sender, RadioFmSystem system, out string response)
        {
            Player player = Player.Get(sender);

            if (player == null)
            {
                response = "Спавнить радио можно только с игрока в игре.";
                return false;
            }

            RadioUnit radio = system.Spawn(player);

            response = $"Радио {radio.Number} заспавнено. Громкость {radio.Volume}, заряд 100%.";
            return true;
        }

        private static bool Delete(ArraySegment<string> args, RadioFmSystem system, out string response)
        {
            if (args.Count < 2)
            {
                response = "radiofm del <номер|all>";
                return false;
            }

            string target = args.At(1).ToLowerInvariant();

            if (target == "all")
            {
                int count = system.RemoveAll();
                response = $"Удалено радио: {count}";
                return true;
            }

            if (!int.TryParse(target, out int number))
            {
                response = "Номер должен быть числом или all.";
                return false;
            }

            if (!system.Delete(number))
            {
                response = $"Радио {number} не найдено.";
                return false;
            }

            response = $"Радио {number} удалено.";
            return true;
        }

        private static bool MaxVol(ArraySegment<string> args, out string response)
        {
            if (args.Count < 2 || !int.TryParse(args.At(1), out int max))
            {
                response = $"Сейчас максимум {Cfg.RadioFmMaxVolume}. Использование: radiofm maxvol 0-5";
                return false;
            }

            max = Mathf.Clamp(max, 0, 5);
            Cfg.RadioFmMaxVolume = max;

            int lowered = RadioFmSystem.Instance.ClampAllVolumes(max);

            response = lowered > 0
                ? $"Максимум громкости {max}. Понижено радио: {lowered}"
                : $"Максимум громкости {max}.";

            return true;
        }

        private static bool SetDisabled(ArraySegment<string> args, RadioFmSystem system, bool disabled, out string response)
        {
            string word = disabled ? "отключено" : "включено в работу";

            if (args.Count < 2)
            {
                response = disabled
                    ? "radiofm disable <номер|all>"
                    : "radiofm enable <номер|all>";
                return false;
            }

            string target = args.At(1).ToLowerInvariant();

            if (target == "all")
            {
                int count = system.SetDisabledAll(disabled);
                response = $"Радио {word}: {count}";
                return true;
            }

            if (!int.TryParse(target, out int number))
            {
                response = "Номер должен быть числом или all.";
                return false;
            }

            if (!system.SetDisabled(number, disabled))
            {
                response = $"Радио {number} не найдено.";
                return false;
            }

            response = $"Радио {number} {word}.";
            return true;
        }

        private static bool ChangeBat(ArraySegment<string> args, RadioFmSystem system, out string response)
        {
            if (args.Count < 2)
            {
                response = "radiofm changebat <номер|all>";
                return false;
            }

            string target = args.At(1).ToLowerInvariant();

            if (target == "all")
            {
                int count = system.ChangeBatteryAll();
                response = $"Заряд 100% выставлен. Радио: {count}";
                return true;
            }

            if (!int.TryParse(target, out int number))
            {
                response = "Номер должен быть числом или all.";
                return false;
            }

            if (!system.ChangeBattery(number))
            {
                response = $"Радио {number} не найдено.";
                return false;
            }

            response = $"Радио {number}, заряд 100%.";
            return true;
        }

        private static bool Range(ArraySegment<string> args, RadioFmSystem system, out string response)
        {
            if (args.Count < 2 || !float.TryParse(args.At(1), out float value))
            {
                response =
                    "radiofm range 0-50\n" +
                    $"Сейчас: {string.Join(", ", Cfg.RadioFmRange.Select(x => x.ToString("0")))}";
                return false;
            }

            system.SetBaseRange(value);

            response =
                "Радиусы обновлены.\n" +
                $"1: {Cfg.RadioFmRange[1]:0}, 2: {Cfg.RadioFmRange[2]:0}, " +
                $"3: {Cfg.RadioFmRange[3]:0}, 4: {Cfg.RadioFmRange[4]:0}, 5: {Cfg.RadioFmRange[5]:0}";

            return true;
        }
    }
}