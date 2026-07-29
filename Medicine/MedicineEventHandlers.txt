using System;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.API.Features.DamageHandlers;
using EventHUD.Rpm;
using MEC;
using PlayerStatsSystem;

namespace EventHUD.Medicine
{
    public class MedicineEventHandlers
    {
        private readonly Config _config;

        public MedicineEventHandlers(Config config)
        {
            _config = config;
        }

        public void OnHurting(HurtingEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine))
                return;

            if (ev.Player == null || !ev.Player.IsAlive)
                return;

            // SCP не получают травмы
            if (ev.Player.Role.Team == PlayerRoles.Team.SCPs)
                return;

            float damage = ev.Amount;
            var damageType = ev.DamageHandler.Type;

            if (_config.Debug)
            {
                Exiled.API.Features.Log.Debug(
                    $"[Medicine] OnHurting: Player={ev.Player.Nickname}, " +
                    $"DamageType={damageType}, Amount={damage}");
            }

            HitboxType hitbox = HitboxType.Body;
            if (ev.DamageHandler.Base is StandardDamageHandler sdh)
                hitbox = sdh.Hitbox;

            // Penetration
            float penetration = GetDefaultPenetration(damageType);
            try
            {
                var exiledHandler = ev.DamageHandler as Exiled.API.Features.DamageHandlers.DamageHandlerBase;
                if (exiledHandler != null)
                {
                    var penProp = exiledHandler.GetType().GetProperty("Penetration");
                    if (penProp != null)
                    {
                        var val = penProp.GetValue(exiledHandler);
                        if (val is float f && f > 0f)
                            penetration = f;
                    }
                }
            }
            catch { }

