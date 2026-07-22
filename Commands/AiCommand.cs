using System;
using CommandSystem;
using EventHUD.Ai;
using Exiled.API.Features;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class AiCommand : ICommand
    {
        public string Command { get; } = "ai";
        public string[] Aliases { get; } = Array.Empty<string>();
        public string Description { get; } = "ИИ-ассистент: .ai [вопрос], .ai model [название], .ai reset";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);
            if (player == null)
            {
                response = "Команда только для игроков.";
                return false;
            }

            if (arguments.Count == 0)
            {
                response = "Использование: .ai [вопрос]. Модель: .ai model. Очистить память: .ai reset";
                return false;
            }

            string first = arguments.At(0).ToLowerInvariant();

            if (first == "model")
            {
                if (arguments.Count == 1)
                {
                    response = $"Напиши .ai model [название]. {AiService.ModelList()}\n" +
                               "deepseek - Базовая модель, быстрее чем claude.\n" +
                               "claudefable - Умная модель, проводник - claude, это самая лучшая модель впринципе в ИИ.";
                    return true;
                }

                return AiService.TrySelectModel(player, arguments.At(1), out response);
            }

            if (first == "reset")
            {
                AiMemoryService.Clear(player);
                response = "Память очищена.";
                return true;
            }

            if (!AiService.HasModel(player))
            {
                response = $"Сначала выбери модель: напиши .ai model, доступны deepseek / claudefable";
                return false;
            }

            string denied = AiService.CheckAccess(player);
            if (denied != null)
            {
                response = denied;
                return false;
            }

            string question = string.Join(" ", arguments);
            AiService.Ask(player, question);

            response = $"Вы написали {AiService.TrySelectLabel(player)}, ожидаем ответ";
            return true;
        }
    }
}
