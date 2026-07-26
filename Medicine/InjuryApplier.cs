using System;
using Exiled.API.Features;
using Exiled.API.Enums;
using CustomPlayerEffects;
using EventHUD.Enums;
using EventHUD.Extensions;
using PlayerRoles;
using PlayerStatsSystem;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Определяет какие травмы и состояния назначить при получении урона.
    /// </summary>
    public static class InjuryApplier
    {
        /// <summary>
        /// Вызывается из MedicineEventHandlers.OnHurting для обработки общего урона (падение).
        /// </summary>
        public static void ProcessDamage(Player player, float damage, DamageType damageType, HitboxType hitbox)
        {
            var state = MedicalStorage.GetOrCreate(player.UserId);
            var config = Plugin.Instance.Config;

            // Перелом при падении
            if (damageType == DamageType.Falldown && damage >= config.LightBleedFallMaxDamage)
            {
                var leg = UnityEngine.Random.value < 0.5f ? BodyPart.LeftLeg : BodyPart.RightLeg;
                state.AddInjury(LocalInjuryType.Fracture, leg);

                player.EnableEffect<Slowness>(config.FractureConcussedDuration);
                player.ChangeEffectIntensity<Slowness>(config.FractureSlownessIntensity);
                player.EnableEffect<Concussed>(config.FractureConcussedDuration);
            }
        }

        /// <summary>
        /// Обработка огнестрельного урона.
        /// </summary>
        public static void ProcessFirearmDamage(Player player, float damage, ItemType weapon, HitboxType hitbox)
        {
            var state = MedicalStorage.GetOrCreate(player.UserId);
            var config = Plugin.Instance.Config;
            var bodyPart = BodyPartExtensions.FromHitbox(hitbox);

            state.AddInjury(LocalInjuryType.Gunshot, bodyPart);

            // На лёгких РП ранения менее серьёзные (кровотечение на ступень слабее)
            var rpType = EventManager.Instance.Session?.RpType ?? RPType.NONRP;
            bool lightRp = rpType.IsLightRp();

            if (IsHeavyBleedFirearm(weapon, bodyPart))
            {
                if (lightRp) ApplyMediumBleeding(state, config, player);
                else ApplyHeavyBleeding(state, config, player);
            }
            else
            {
                if (lightRp) ApplyLightBleeding(state, config, player);
                else ApplyMediumBleeding(state, config, player);
            }
        }

        /// <summary>
        /// Обработка взрыва гранаты.
        /// </summary>
        public static void ProcessExplosionDamage(Player player, float damage)
        {
            var state = MedicalStorage.GetOrCreate(player.UserId);
            var config = Plugin.Instance.Config;

            if (damage < config.LightBleedGrenadeMaxDamage)
                ApplyLightBleeding(state, config, player);
            else if (damage >= config.MediumBleedGrenadeMinDamage && damage < config.MediumBleedGrenadeMaxDamage)
                ApplyMediumBleeding(state, config, player);
            else if (damage >= config.MediumBleedGrenadeMaxDamage)
                ApplyHeavyBleeding(state, config, player);
        }

        /// <summary>
        /// Обработка урона от падения.
        /// </summary>
        public static void ProcessFallDamage(Player player, float damage)
        {
            var state = MedicalStorage.GetOrCreate(player.UserId);
            var config = Plugin.Instance.Config;

            if (damage > 0 && damage < config.LightBleedFallMaxDamage)
                ApplyLightBleeding(state, config, player);
        }

        /// <summary>
        /// Обработка урона от SCP.
        /// </summary>
        public static void ProcessScpDamage(Player player, float damage, DamageType damageType, HitboxType hitbox)
        {
            var state = MedicalStorage.GetOrCreate(player.UserId);
            var config = Plugin.Instance.Config;
            var bodyPart = BodyPartExtensions.FromHitbox(hitbox);

            switch (damageType)
            {
                case DamageType.Scp939:
                    state.AddInjury(LocalInjuryType.Stab, bodyPart);
                    if (bodyPart == BodyPart.Neck || bodyPart == BodyPart.Abdomen)
                        ApplyHeavyBleeding(state, config, player);
                    else
                        ApplyMediumBleeding(state, config, player);
                    break;

                case DamageType.Scp096:
                    state.AddInjury(LocalInjuryType.Stab, bodyPart);
                    ApplyHeavyBleeding(state, config, player);
                    break;

                case DamageType.Scp0492:
                    state.AddInjury(LocalInjuryType.Bruise, bodyPart);
                    break;

                case DamageType.Scp049:
                    state.AddInjury(LocalInjuryType.Bruise, bodyPart);
                    break;

                case DamageType.Scp3114:
                    state.AddInjury(LocalInjuryType.Stab, bodyPart);
                    ApplyMediumBleeding(state, config, player);
                    break;

                case DamageType.Strangled:
                    state.AddInjury(LocalInjuryType.Bruise, BodyPart.Neck);
                    break;
            }
        }

        /// <summary>
        /// Обработка удара Jailbird.
        /// </summary>
        public static void ProcessJailbirdDamage(Player player, HitboxType hitbox)
        {
            var state = MedicalStorage.GetOrCreate(player.UserId);
            var bodyPart = BodyPartExtensions.FromHitbox(hitbox);
            state.AddInjury(LocalInjuryType.Bruise, bodyPart);
        }

        /// <summary>
        /// Обработка урона от Micro-HID.
        /// </summary>
        public static void ProcessMicroHidDamage(Player player, HitboxType hitbox)
        {
            var state = MedicalStorage.GetOrCreate(player.UserId);
            var config = Plugin.Instance.Config;
            var bodyPart = BodyPartExtensions.FromHitbox(hitbox);

            state.AddInjury(LocalInjuryType.Burn, bodyPart);
            player.EnableEffect<Burned>(config.BurnDuration);
        }

        /// <summary>
        /// Обработка использования адреналина.
        /// </summary>
        public static void ProcessAdrenalineUse(Player player)
        {
            var state = MedicalStorage.GetOrCreate(player.UserId);
            var config = Plugin.Instance.Config;

            state.AdrenalineUsed++;

            // Побочка от иглы — капиллярное кровотечение
            ApplyLightBleeding(state, config, player);

            if (state.AdrenalineUsed >= config.AdrenalineOverdoseThreshold)
            {
                state.AddCondition(GlobalCondition.AdrenalineOverdose);
                state.RemoveCondition(GlobalCondition.Adrenaline);

                player.EnableEffect<Blurred>(config.OverdoseEffectDuration);
                player.EnableEffect<Slowness>(config.OverdoseEffectDuration);
                player.ChangeEffectIntensity<Slowness>(config.OverdoseSlownessIntensity);
                player.EnableEffect<Asphyxiated>(config.OverdoseEffectDuration);
                player.ChangeEffectIntensity<Asphyxiated>(config.OverdoseAsphyxiatedIntensity);
            }
            else
            {
                state.AddCondition(GlobalCondition.Adrenaline);
            }
        }

        /// <summary>
        /// Обработка использования болеутоляющего.
        /// </summary>
        public static void ProcessPainkillerUse(Player player)
        {
            var state = MedicalStorage.GetOrCreate(player.UserId);
            var config = Plugin.Instance.Config;

            state.PainkillerUsed++;

            if (state.PainkillerUsed >= config.PainkillerOverdoseThreshold)
            {
                state.AddCondition(GlobalCondition.PainkillerOverdose);
                state.RemoveCondition(GlobalCondition.Painkiller);

                player.EnableEffect<Blurred>(config.OverdoseEffectDuration);
                player.EnableEffect<Slowness>(config.OverdoseEffectDuration);
                player.ChangeEffectIntensity<Slowness>(config.OverdoseSlownessIntensity);
                player.EnableEffect<Asphyxiated>(config.OverdoseEffectDuration);
                player.ChangeEffectIntensity<Asphyxiated>(config.OverdoseAsphyxiatedIntensity);
            }
            else
            {
                state.AddCondition(GlobalCondition.Painkiller);
            }
        }

        /// <summary>
        /// Коррозия SCP-106 (мгновенная).
        /// </summary>
        public static void ApplyCorrosion(Player player)
        {
            var state = MedicalStorage.GetOrCreate(player.UserId);
            var config = Plugin.Instance.Config;

            var part = BodyPartExtensions.FromHitbox(HitboxType.Body);
            state.AddInjury(LocalInjuryType.Corrosion, part);

            player.EnableEffect<Stained>(config.CorrosionStainedDuration);
            player.EnableEffect<Blindness>(config.CorrosionStainedDuration);
            player.ChangeEffectIntensity<Blindness>(config.CorrosionBlindnessIntensity);
            player.EnableEffect<Concussed>(config.CorrosionConcussedDuration);
        }

        /// <summary>
        /// Химическая травма от SCP-244.
        /// </summary>
        public static void ApplyChemical(Player player, BodyPart part)
        {
            var state = MedicalStorage.GetOrCreate(player.UserId);
            var config = Plugin.Instance.Config;

            state.AddInjury(LocalInjuryType.Chemical, part);
            player.EnableEffect<Concussed>(config.ChemicalConcussedDuration);
        }

        // ═══════════════════════════════════════
        // Приватные
        // ═══════════════════════════════════════

        private static void ApplyLightBleeding(PlayerMedicalState state, Config config, Player player)
        {
            state.EscalateBleeding(GlobalCondition.BleedingLight);
            player.EnableEffect<Concussed>(config.LightBleedConcussedDuration);
        }

        private static void ApplyMediumBleeding(PlayerMedicalState state, Config config, Player player)
        {
            state.EscalateBleeding(GlobalCondition.BleedingMedium);
            player.EnableEffect<Concussed>(config.MediumBleedConcussedDuration);
            player.EnableEffect<Deafened>(config.MediumBleedDeafenedDuration);
            player.EnableEffect<Blindness>(config.MediumBleedBlindnessDuration);
            player.ChangeEffectIntensity<Blindness>(config.MediumBleedBlindnessIntensity);
        }

        private static void ApplyHeavyBleeding(PlayerMedicalState state, Config config, Player player)
        {
            state.EscalateBleeding(GlobalCondition.BleedingHeavy);
            player.EnableEffect<Concussed>(config.HeavyBleedConcussedDuration);
            player.EnableEffect<Deafened>(config.HeavyBleedDeafenedDuration);
            player.EnableEffect<Blindness>(config.HeavyBleedBlindnessDuration);
            player.ChangeEffectIntensity<Blindness>(config.HeavyBleedBlindnessIntensity);
        }

        private static bool IsHeavyBleedFirearm(ItemType weapon, BodyPart bodyPart)
        {
            if (bodyPart == BodyPart.Neck)
                return true;

            switch (weapon)
            {
                case ItemType.GunAK:
                case ItemType.GunLogicer:
                case ItemType.GunFRMG0:
                case ItemType.GunA7:
                    return bodyPart.IsVital();

                case ItemType.GunShotgun:
                    return bodyPart == BodyPart.Chest ||
                           bodyPart == BodyPart.Abdomen ||
                           bodyPart == BodyPart.Neck;

                case ItemType.GunE11SR:
                    return bodyPart == BodyPart.Neck || bodyPart == BodyPart.Head;

                case ItemType.GunRevolver:
                    return bodyPart == BodyPart.Head ||
                           bodyPart == BodyPart.Chest ||
                           bodyPart == BodyPart.Neck;

                case ItemType.GunCrossvec:
                    return bodyPart == BodyPart.Neck;

                default:
                    return false;
            }
        }
    }
}
 