            switch (damageType)
            {
                // ── Огнестрельное ──
                case DamageType.Com15:
                case DamageType.Com18:
                case DamageType.Com45:
                case DamageType.Revolver:
                case DamageType.Fsp9:
                case DamageType.Crossvec:
                case DamageType.Shotgun:
                case DamageType.AK:
                case DamageType.Logicer:
                case DamageType.E11Sr:
                case DamageType.Frmg0:
                case DamageType.A7:
                case DamageType.Firearm:
                {
                    float modifiedDamage = ArmorDamageProcessor.ProcessFirearmDamage(
                        ev.Player, ev.Attacker, damage, damageType, hitbox, penetration,
                        out bool shouldApplyInjury, out bool isLethalHeadshot);

                    ev.Amount = modifiedDamage;

                    if (isLethalHeadshot)
                    {
                        ev.Amount = 0;
                        return;
                    }

                    if (shouldApplyInjury)
                    {
                        var weaponItem = DamageTypeToItemType(damageType);
                        InjuryApplier.ProcessFirearmDamage(ev.Player, modifiedDamage,
                            weaponItem ?? ItemType.GunCOM18, hitbox);
                    }
                    break;
                }

                case DamageType.Explosion:
                    InjuryApplier.ProcessExplosionDamage(ev.Player, damage);
                    break;

                case DamageType.Falldown:
                    InjuryApplier.ProcessFallDamage(ev.Player, damage);
                    InjuryApplier.ProcessDamage(ev.Player, damage, damageType, hitbox);
                    break;

                // ── SCP ──
                case DamageType.Scp939:
                case DamageType.Scp096:
                case DamageType.Scp049:
                case DamageType.Scp0492:
                case DamageType.Scp3114:
                case DamageType.Strangled:
                case DamageType.Scp:
                {
                    float scpDamage = ArmorDamageProcessor.ProcessScpDamage(
                        ev.Player, damage, damageType, out bool shouldApplyInjury);
                    ev.Amount = scpDamage;
                    if (shouldApplyInjury)
                        InjuryApplier.ProcessScpDamage(ev.Player, scpDamage, damageType, hitbox);
                    break;
                }

                case DamageType.Jailbird:
                {
                    float jbDamage = ArmorDamageProcessor.ProcessScpDamage(
                        ev.Player, damage, damageType, out bool shouldApplyInjury);
                    ev.Amount = jbDamage;
                    if (shouldApplyInjury)
                        InjuryApplier.ProcessJailbirdDamage(ev.Player, hitbox);
                    break;
                }

                case DamageType.MicroHid:
                    // Не даём ожог себе при стрельбе из Micro-HID
                    if (ev.Attacker == null || ev.Attacker.Id != ev.Player.Id)
                        InjuryApplier.ProcessMicroHidDamage(ev.Player, hitbox);
                    break;

                case DamageType.PocketDimension:
                    InjuryApplier.ApplyCorrosion(ev.Player);
                    break;
            }
        }

        public void OnUsedItem(UsedItemEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine))
                return;

            if (ev.Player == null)
                return;

            switch (ev.Item.Type)
            {
                case ItemType.Adrenaline:
                    InjuryApplier.ProcessAdrenalineUse(ev.Player);
                    break;
                case ItemType.Painkillers:
                    InjuryApplier.ProcessPainkillerUse(ev.Player);
                    break;
                case ItemType.SCP500:
                    ProcessScp500Use(ev.Player);
                    break;
            }
        }

        public void OnUsingItem(UsingItemEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine))
                return;

            if (ev.Player == null)
                return;

            if (ev.Item.Type == ItemType.Medkit)
                ev.IsAllowed = false;
        }

        [Obsolete]
        public void OnItemAdded(ItemAddedEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine))
                return;

            if (ev.Player == null || ev.Item == null)
                return;

            // Медкит
            if (ev.Item.Type == ItemType.Medkit)
            {
                if (!MedkitInventoryStorage.TryGet(ev.Item.Serial, out _))
                {
                    // Задержка — при спавне роль может быть ещё не установлена
                    ushort serial = ev.Item.Serial;
                    Player playerRef = ev.Player;
                    MEC.Timing.CallDelayed(0.5f, () =>
                    {
                        try
                        {
                            if (playerRef == null || !playerRef.IsAlive) return;
                            if (MedkitInventoryStorage.TryGet(serial, out _)) return;
                            var type = MedkitTypeAssigner.GetByRole(playerRef);
                            MedkitInventoryStorage.Set(serial, MedkitInventory.Create(type));
                        }
                        catch { }
                    });
                }
                return;
            }

            // Бронежилет
            if (ev.Item.Type == ItemType.ArmorLight ||
                ev.Item.Type == ItemType.ArmorCombat ||
                ev.Item.Type == ItemType.ArmorHeavy)
            {
                ArmorState armorState =
                    ArmorStorage.GetOrCreate(ev.Player.UserId);

                if (armorState.Type == ArmorType.None)
                {
                    armorState.SetType(
                        AssignArmorType(ev.Player, ev.Item.Type));
                }

                if (ArmorItemDurabilityStorage.TryGet(
                        ev.Item.Serial,
                        out float savedDurability))
                {
                    armorState.Durability = savedDurability;
                }

                if (ArmorItemDurabilityStorage.TryGetHelmetDurability(
                        ev.Item.Serial,
                        out int savedHelmetDurability))
                {
                    armorState.HelmetDurability = savedHelmetDurability;
                }

                if (armorState.Type != ArmorType.Helmet &&
                    armorState.Durability > 0f)
                {
                    ApplyArmorShield(ev.Player, armorState.Type);
                }
            }
        }

        /// <summary>
        /// Устанавливает синий броне-щит (Hume Shield) по типу брони.
        /// Не восстанавливается (у людей нет HS-регена), но принимает урон.
        /// Применяется с задержкой, чтобы пережить сброс стата при спавне.
        /// </summary>
        [Obsolete]

        public static void ApplyArmorShield(Player player, ArmorType type)
        {
            float shield = type.GetArmorShield();
            if (shield <= 0f) return;

            MEC.Timing.CallDelayed(0.5f, () =>
            {
                if (player == null || !player.IsAlive) return;
                try
                {
                    player.HumeShieldStat.MaxValue = shield;
                    player.HumeShield = shield;
                }
                catch { }
            });
        }

        public void OnDied(DiedEventArgs ev)
        {
            if (ev.Player == null)
                return;

            // Anti-абьюз: при смерти от Pit Death снимаем 50% патронов и удаляем мячики
            if (ev.DamageHandler.Type == DamageType.Falldown ||
                ev.DamageHandler.Type.ToString().ToLowerInvariant().Contains("pit"))
            {
                RemoveHalfAmmo(ev.Player);
                RemoveCandies(ev.Player);
            }

            MedicalStorage.GetOrCreate(ev.Player.UserId).Reset();
            Plugin.Instance.InjuryTicks?.ResetPlayer(ev.Player.UserId);
            ArmorStorage.Remove(ev.Player.UserId);
            Plugin.Instance.CritState?.Cancel(ev.Player.UserId);
        }

        private void RemoveHalfAmmo(Player player)
        {
            try
            {
                var ammoItems = player.Items
                    .Where(i => i != null && IsAmmoType(i.Type))
                    .ToList();

                int toRemove = ammoItems.Count / 2;
                for (int i = 0; i < toRemove; i++)
                {
                    ammoItems[i].Destroy();
                }
            }
            catch { }
        }

        private void RemoveCandies(Player player)
        {
            try
            {
                foreach (var item in player.Items.ToList())
                {
                    if (item != null && item.Type == ItemType.SCP330)
                        item.Destroy();
                }
            }
            catch { }
        }

        private bool IsAmmoType(ItemType type)
        {
            return type == ItemType.Ammo556x45 ||
                   type == ItemType.Ammo762x39 ||
                   type == ItemType.Ammo9x19 ||
                   type == ItemType.Ammo12gauge ||
                   type == ItemType.Ammo44cal;
        }

        public void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (ev.Player == null)
                return;

            if (ev.NewRole != PlayerRoles.RoleTypeId.Spectator &&
                ev.NewRole != PlayerRoles.RoleTypeId.None)
            {
                MedicalStorage.GetOrCreate(ev.Player.UserId).Reset();
                Plugin.Instance.InjuryTicks?.ResetPlayer(ev.Player.UserId);
                ArmorStorage.Remove(ev.Player.UserId);
                Plugin.Instance.CritState?.Cancel(ev.Player.UserId);
            }
        }

        public void OnReceivingEffect(ReceivingEffectEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine))
                return;

            if (ev.Player == null)
                return;

            if (ev.Effect is CustomPlayerEffects.CardiacArrest && ev.Intensity > 0)
                MedicalStorage.GetOrCreate(ev.Player.UserId).AddCondition(GlobalCondition.CardiacArrest);
        }

        public void OnDroppingItem(DroppingItemEventArgs ev)
        {
            if (ev.Player == null || ev.Item == null) return;

            if (ev.Item.Type != ItemType.ArmorLight &&
                ev.Item.Type != ItemType.ArmorCombat &&
                ev.Item.Type != ItemType.ArmorHeavy)
                return;

            if (!ArmorStorage.TryGet(
                    ev.Player.UserId,
                    out ArmorState armorState) ||
                armorState.Type == ArmorType.None)
            {
                return;
            }

            ArmorItemDurabilityStorage.Set(
                ev.Item.Serial,
                armorState.Durability,
                armorState.HelmetDurability);

            armorState.Reset();
        }

        public void OnEnteringPocketDimension(EnteringPocketDimensionEventArgs ev)
        {
            if (!RpModuleManager.Instance.IsEnabled(RpModuleType.Medicine))
                return;

            if (ev.Player == null)
                return;

            InjuryApplier.ApplyCorrosion(ev.Player);
        }

        // ═══════════════════════════════════════

        private void ProcessScp500Use(Player player)
        {
            var state = MedicalStorage.GetOrCreate(player.UserId);
            var toRemove = new System.Collections.Generic.List<GlobalCondition>();
            foreach (var cond in state.Conditions)
            {
                if (cond != GlobalCondition.Normal && cond != GlobalCondition.LethalHeadshot)
                    toRemove.Add(cond);
            }
            foreach (var cond in toRemove)
                state.RemoveCondition(cond);

            state.Injuries.RemoveAll(i => i.Type != LocalInjuryType.Corrosion);
            Plugin.Instance.InjuryTicks?.ResetPlayer(player.UserId);
        }

        private static ArmorType AssignArmorType(
            Player player,
            ItemType itemType)
        {
            if (itemType == ItemType.ArmorHeavy &&
                player.Role.Type == PlayerRoles.RoleTypeId.NtfCaptain)
            {
                return ArmorType.Tank;
            }

            return itemType switch
            {
                ItemType.ArmorLight => ArmorType.Helmet,
                ItemType.ArmorCombat => ArmorType.Combat,
                ItemType.ArmorHeavy => ArmorType.Heavy,
                _ => ArmorType.None
            };
        }

        private static float GetDefaultPenetration(DamageType dt) => dt switch
        {
            DamageType.Com15    => 0.20f,
            DamageType.Com18    => 0.55f,
            DamageType.Com45    => 0.55f,
            DamageType.Fsp9     => 0.45f,
            DamageType.Crossvec => 0.40f,
            DamageType.E11Sr    => 0.70f,
            DamageType.AK       => 0.80f,
            DamageType.Logicer  => 0.75f,
            DamageType.Frmg0    => 0.85f,
            DamageType.A7       => 0.65f,
            DamageType.Shotgun  => 0.60f,
            DamageType.Revolver => 0.65f,
            _                   => 0.50f
        };

        private static ItemType? DamageTypeToItemType(DamageType dt) => dt switch
        {
            DamageType.Com15    => ItemType.GunCOM15,
            DamageType.Com18    => ItemType.GunCOM18,
            DamageType.Com45    => ItemType.GunCom45,
            DamageType.Revolver => ItemType.GunRevolver,
            DamageType.Fsp9     => ItemType.GunFSP9,
            DamageType.Crossvec => ItemType.GunCrossvec,
            DamageType.Shotgun  => ItemType.GunShotgun,
            DamageType.AK       => ItemType.GunAK,
            DamageType.Logicer  => ItemType.GunLogicer,
            DamageType.E11Sr    => ItemType.GunE11SR,
            DamageType.Frmg0    => ItemType.GunFRMG0,
            DamageType.A7       => ItemType.GunA7,
            _                   => null
        };
    }
}
 