using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Определяет тип аптечки при подборе/спавне.
    /// 
    /// Правила:
    ///   МОГ, Мародёр ПХ, Репрессор ПХ → Военная (IFAK)
    ///   NtfSpecialist (парамедик)     → Парамедика
    ///   СБ (FacilityGuard)            → Рабочая
    ///   НС (Scientist)               → Гражданская
    ///   Остальные FoundationForces    → Рабочая (младшие МОГ)
    ///   Остальные ChaosInsurgency     → Рабочая (младшие ПХ)
    ///   
    ///   По карте (если рация не назначена / нет владельца):
    ///     Light Containment → Гражданская
    ///     Heavy Containment → Рабочая
    ///     Entrance Zone     → Гражданская
    ///     Surface           → Рабочая
    /// </summary>
    public static class MedkitTypeAssigner
    {
        /// <summary>
        /// Определяет тип аптечки по роли игрока который подобрал.
        /// </summary>
        public static MedkitType GetByRole(Player player)
        {
            if (player == null)
                return MedkitType.Civilian;

            var role = player.Role.Type;

            // Парамедик = NtfSpecialist
            if (role == RoleTypeId.NtfSpecialist)
                return MedkitType.Paramedic;

            // МОГ (кроме Specialist) — военная
            if (role == RoleTypeId.NtfCaptain ||
                role == RoleTypeId.NtfSergeant ||
                role == RoleTypeId.NtfPrivate)
                return MedkitType.Military;

            // ПХ боевые роли — военная
            if (role == RoleTypeId.ChaosMarauder ||
                role == RoleTypeId.ChaosRepressor)
                return MedkitType.Military;

            // ПХ остальные — рабочая
            if (player.Role.Team == Team.ChaosInsurgency)
                return MedkitType.Industrial;

            // СБ — рабочая
            if (role == RoleTypeId.FacilityGuard)
                return MedkitType.Industrial;

            // НС — гражданская
            if (role == RoleTypeId.Scientist)
                return MedkitType.Civilian;

            // Остальные FoundationForces (если есть) — рабочая
            if (player.Role.Team == Team.FoundationForces)
                return MedkitType.Industrial;

            // Д-класс и всё остальное — гражданская
            return MedkitType.Civilian;
        }

        /// <summary>
        /// Определяет тип аптечки по зоне на карте (для валяющихся аптечек).
        /// </summary>
        public static MedkitType GetByZone(ZoneType zone)
        {
            return zone switch
            {
                ZoneType.LightContainment => MedkitType.Civilian,
                ZoneType.HeavyContainment => MedkitType.Industrial,
                ZoneType.Entrance         => MedkitType.Civilian,
                ZoneType.Surface          => MedkitType.Industrial,
                _                         => MedkitType.Civilian
            };
        }
    }
}
 