using System;
using System.Linq;
using EventHUD.Rpm;
using Exiled.API.Features;
using UserSettings.ServerSpecific;

namespace EventHUD.Radio
{
    /// <summary>
    /// Регистрирует Server-Specific Setting «Radio HZ» (слайдер 0–400).
    ///
    /// Правила по ТЗ:
    ///   • Диапазон слайдера: 0–400.
    ///   • 0 = не задана (Custom-волна недоступна).
    ///   • Значения 1–9 и 391–400 принудительно зажимаются к границам 10–390.
    ///   • При изменении — сохраняем в RadioCustomFrequencyStorage.
    ///   • Если игрок сейчас на Custom-волне — обновляем частоту рации немедленно.
    ///
    /// Совместимость:
    ///   Добавляем слайдер к существующему массиву DefinedSettings,
    ///   а не заменяем его целиком — чтобы не сломать другие плагины с SSS.
    /// </summary>
    public static class ServerSpecificSettingsHandler
    {
        private static bool _registered;
        private static int  _settingId;

        public static void Register(Config config)
        {
            if (_registered)
                return;

            _registered = true;
            _settingId  = config.RadioHzSettingId;

            var slider = new SSSliderSetting(
                _settingId,
                "Radio HZ",
                0f,
                400f,
                0f,
                false,
                "0",
                "{0}",
                "Свободная частота рации (10–390). 0 = не используется."
            );

            EventHUD.Hud.SssRoleSync.HumanSettings.Add(slider);

            ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnSettingChanged;
        }

        public static void Unregister()
        {
            if (!_registered)
                return;

            _registered = false;

            ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnSettingChanged;
        }

        private static void OnSettingChanged(
            ReferenceHub hub,
            ServerSpecificSettingBase setting)
        {
            if (setting.SettingId != _settingId)
                return;

            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Radio))
                return;

            if (setting is not SSSliderSetting slider)
                return;

            float raw    = slider.Integer ? slider.SyncIntValue : slider.SyncFloatValue;
            var   player = Player.Get(hub);

            if (player == null)
                return;

            // 0 = игрок сбросил слайдер → убираем Custom-волну
            if (raw < 1f)
            {
                RadioCustomFrequencyStorage.Remove(player.UserId);
                // Если сейчас на Custom — сбрасываем на первую доступную командную волну
                ResetCustomIfActive(player);
                return;
            }

            // Зажимаем к допустимому диапазону 10–390
            float clamped = Clamp(raw, 10f, 390f);

            // Запрет совпадения с зарезервированными командными частотами
            var config = Plugin.Instance.Config;
            float[] reserved = { config.RadioFreqUnknown, config.RadioFreqFacility, config.RadioFreqSecurity, config.RadioFreqMtf, config.RadioFreqChaos };
            foreach (var r in reserved)
            {
                if (Math.Abs(clamped - r) < 0.1f)
                {
                    clamped = r + 1f;
                    break;
                }
            }

            RadioCustomFrequencyStorage.Set(player.UserId, clamped);

            // Если игрок сейчас на Custom-волне — обновляем частоту немедленно
            foreach (var item in player.Items)
            {
                if (item is Exiled.API.Features.Items.Radio radio)
                {
                    if (RadioFrequencyStorage.TryGet(radio.Serial, out var state) &&
                        state.Team == RadioTeam.Custom)
                    {
                        state.Frequency = clamped;
                    }
                }
            }
        }

        /// <summary>
        /// Если игрок находился на Custom-волне, а частота была сброшена —
        /// переключаем его на первую доступную командную волну.
        /// </summary>
        private static void ResetCustomIfActive(Player player)
        {
            foreach (var item in player.Items)
            {
                if (item is Exiled.API.Features.Items.Radio radio)
                {
                    if (!RadioFrequencyStorage.TryGet(radio.Serial, out var state))
                        continue;

                    if (state.Team != RadioTeam.Custom)
                        continue;

                    var teams = RadioModeProvider.GetAvailableTeams(player);
                    if (teams.Count == 0)
                        return;

                    var config = Plugin.Instance.Config;
                    state.Team      = teams[0];
                    state.Frequency = teams[0].GetFrequency(config);
                }
            }
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
 