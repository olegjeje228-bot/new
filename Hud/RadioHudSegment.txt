using EventHUD.Radio;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;

namespace EventHUD.Hud
{
    using ExiledRadio = Exiled.API.Features.Items.Radio;

    /// <summary>
    /// Строка "Волна рации: МОГ" в карточке игрока.
    ///
    /// Показывается если:
    ///   • Роль не СЦП, не Overwatch, не Spectator, не Filmmaker.
    ///   • В инвентаре есть рация (необязательно в руках).
    ///
    /// Дополнительно показывает числовую частоту рядом с волной,
    /// если игрок держит чужую рацию (волна не принадлежит его роли)
    /// и частота ниже 300 — чтобы Д-класс мог передать информацию другим.
    /// </summary>
    public static class RadioHudSegment
    {
        private static readonly RoleTypeId[] _excluded =
        {
            RoleTypeId.Spectator,
            RoleTypeId.Overwatch,
            RoleTypeId.Filmmaker
        };

        public static string Build(Player player, Config c, float? voffsetOverride = null)
        {
            // СЦП — не показываем
            if (player.Role.Team == Team.SCPs)
                return string.Empty;

            // Исключённые роли
            foreach (var role in _excluded)
                if (player.Role.Type == role)
                    return string.Empty;

            // Ищем рацию в инвентаре
            ExiledRadio radio = null;
            foreach (var item in player.Items)
            {
                if (item is ExiledRadio r) { radio = r; break; }
            }

            if (radio == null)
                return string.Empty;

            // Если рация не настроена — показываем "?"
            if (!RadioFrequencyStorage.TryGet(radio.Serial, out var state))
            {
                float unassignedVoffset = voffsetOverride ?? c.RadioWaveVoffset;
                return $"<voffset={unassignedVoffset}em><indent={c.RadioWaveIndent}%>" +
                       $"{c.RadioWaveLabel}: <color={RadioTeam.Unknown.GetColor()}>{RadioTeam.Unknown.GetDisplayName()}</color>";
            }

            // Метка волны
            string label = state.Team == RadioTeam.Custom
                ? $"{state.Frequency:0.0}"
                : state.Team.GetDisplayName();

            string color     = state.Team.GetColor();
            string freqHint  = BuildFrequencyHint(player, state);

            float voffset = voffsetOverride ?? c.RadioWaveVoffset;

            return
                $"<voffset={voffset}em><indent={c.RadioWaveIndent}%>" +
                $"{c.RadioWaveLabel}: <color={color}>{label}</color>" +
                freqHint;
        }

        /// <summary>
        /// Если игрок держит "чужую" рацию (волна не входит в список его роли)
        /// и частота меньше 300 — показываем числовую частоту серым.
        /// Это позволяет Д-классу узнать частоту СБ-шника и передать её другим.
        /// </summary>
        private static string BuildFrequencyHint(Player player, RadioState state)
        {
            // Custom — частота и так отображается числом
            if (state.Team == RadioTeam.Custom)
                return string.Empty;

            // Частоты ≥ 300 скрыты по ТЗ
            if (state.Frequency >= 300f)
                return string.Empty;

            // Если эта волна "родная" для роли — подсказку не показываем
            var ownTeams = RadioModeProvider.GetAvailableTeams(player);
            if (ownTeams.Contains(state.Team))
                return string.Empty;

            return $" <color=#888888>({state.Frequency:0.0})</color>";
        }
    }
}
 