using System;
using System.Collections.Generic;
using System.Diagnostics;
using Exiled.API.Features;
using MEC;

namespace EventHUD.Hud
{
    public class HudCompositor
    {
        private readonly Config          _config;
        private readonly EffectScheduler _effects;
        private          CoroutineHandle _tickHandle;
        private readonly HashSet<string> _afkPlayers = new();

        public HudCompositor(Config config, EffectScheduler effects)
        {
            _config  = config;
            _effects = effects;
        }

        public void Start() => _tickHandle = Timing.RunCoroutine(TickLoop());
        public void Stop()  => Timing.KillCoroutines(_tickHandle);

        private IEnumerator<float> TickLoop()
        {
            while (true)
            {
                float interval = GetCurrentInterval();

                foreach (var player in Player.List)
                {
                    try
                    {
                        if (!HudToggleService.IsEnabled(player))
                        {
                            string offNotice = HudNoticeService.GetActive(player);
                            if (!string.IsNullOrEmpty(offNotice))
                            {
                                player.ShowHint(
                                    $"<indent={_config.RadioSwitchHintIndent}%><voffset=10em><size=32>{offNotice}",
                                    interval + 2f);
                            }
                            continue;
                        }

                        // Не показываем HUD для Dummy-игроков и ботов
                        if (player.IsNPC || (player.Nickname != null && player.Nickname.Contains("Dummy")))
                            continue;

                        // AFK: если игрок не двигался > 5 минут — пропускаем
                        if (IsAfk(player))
                        {
                            _afkPlayers.Add(player.UserId);
                            continue;
                        }
                        _afkPlayers.Remove(player.UserId);

                        string text = Build(player);
                        if (!string.IsNullOrEmpty(text))
                            player.ShowHint(text, interval + 2f);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"[HUD] TickLoop exception for {player?.Nickname}: {ex.Message}");
                    }
                }

                yield return Timing.WaitForSeconds(interval);
            }
        }

        private bool IsAfk(Player player)
        {
            try
            {
                // Exiled не имеет встроенного AFK, используем Position как эвристику
                // Упрощённо: если игрок Spectator/Dead — не считаем AFK
                if (!player.IsAlive) return false;
                // Здесь можно добавить кастомный AFK-трекер
                return false;
            }
            catch { return false; }
        }

        private float GetCurrentInterval()
        {
            bool anyEffect = _effects.State.IsMinutePulseActive ||
                             _effects.State.IsFlashActive;

            return anyEffect ? _config.EffectTickInterval : _config.HudUpdateInterval;
        }

        private string Build(Player player)
        {
            var sw = Stopwatch.StartNew();

            string card      = PlayerCardBuilder.Build(player, _config);
            string eventPart = EventStatusBuilder.Build(_config);
            string full      = card + eventPart;

            sw.Stop();
            if (sw.ElapsedMilliseconds > 5 && _config.Debug)
                Log.Debug($"[HUD] Build took {sw.ElapsedMilliseconds}ms for {player.Nickname}");

            // Хинт смены волны (1 сек, поверх всего)
            string radioNotice = Radio.RadioSwitchNoticeService.GetActive(player);
            if (!string.IsNullOrEmpty(radioNotice))
            {
                full +=
                    $"<indent={_config.RadioSwitchHintIndent}%>" +
                    $"<voffset={_config.RadioSwitchHintVoffset}em>" +
                    $"<size={_config.RadioSwitchHintFontSize}>" +
                    radioNotice +
                    "</size>";
            }

            // Общие уведомления (SCP, медицина, команды) — в этом же хинте
            // ВАЖНО: без <align> — align в TMP действует на всю строку и сдвигает весь HUD
            string notice = HudNoticeService.GetActive(player);
            if (!string.IsNullOrEmpty(notice))
            {
                full +=
                    $"<indent={_config.RadioSwitchHintIndent}%>" +
                    "<voffset=10em>" +
                    "<size=32>" + notice + "</size>";
            }

            // Эффекты цвета
            if (_effects.State.IsFlashActive)
            {
                full = EffectApplier.ApplyColorOverlay(
                    full,
                    _effects.State.FlashProgress,
                    _config.ColorFlashColor);
            }
            else if (_effects.State.IsMinutePulseActive)
            {
                full = EffectApplier.ApplyColorOverlay(
                    full,
                    _effects.State.MinutePulseProgress,
                    _config.MinutePulseColor);
            }

            return full;
        }
    }
}
 