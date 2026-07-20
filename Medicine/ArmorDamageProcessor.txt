using System;
using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using EventHUD.Enums;
using EventHUD.Extensions;
using MEC;
using PlayerStatsSystem;
using UnityEngine;

namespace EventHUD.Medicine
{
    /// <summary>
    /// Система бронежилетов v2: прочность + поглощение + деградация + стаки + хедшот.
    /// </summary>
    public static class ArmorDamageProcessor
    {
        private static readonly HashSet<DamageType> HelmetBlockable =
            new HashSet<DamageType>
            {
                DamageType.Com15,
                DamageType.Com18,
                DamageType.Com45,
                DamageType.Fsp9,
                DamageType.Crossvec
            };

        /// <summary>
        /// Обработка огнестрельного урона через броню.
        /// Возвращает финальный урон по HP.
        /// </summary>
        public static float ProcessFirearmDamage(
            Player victim, Player attacker, float baseDamage,
            DamageType damageType, HitboxType hitbox, float _penetration,
            out bool shouldApplyInjury, out bool isLethalHeadshot)
        {
            shouldApplyInjury = true;
            isLethalHeadshot = false;

            ArmorState armor =
                ArmorStorage.GetOrCreate(victim.UserId);

            if (hitbox == HitboxType.Headshot)
            {
                return ProcessHeadshot(
                    victim,
                    attacker,
                    baseDamage,
                    damageType,
                    armor,
                    out isLethalHeadshot,
                    out shouldApplyInjury);
            }

            // Отдельный шлем не защищает корпус и конечности.
            if (armor.Type == ArmorType.Helmet)
                return baseDamage;

            if (armor.Type == ArmorType.None || armor.IsBroken)
                return baseDamage;

            // ── Конечности ──
            if (hitbox == HitboxType.Limb)
            {
                float limbMultiplier =
                    armor.Type.GetLimbMultiplier();

                if (limbMultiplier <= 0f)
                    return baseDamage;

                return ProcessBodyDamage(
                    victim,
                    attacker,
                    baseDamage,
                    damageType,
                    armor,
                    limbMultiplier,
                    out shouldApplyInjury);
            }

            // ── Корпус ──
            return ProcessBodyDamage(
                victim,
                attacker,
                baseDamage,
                damageType,
                armor,
                1f,
                out shouldApplyInjury);
        }

        /// <summary>
        /// Калибр оружия = сколько прочности брони снимает один выстрел.
        /// Пистолет (10) — база под спецификацию 3/9/15/21 пуль.
        /// </summary>
        private static float GetCaliber(DamageType dt) => dt switch
        {
            DamageType.Com15    => 8f,   // слабый пистолет
            DamageType.Com18    => 10f,  // база (9мм)
            DamageType.Com45    => 13f,  // .45 ACP
            DamageType.Fsp9     => 10f,  // 9мм ПП
            DamageType.Crossvec => 12f,  // ПП высокой скорости
            DamageType.Revolver => 20f,  // .44 magnum
            DamageType.Shotgun  => 16f,  // картечь в упор
            DamageType.AK       => 22f,  // 7.62×39
            DamageType.E11Sr    => 18f,  // 5.56 высокоскоростной
            DamageType.Logicer  => 24f,  // 7.62 пулемёт
            DamageType.Frmg0    => 28f,  // крупный калибр
            DamageType.A7       => 16f,
            DamageType.Firearm  => 12f,
            _                   => 12f
        };

        /// <summary>
        /// Обработка урона от SCP.
        /// </summary>
        public static float ProcessScpDamage(Player victim, float baseDamage, DamageType scpType, out bool shouldApplyInjury)
        {
            shouldApplyInjury = true;
            var armor = ArmorStorage.GetOrCreate(victim.UserId);

            if (armor.Type == ArmorType.Helmet)
                return baseDamage;

            if (armor.Type == ArmorType.None || armor.IsBroken)
                return baseDamage;

            float scpDmg = GetScpArmorDamage(armor.Type, scpType);
            float armorDmg = baseDamage * (armor.EffectiveAbsorption / 100f);
            armor.Durability -= armorDmg;

            if (armor.Durability <= 0)
                armor.Durability = 0;

            if (scpDmg < baseDamage * 0.5f)
            {
                shouldApplyInjury = false;
                armor.AddStun();
                ApplyStunEffects(victim, armor);
            }

            return Math.Min(scpDmg, baseDamage);
        }

        // ═══════════════════════════════════════
        // Корпус
        // ═══════════════════════════════════════

