using System;
using System.Linq;
using System.Text;
using CommandSystem;
using EventHUD.Medicine;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class InjuriesCommand : ICommand
    {
        public string Command => "injuries";
        public string[] Aliases => new[] { "health", "myinjuries" };
        public string Description => "Показать свои текущие травмы и состояния";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null)
            {
                response = "Только для игроков.";
                return false;
            }

            if (!MedicalStorage.TryGet(player.UserId, out var state) || !state.HasAnything)
            {
                response = "Состояние: Норма. Травм нет.";
                return true;
            }

            var sb = new StringBuilder();
            sb.AppendLine("═══ Медицинское состояние ═══");

            // Глобальные
            if (state.Conditions.Count > 0)
            {
                sb.AppendLine("\n▸ Состояния:");
                foreach (var cond in state.Conditions.OrderByDescending(c => c.GetPriority()))
                {
                    if (cond == GlobalCondition.Normal)
                        continue;
                    sb.AppendLine($"  • {cond.GetFullName()}");
                }
            }

            // Локальные
            if (state.Injuries.Count > 0)
            {
                sb.AppendLine("\n▸ Травмы:");
                foreach (var injury in state.Injuries.OrderByDescending(i => i.Type.GetPriority()))
                {
                    sb.AppendLine($"  • {injury.ToFullString()}");
                }
            }

            // Счётчики
            sb.AppendLine($"\n▸ Адреналин за жизнь: {state.AdrenalineUsed}");
            sb.AppendLine($"▸ Обезболивающее за жизнь: {state.PainkillerUsed}");

            response = sb.ToString();
            return true;
        }
    }
}
 