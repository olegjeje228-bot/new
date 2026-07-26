using System;
using System.Linq;
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

        public string Description => "FM-радио: .radiofm — туториал, on/off/list/volume/battery/changebat";

        private static string Tutorial =>
            "Тутор: .radiofm on random/имя файла | " +
            "Имя файла можно найти в .radiofm list | " +
            "Так-же регулировать доп функции: .radiofm volume/battery | " +
            "если закончится батарейка, возьмите монетку в руки и напишите .radiofm changebat";

        private static string BuildInfo(RadioUnit radio)
        {
            if (radio == null)
                return "Информация.\n[Вы не рядом с radio]";

            string state = radio.Disabled
                ? "отключено администрацией"
                : radio.IsOn ? $"играет: {radio.CurrentTrack}" : "выключено";

            return
                "Информация.\n" +
                $"Радио {radio.Number} - {state}\n" +
                $"Громкость: {radio.Volume} (слышно на {RadioFmSystem.Instance.GetRange(radio.Volume):0} м)\n" +
                $"Заряд: {radio.BatteryLeft:0.#}%";
        }

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);
            if (player == null)
            {
                response = "Команда доступна только игрокам.";
                return false;
            }

            if (RadioFmSystem.Instance == null)
            {
                response = "Система радио не запущена.";
                return false;
            }

            float useDist = Plugin.Instance?.Config.RadioFmUseDistance ?? 3f;
            RadioUnit radio = RadioFmSystem.Instance.GetNearest(player.Position, useDist);

            // ── Без аргументов: всегда туториал + информация ──
            if (arguments.Count < 1)
            {
                response = Tutorial + "\n\n" + BuildInfo(radio);
                return true;
            }

            string action = arguments.At(0).Trim().ToLowerInvariant();

            // ── Список треков доступен с любого расстояния ──
            if (action == "list" || action == "tracks" || action == "список")
            {
                response = RadioStreamService.BuildTracksList();
                return true;
            }

            // ── Остальные действия требуют радио рядом ──
            if (radio == null)
            {
                response = Tutorial + "\n\nИнформация.\n[Вы не рядом с radio]";
                return false;
            }

            switch (action)
            {
                case "on":
                case "вкл":
                case "включить":
                {
                    string track = "random";
                    if (arguments.Count >= 2)
                    {
                        var parts = new string[arguments.Count - 1];
                        for (int i = 1; i < arguments.Count; i++)
                            parts[i - 1] = arguments.At(i);
                        track = string.Join(" ", parts);
                    }

                    if (!RadioFmSystem.Instance.TurnOn(radio, out string error, track))
                    {
                        response = error;
                        return false;
                    }

                    response = $"Радио {radio.Number} включено: {radio.CurrentTrack}. Громкость {radio.Volume}.";
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

                    // Перезапускаем тот же трек с новой громкостью (Stop внутри Play)
                    if (radio.IsOn)
                    {
                        string cur = string.IsNullOrEmpty(radio.CurrentTrack)
                            ? RadioStreamService.PickRandom()
                            : radio.CurrentTrack;

                        RadioStreamService.Play(radio, cur, RadioFmSystem.Instance.GetRange(vol));
                    }

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
                        player.Broadcast(Plugin.Instance?.Config.RadioFmShockBroadcastSeconds ?? 5,
                            "<color=red>Вы попытались поменять батарейки в работающей радио, круто придумали. ");
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
                    response = "Батарейка заменена. Заряд 100%.";
                    return true;
                }

                default:
                    response = Tutorial + "\n\n" + BuildInfo(radio);
                    return false;
            }
        }
    }
}