        private static float ProcessBodyDamage(
            Player victim, Player attacker, float baseDamage, DamageType damageType,
            ArmorState armor, float zoneMultiplier, out bool shouldApplyInjury)
        {
            shouldApplyInjury = true;

            var rpType = EventManager.Instance.Session?.RpType ?? RPType.NONRP;

            // Угол попадания влияет на пробитие
            float angleMult = (attacker != null) ? GetAngleMultiplier(victim, attacker) : 1f;

            // Прочность-урон = калибр оружия (не зависит от угла/РП → фиксированное число пуль)
            float caliber = GetCaliber(damageType);
            float durabilityDamage = caliber;

            if (armor.Durability > 0f)
            {
                // Броня цела — снижает урон по HP
                float reduction = armor.Type.GetIntactDamageReduction() + rpType.GetArmorReductionBonus();
                if (reduction > 0.95f) reduction = 0.95f;

                // Выстрел в бок/спину пробивает защиту сильнее (меньше прикрыто)
                reduction *= angleMult;

                // Конечности (танк, zoneMult=0.5) защищены хуже
                if (zoneMultiplier > 0f && zoneMultiplier < 1f)
                    reduction *= zoneMultiplier;

                float hpDamage = baseDamage * (1f - reduction);

                armor.Durability -= durabilityDamage;

                if (armor.Durability <= 0f)
                {
                    // Пробитие этим выстрелом — сквозное ранение
                    armor.Durability = 0f;
                    shouldApplyInjury = true;
                    hpDamage = Math.Max(hpDamage, baseDamage * 0.6f);
                }
                else
                {
                    // Броня держит — тупая травма/контузия, ранения нет
                    shouldApplyInjury = false;
                    armor.AddStun();
                    ApplyStunEffects(victim, armor);
                }

                return Math.Max(hpDamage, 1f);
            }

            // Броня уже пробита — полный урон + ранение
            shouldApplyInjury = true;
            return baseDamage;
        }

        // ═══════════════════════════════════════
        // Хедшот
        // ═══════════════════════════════════════

        private static float ProcessHeadshot(
            Player victim,
            Player attacker,
            float headshotDamage,
            DamageType damageType,
            ArmorState armor,
            out bool isLethal,
            out bool shouldApplyInjury)
        {
            isLethal = false;
            shouldApplyInjury = true;

            RPType rpType =
                EventManager.Instance.Session?.RpType ?? RPType.NONRP;

            bool helmetProtects;

            switch (armor.Type)
            {
                case ArmorType.Helmet:
                    helmetProtects =
                        armor.HelmetDurability > 0 &&
                        HelmetBlockable.Contains(damageType);
                    break;

                case ArmorType.Heavy:
                case ArmorType.Tank:
                    helmetProtects = true;
                    break;

                case ArmorType.Combat:
                    helmetProtects =
                        rpType.CombatSurvivesHeadshot();
                    break;

                default:
                    helmetProtects = false;
                    break;
            }

            if (!helmetProtects)
            {
                isLethal = true;
                shouldApplyInjury = false;

                InstantHeadshotKill(victim);
                return 0f;
            }

            // Отдельный лёгкий шлем принимает ровно одну разрешённую пулю.
            if (armor.Type == ArmorType.Helmet)
            {
                armor.HelmetDurability = 0;

                ApplyHelmetSurvivalEffects(victim);

                PlayerMedicalState medicalState =
                    MedicalStorage.GetOrCreate(victim.UserId);

                medicalState.AddInjury(
                    LocalInjuryType.Gunshot,
                    BodyPart.Head);

                shouldApplyInjury = true;

                return Math.Max(headshotDamage * 0.15f, 2f);
            }

            float threshold =
                armor.Type.GetHelmetThreshold();

            if (headshotDamage < threshold)
            {
                ApplyHelmetSurvivalEffects(victim);

                PlayerMedicalState medicalState =
                    MedicalStorage.GetOrCreate(victim.UserId);

                medicalState.EscalateBleeding(
                    GlobalCondition.BleedingMedium);

                medicalState.AddInjury(
                    LocalInjuryType.Gunshot,
                    BodyPart.Head);

                shouldApplyInjury = true;

                TryDropWeapon(victim);

                return Math.Max(headshotDamage * 0.15f, 2f);
            }

            isLethal = true;
            shouldApplyInjury = false;

            ApplyLethalHeadshotEffects(victim);
            return 0f;
        }

        /// <summary>
        /// Моментальная смерть от хедшота без каски.
        /// -140 HP, адреналин (AHP) и синий щит не учитываются.
        /// </summary>
        private static void InstantHeadshotKill(Player victim)
        {
            try
            {
                victim.ArtificialHealth = 0f;      // адреналин не считается
                try { victim.HumeShield = 0f; } catch { }
                victim.Kill("Смертельное ранение в голову");
            }
            catch
            {
                try { victim.Kill(DamageType.Firearm); } catch { }
            }
        }

