using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;

namespace EventHUD.Radio
{
    /// <summary>
    /// Возвращает список базовых командных волн для роли игрока.
    /// ВАЖНО: проверки конкретных ролей РАНЬШЕ проверок команд,
    /// потому что FacilityGuard входит в Team.FoundationForces.
    /// </summary>
    public static class RadioModeProvider
    {
        public static List<RadioTeam> GetAvailableTeams(Player player)
        {
            // СЦП — рации нет
            if (player.Role.Team == Team.SCPs)
                return new List<RadioTeam>();

            // ── Конкретные роли ПЕРВЫМИ (до проверки команды) ──

            // СБ — Комплекс + СБ
            if (player.Role.Type == RoleTypeId.FacilityGuard)
                return new List<RadioTeam> { RadioTeam.Facility, RadioTeam.Security };

            // Учёный — только Комплекс
            if (player.Role.Type == RoleTypeId.Scientist)
                return new List<RadioTeam> { RadioTeam.Facility };

            // ── Команды ──

            // ПХ — только волна ПХ
            if (player.Role.Team == Team.ChaosInsurgency)
                return new List<RadioTeam> { RadioTeam.Chaos };

            // МОГ (NTF, капитан и т.д.) — только волна МОГ
            if (player.Role.Team == Team.FoundationForces)
                return new List<RadioTeam> { RadioTeam.Mtf };

            // Д-класс и всё остальное — только "?"
            return new List<RadioTeam> { RadioTeam.Unknown };
        }
    }
}
 