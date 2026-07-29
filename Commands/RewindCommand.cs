using System;
using System.Collections.Generic;
using CommandSystem;
using Exiled.API.Features;
using MEC;
using UnityEngine;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class RewindCommand : ICommand
    {
        public string Command => "rewind";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Заточка ножниц SCP-1509: 10 секунд без движения.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);
            if (player == null) { response = "Команда только для игроков."; return false; }

            if (!Tripwire.TripwireSystem.IsScissorsType(player.CurrentItem?.Type ?? ItemType.None))
            { response = "Возьмите ножницы (SCP-1509) в руки."; return false; }

            if (Tripwire.TripwireSystem.Sharpening.Contains(player.UserId))
            { response = "Вы уже точите ножницы."; return false; }

            // Сброс износа
            Tripwire.TripwireSystem.KnifeWear.Remove(player.UserId);
            Tripwire.TripwireSystem.KnifeLimit.Remove(player.UserId);
            Tripwire.TripwireSystem.Sharpening.Add(player.UserId);

            Timing.RunCoroutine(SharpeningCoroutine(player));
            response = "Началась заточка ножниц (10 сек). Не убирайте ножницы и не умирайте.";
            return true;
        }

        private static IEnumerator<float> SharpeningCoroutine(Player player)
        {
            string uid = player.UserId;
            Vector3 startPos = player.Position;
            ItemType held = player.CurrentItem?.Type ?? ItemType.None;

            for (int i = 10; i > 0; i--)
            {
                // Проверки на прерывание
                if (player == null || !player.IsAlive ||
                    player.CurrentItem?.Type != held ||
                    Vector3.Distance(player.Position, startPos) > 0.5f ||
                    !Tripwire.TripwireSystem.Sharpening.Contains(uid))
                {
                    Tripwire.TripwireSystem.Sharpening.Remove(uid);
                    if (player != null)
                        player.ShowHint("<color=red>Заточка прервана</color>", 2f);
                    yield break;
                }

                player.ShowHint($"<color=yellow>Заточка: {i}/10 сек</color>", 1.2f);
                yield return Timing.WaitForSeconds(1f);
            }

            Tripwire.TripwireSystem.Sharpening.Remove(uid);
            Tripwire.TripwireSystem.KnifeWear[uid] = 0;
            Tripwire.TripwireSystem.KnifeLimit[uid] = UnityEngine.Random.Range(10, 16);
            player.ShowHint("<color=green>Ножницы заточены</color>", 2f);
        }
    }
}