        // ═══════════════════════════════════════
        // Стаки контузии корпуса
        // ═══════════════════════════════════════

        private static void ApplyStunEffects(Player player, ArmorState armor)
        {
            int stacks = armor.ActiveStacks;

            switch (stacks)
            {
                case 1:
                    // Толчок 0.1с — минимальный эффект
                    break;
                case 2:
                    player.EnableEffect<Blurred>(0.3f);
                    break;
                case 3:
                    player.EnableEffect<Blurred>(0.5f);
                    player.EnableEffect<Slowness>(5f);
                    player.ChangeEffectIntensity<Slowness>(15);
                    break;
                case 4:
                    // Ушиб лёгких
                    player.EnableEffect<Slowness>(10f);
                    player.ChangeEffectIntensity<Slowness>(30);
                    player.EnableEffect<Exhausted>(10f);
                    armor.HasLungBruise = true;
                    break;
                default:
                    if (stacks >= 5)
                    {
                        // Асфиксия
                        player.EnableEffect<Slowness>(15f);
                        player.ChangeEffectIntensity<Slowness>(40);
                        player.EnableEffect<Exhausted>(15f);
                        armor.HasAsphyxia = true;
                    }
                    break;
            }
        }

        // ═══════════════════════════════════════
        // Хедшот эффекты
        // ═══════════════════════════════════════

        private static void ApplyLethalHeadshotEffects(Player victim)
        {
            victim.EnableEffect<Blurred>(1f);
            victim.EnableEffect<Blindness>(1f);
            victim.ChangeEffectIntensity<Blindness>(45);
            victim.EnableEffect<Deafened>(1f);

            var state = MedicalStorage.GetOrCreate(victim.UserId);
            state.AddCondition(GlobalCondition.LethalHeadshot);

            // Смерть через 1 секунду (было 3)
            Timing.RunCoroutine(KillAfterDelay(victim, 1f));
        }

        private static void ApplyHelmetSurvivalEffects(Player victim)
        {
            victim.EnableEffect<Blurred>(1.5f);
            victim.EnableEffect<Deafened>(1.5f);
        }

        private static IEnumerator<float> KillAfterDelay(Player player, float delay)
        {
            yield return Timing.WaitForSeconds(delay);

            if (player != null && player.IsAlive)
            {
                var state = MedicalStorage.GetOrCreate(player.UserId);
                if (state.HasCondition(GlobalCondition.LethalHeadshot))
                    player.Kill("Смертельное ранение в голову");
            }
        }

        private static void TryDropWeapon(Player victim)
        {
            var rpType = EventManager.Instance.Session?.RpType ?? RPType.NONRP;

            float chance = rpType switch
            {
                RPType.HARDRP or RPType.FULLRP => 0.40f,
                RPType.MEDIUMRP                => 0.30f,
                RPType.LIGHTRP or RPType.FUNRP  => 0.20f,
                _                              => 0f
            };

            if (chance > 0f && UnityEngine.Random.value < chance && victim.CurrentItem != null)
                victim.DropItem(victim.CurrentItem);
        }

        // ═══════════════════════════════════════
        // Угол
        // ═══════════════════════════════════════

        private static float GetAngleMultiplier(Player victim, Player attacker)
        {
            Vector3 toAttacker = (attacker.Position - victim.Position).normalized;
            Vector3 victimForward = victim.ReferenceHub.PlayerCameraReference.forward;
            float angle = Vector3.Angle(victimForward, toAttacker);

            if (angle < 60f)  return 1.0f;
            if (angle < 120f) return 0.7f;
            return 0.4f;
        }

        // ═══════════════════════════════════════
        // SCP урон
        // ═══════════════════════════════════════

        private static float GetScpArmorDamage(ArmorType armor, DamageType scpType)
        {
            return (armor, scpType) switch
            {
                (_, DamageType.Scp0492) => 1f,
                (ArmorType.Tank, DamageType.Scp939) => 2f,
                (_, DamageType.Scp939) => 3f,
                (ArmorType.Light, DamageType.Scp3114) => 5f,
                (ArmorType.Combat, DamageType.Scp3114) => 3f,
                (ArmorType.Heavy, DamageType.Scp3114) => 2f,
                (ArmorType.Tank, DamageType.Scp3114) => 1f,
                (ArmorType.Light, DamageType.Jailbird) => 10f,
                (ArmorType.Combat, DamageType.Jailbird) => 5f,
                (ArmorType.Heavy, DamageType.Jailbird) => 3f,
                (ArmorType.Tank, DamageType.Jailbird) => 2f,
                _ => 999f
            };
        }
    }
}
 