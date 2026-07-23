using System;
using EventHUD.Rpm;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;

namespace EventHUD.Radio
{
    using ExiledRadio = Exiled.API.Features.Items.Radio;

    public class RadioEventHandlers
    {
        private readonly Config _config;

        public RadioEventHandlers(Config config) => _config = config;

        // ── Выдача рации ПХ при спавне ──────────────────────────────────────────

        /// <summary>
        /// ПХ (Chaos Insurgency) не имеют рации по умолчанию.
        /// При спавне с reset inventory = true выдаём рацию дополнительно.
        /// </summary>
        public void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Radio))
                return;

            if (ev.Player == null) return;

            // Только для ПХ
            if (ev.NewRole != RoleTypeId.ChaosConscript &&
                ev.NewRole != RoleTypeId.ChaosMarauder &&
                ev.NewRole != RoleTypeId.ChaosRepressor &&
                ev.NewRole != RoleTypeId.ChaosRifleman)
                return;

            // Выдаём рацию с задержкой, чтобы инвентарь успел обновиться после спавна
            Timing.CallDelayed(0.3f, () =>
            {
                if (ev.Player == null || !ev.Player.IsAlive) return;
                if (ev.Player.Role.Team != Team.ChaosInsurgency) return;

                // Проверяем, нет ли уже рации
                bool hasRadio = false;
                foreach (var item in ev.Player.Items)
                {
                    if (item is ExiledRadio) { hasRadio = true; break; }
                }
                if (hasRadio) return;

                try
                {
                    var radio = (ExiledRadio)ExiledRadio.Create(ItemType.Radio);
                    ev.Player.AddItem(radio);
                }
                catch { }
            });
        }

        // ── Подбор рации ─────────────────────────────────────────────────────────

        public void OnItemAdded(ItemAddedEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Radio))
                return;

            if (ev.Item is not ExiledRadio radio)
                return;

            var state = RadioFrequencyStorage.GetOrCreate(radio.Serial);

            // Рация уже была настроена (например лежала у СБ, подобрал Д-класс).
            // Не переназначаем — она остаётся на волне СБ.
            if (state.IsAssigned)
                return;

            var availableTeams = RadioModeProvider.GetAvailableTeams(ev.Player);
            var defaultTeam    = availableTeams.Count > 0
                ? availableTeams[0]
                : RadioTeam.Unknown;

            state.Team         = defaultTeam;
            state.Frequency    = defaultTeam.GetFrequency(_config);
            state.AllowedTeams = availableTeams;
            state.IsAssigned   = true;
        }

        // ── ЛКМ: смена волны ─────────────────────────────────────────────────────

        /// <summary>
        /// Перехватываем нажатие ЛКМ (ванильная смена дальности).
        /// Заменяем поведение: крутим список доступных командных волн
        /// + Custom-частота из SSS как дополнительный пункт.
        /// Дальность всегда бесконечна пока модуль Radio включён.
        /// </summary>
        public void OnChangingRadioPreset(ChangingRadioPresetEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Radio))
                return;

            ev.IsAllowed = false;

            var state = RadioFrequencyStorage.GetOrCreate(ev.Radio.Serial);
            var teams = (state.AllowedTeams != null && state.AllowedTeams.Count > 0)
                ? state.AllowedTeams
                : RadioModeProvider.GetAvailableTeams(ev.Player);
            int teamCount = teams.Count;

            var eventWaves = EventWaveStorage.GetAvailableFor(ev.Player);
            int eventCount = eventWaves.Count;

            float? customFreq = RadioCustomFrequencyStorage.Get(ev.Player.UserId);
            bool hasCustom = customFreq.HasValue;

            int totalModes = teamCount + eventCount + (hasCustom ? 1 : 0);
            if (totalModes == 0)
                return;

            int currentIndex;

            if (state.Team == RadioTeam.Custom && hasCustom)
                currentIndex = teamCount + eventCount;
            else if (state.Team == RadioTeam.Event)
            {
                currentIndex = -1;
                for (int i = 0; i < eventCount; i++)
                {
                    if (string.Equals(eventWaves[i].Name, state.EventWaveName, StringComparison.OrdinalIgnoreCase))
                    {
                        currentIndex = teamCount + i;
                        break;
                    }
                }
                if (currentIndex < 0)
                    currentIndex = totalModes - 1;
            }
            else
            {
                currentIndex = teams.IndexOf(state.Team);
                if (currentIndex < 0)
                    currentIndex = 0;
            }

            int nextIndex = (currentIndex + 1) % totalModes;
            string hintText;

            if (nextIndex < teamCount)
            {
                var nextTeam = teams[nextIndex];
                state.Team = nextTeam;
                state.EventWaveName = null;
                state.Frequency = nextTeam.GetFrequency(_config);
                hintText = string.Format(_config.RadioSwitchHintTemplate, nextTeam.GetColor(), nextTeam.GetDisplayName());
            }
            else if (nextIndex < teamCount + eventCount)
            {
                var wave = eventWaves[nextIndex - teamCount];
                state.Team = RadioTeam.Event;
                state.EventWaveName = wave.Name;
                state.Frequency = wave.Frequency;
                hintText = string.Format(_config.RadioSwitchHintTemplate, RadioTeam.Event.GetColor(), wave.Name);
            }
            else
            {
                state.Team = RadioTeam.Custom;
                state.EventWaveName = null;
                state.Frequency = customFreq!.Value;
                hintText = string.Format(_config.RadioSwitchHintTemplate, RadioTeam.Custom.GetColor(), $"{customFreq.Value:0.0}");
            }

            RadioSwitchNoticeService.Show(ev.Player, hintText, _config.RadioSwitchHintDuration);
        }

        // ── Батарея ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Пока модуль Radio включён — батарея не тратится.
        /// </summary>
        public void OnUsingRadioBattery(UsingRadioBatteryEventArgs ev)
        {
            ev.Drain = 0f;
        }
    }
}
 