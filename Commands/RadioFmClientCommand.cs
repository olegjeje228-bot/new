using System;
using CommandSystem;
using EventHUD.Radio;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Enums;
using UnityEngine;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class RadioFmClientCommand : ICommand
    {
        public string Command => "radiofm";

        public string[] Aliases => new[] { "rfm", "радио", "радиофм" };

        public string Description => "Управление FM-радио: .radiofm on/off/volume/battery/changebat";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);
            if (player == null)
            {
                response = "Команда доступна только игрокам.";
                return false;
            }

            RadioUnit radio = RadioFmSystem.Instance?.GetNearest(player.Position, Plugin.Instance?.Config.RadioFmUseDistance ?? 3f);
            if (radio == null)
            {
                response = "Рядом нет радио. Подойдите ближе чем на 3 метра.";
                return false;
            }

            if (arguments.Count < 1)
            {
                response = $"Использование: .radiofm on/off/volume/battery/changebat\nРадио {radio.Number}, громкость {radio.Volume}, заряд {radio.BatteryLeft:0.#}%";
                return true;
            }

            string action = arguments.At(0).Trim().ToLowerInvariant();

            switch (action)
            {
                case "on":
                case "вкл":
                case "включить":
                {
                    if (!RadioFmSystem.Instance.TurnOn(radio, out string error))
                    {
                        response = error;
                        return false;
                    }
                    response = $"Радио {radio.Number} включено. Громкость {radio.Volume}.";
                    return true;
                }

                case "off":
                case "выкл":
                case "выключить":
                {
                    RadioFmSystem.Instance.TurnOff(radio);
                    response = $"Радио {radio.Number} выключено.";
                    return true;
                }

                case "volume":
                case "vol":
                case "громкость":
                {
                    if (arguments.Count < 2 || !int.TryParse(arguments.At(1), out int vol))
                    {
                        response = $"Текущая громкость {radio.Volume}. Использование: .radiofm volume 0-5";
                        return false;
                    }

                    int max = Mathf.Clamp(Plugin.Instance?.Config.RadioFmMaxVolume ?? 5, 0, 5);
                    vol = Mathf.Clamp(vol, 0, max);
                    radio.Volume = vol;

                    if (radio.IsOn)
                        RadioStreamService.StopUnit(radio);

                    response = $"Громкость {vol}. Слышно на {RadioFmSystem.Instance.GetRange(vol):0} метров.";
                    return true;
                }

                case "battery":
                case "bat":
                case "батарея":
                {
                    float seconds = radio.Volume == 0
                        ? 0f
                        : radio.BatteryLeft / 100f * RadioFmSystem.Instance.GetBatterySecondsPublic(radio.Volume);

                    string left = radio.Volume == 0
                        ? "бесконечно"
                        : $"{(int)(seconds / 3600)} ч {(int)(seconds % 3600 / 60)} мин";

                    response =
                        $"Радио {radio.Number}\n" +
                        $"Заряд: {radio.BatteryLeft:0.#}%\n" +
                        $"Громкость: {radio.Volume}\n" +
                        $"Хватит примерно на: {left}\n" +
                        $"Слышно на: {RadioFmSystem.Instance.GetRange(radio.Volume):0} м";
                    return true;
                }

                case "changebat":
                case "сменитьбатарею":
                case "батарейка":
                {
                    if (radio.IsOn)
                    {
                        player.Hurt(Plugin.Instance?.Config.RadioFmShockDamage ?? 20f, "Удар током от радио");
                        player.EnableEffect(EffectType.Flashed, Plugin.Instance?.Config.RadioFmShockFlashSeconds ?? 2f);
                        player.Broadcast(Plugin.Instance?.Config.RadioFmShockBroadcastSeconds ?? 5, "<color=red>Вы попытались поменять батарейки в работающей радио, круто придумали.</color>");
                        response = "Радио работает. Сначала выключите его.";
                        return false;
                    }

                    Item held = player.CurrentItem;
                    if (held == null || held.Type != ItemType.Coin)
                    {
                        response = "Возьмите монетку в руки.";
                        return false;
                    }

                    player.RemoveItem(held);
                    radio.BatteryLeft = 100f;
                    response = $"Батарейка заменена. Заряд 100%.";
                    return true;
                }

                default:
                    response = $"Неизвестная команда. Использование: .radiofm on/off/volume/battery/changebat";
                    return false;
            }
        }
    }
}