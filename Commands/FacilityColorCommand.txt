using System;
using System.Globalization;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using UnityEngine;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public sealed class FacilityColorCommand : ICommand
    {
        public string Command => "facilitycolor";

        public string[] Aliases => new[] { "ccolor" };

        public string Description =>
            "Применяет цвет ко всем комнатам: " +
            "facilitycolor <r> <g> <b>. Без аргументов — сброс.";

        public bool Execute(
            ArraySegment<string> arguments,
            ICommandSender sender,
            out string response)
        {
            if (arguments.Count == 0)
            {
                foreach (Room room in Room.List)
                    room.ResetColor();

                response = "Цвет всех комнат сброшен.";
                return true;
            }

            if (arguments.Count != 3 ||
                !TryParse(arguments.At(0), out float red) ||
                !TryParse(arguments.At(1), out float green) ||
                !TryParse(arguments.At(2), out float blue))
            {
                response =
                    "Использование: facilitycolor <r> <g> <b> " +
                    "(0–1000) или facilitycolor для сброса.";

                return false;
            }

            red = Mathf.Clamp(red, 0f, 1000f);
            green = Mathf.Clamp(green, 0f, 1000f);
            blue = Mathf.Clamp(blue, 0f, 1000f);

            Color color = new Color(
                red / 255f,
                green / 255f,
                blue / 255f);

            int changedRooms = 0;

            foreach (Room room in Room.List)
            {
                room.Color = color;
                changedRooms++;
            }

            response =
                $"Цвет {red:0.##};{green:0.##};{blue:0.##} " +
                $"применён к {changedRooms} комнатам.";

            return true;
        }

        private static bool TryParse(
            string value,
            out float result)
        {
            return float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result);
        }
    }
}