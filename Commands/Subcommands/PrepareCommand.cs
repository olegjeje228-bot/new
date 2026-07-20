using System;
using System.Linq;
using CommandSystem;
using EventHUD.Extensions;

namespace EventHUD.Commands.Subcommands
{
    public class PrepareCommand : ICommand
    {
        public string Command => "prepare";
        public string[] Aliases => new[] { "prep" };
        public string Description => "ev prepare <NRP/LRP/FUNRP/MRP/HRP/FRP> <название>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 2)
            {
                response = "Использование: ev prepare <RP> <название ивента>";
                return false;
            }

            if (!RPTypeExtensions.TryParse(arguments.At(0), out var rpType))
            {
                response = "Неверный тип RP. NRP/LRP/FUNRP/MRP/HRP/FRP";
                return false;
            }

            string eventName = string.Join(" ", arguments.Skip(1));
            int maxLen = Plugin.Instance.Config.MaxEventNameLength;

            if (eventName.Length > maxLen)
            {
                response = $"Название ивента слишком длинное! Максимум {maxLen} символов (сейчас {eventName.Length}).";
                return false;
            }

            EventManager.Instance.Prepare(eventName, rpType, sender);
            response = $"Подготовка ивента запущена: {eventName} [{rpType.GetShortName()}]";
            return true;
        }
    }
}
 