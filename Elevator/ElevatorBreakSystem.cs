using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EventHUD.Audio;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.Events.EventArgs.Map;
using Interactables.Interobjects.DoorUtils;
using MEC;
using PlayerRoles;
using UnityEngine;

namespace EventHUD.Elevator
{
    public sealed class ElevatorBreakSystem
    {
        private sealed class BrokenLift
        {
            public Lift Lift;
            public object BrokenSchematic;
            public Transform LiftRoot;
            public Vector3 SchematicLocalPosition;
            public Quaternion SchematicLocalRotation;
            public CoroutineHandle UnlockHandle;
            public CoroutineHandle FollowHandle;
            public object SoundHandle;
        }

        private readonly Config config;

        private readonly Dictionary<Lift, BrokenLift> brokenLifts =
            new Dictionary<Lift, BrokenLift>();

        private readonly Dictionary<Lift, string> passengerSignatures =
            new Dictionary<Lift, string>();

        private readonly Dictionary<Lift, int> grenadeExplosions =
            new Dictionary<Lift, int>();

        private readonly HashSet<string> elevatorBlindedPlayers =
            new HashSet<string>();

        private readonly Dictionary<Lift, float> restoreImmunity =
            new Dictionary<Lift, float>();

        private CoroutineHandle monitorHandle;
        private CoroutineHandle brokenLiftEffectHandle;
        private bool registered;

        public ElevatorBreakSystem(Config config)
        {
            this.config = config;
        }

        public bool IsEnabled { get; private set; }

        public void Register()
        {
            if (registered)
                return;

            registered = true;

            Exiled.Events.Handlers.Map.ExplodingGrenade += OnExplodingGrenade;
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundEnded += OnRoundEnded;

            monitorHandle = Timing.RunCoroutine(MonitorOverweight());
            brokenLiftEffectHandle = Timing.RunCoroutine(MonitorBrokenLiftEffects());
        }

        public void Unregister()
        {
            if (!registered)
                return;

            registered = false;

            Exiled.Events.Handlers.Map.ExplodingGrenade -= OnExplodingGrenade;
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEnded;

            if (monitorHandle.IsRunning) Timing.KillCoroutines(monitorHandle);
            if (brokenLiftEffectHandle.IsRunning) Timing.KillCoroutines(brokenLiftEffectHandle);

            Disable(true);
        }

        public void Enable()
        {
            IsEnabled = true;
            passengerSignatures.Clear();
            Log.Info("[ElevatorBreak] Система включена.");
        }

        public void Disable(bool restore)
        {
            IsEnabled = false;
            passengerSignatures.Clear();

            if (restore)
                RestoreAll();

            Log.Info("[ElevatorBreak] Система выключена.");
        }

        private void OnRoundStarted() => RestoreAll();
        private void OnRoundEnded(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev) => RestoreAll();

        private void OnExplodingGrenade(ExplodingGrenadeEventArgs ev)
        {
            if (!IsEnabled || ev.Projectile == null)
                return;

            Lift lift = Lift.Get(ev.Projectile.Position);
            if (lift == null || lift.Base == null || brokenLifts.ContainsKey(lift))
                return;

            if (!grenadeExplosions.TryGetValue(lift, out int explosionCount))
                explosionCount = 0;

            explosionCount++;
            grenadeExplosions[lift] = explosionCount;

            if (explosionCount == 1)
            {
                Log.Info("[ElevatorBreak] Первая граната в лифте: лифт гарантированно не ломается.");
                return;
            }

            float chance = Mathf.Clamp01(config.ElevatorGrenadeBreakChance);
            if (UnityEngine.Random.value <= chance)
                Break(lift, false, $"граната #{explosionCount}");
        }

        private IEnumerator<float> MonitorOverweight()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(Mathf.Max(0.2f, config.ElevatorWeightCheckInterval));

                if (!IsEnabled) continue;

                try
                {
                    foreach (Lift lift in Lift.List.ToArray())
                    {
                        if (lift == null || brokenLifts.ContainsKey(lift))
                            continue;

                        List<Player> passengers = GetPassengers(lift);
                        float weight = passengers.Sum(GetWeight);

                        string signature = string.Join("|",
                            passengers.OrderBy(p => p.Id).Select(p => $"{p.Id}:{p.Role.Type}"));

                        passengerSignatures.TryGetValue(lift, out string previousSignature);
                        passengerSignatures[lift] = signature;

                        if (weight <= config.ElevatorMaxWeight || signature == previousSignature)
                            continue;

                        if (UnityEngine.Random.value <= Mathf.Clamp01(config.ElevatorOverweightBreakChance))
                            Break(lift, true, $"перегрузка {weight:0} кг / {passengers.Count} игроков");
                    }
                }
                catch (Exception exception)
                {
                    Log.Error($"[ElevatorBreak] Ошибка проверки веса: {exception}");
                }
            }
        }

        private IEnumerator<float> MonitorBrokenLiftEffects()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(Mathf.Max(0.05f, config.ElevatorBrokenLiftEffectInterval));

                try
                {
                    RestoreExternallyUnlockedLifts();
                    UpdateBrokenLiftBlindness();
                }
                catch (Exception exception)
                {
                    Log.Error($"[ElevatorBreak] Ошибка мониторинга сломанных лифтов: {exception}");
                }
            }
        }

        private void UpdateBrokenLiftBlindness()
        {
            HashSet<string> currentlyInside = new HashSet<string>();

            foreach (Player player in Player.List)
            {
                if (player == null || !player.IsAlive || player.IsNPC) continue;

                Lift currentLift = Lift.Get(player.Position);
                if (currentLift == null || !brokenLifts.ContainsKey(currentLift)) continue;

                currentlyInside.Add(player.UserId);
                player.ChangeEffectIntensity(EffectType.Blinded, config.ElevatorBlindnessIntensity, 0.3f);
                elevatorBlindedPlayers.Add(player.UserId);
            }

            foreach (string userId in elevatorBlindedPlayers.ToArray())
            {
                if (currentlyInside.Contains(userId)) continue;

                Player player = Player.Get(userId);
                player?.DisableEffect(EffectType.Blinded);
                elevatorBlindedPlayers.Remove(userId);
            }
        }

        private static List<Player> GetPassengers(Lift lift)
        {
            return Player.List
                .Where(player => player != null && player.IsAlive && !player.IsNPC && Lift.Get(player.Position) == lift)
                .ToList();
        }

        private float GetWeight(Player player)
        {
            switch (player.Role.Type)
            {
                case RoleTypeId.Scp939: return config.ElevatorScp939Weight;
                case RoleTypeId.Scp173: return config.ElevatorScp173Weight;
                case RoleTypeId.Scp096: return config.ElevatorScp096Weight;
                case RoleTypeId.Scp3114: return config.ElevatorScp3114Weight;
                case RoleTypeId.Scp049: return config.ElevatorScp049Weight;
                case RoleTypeId.Scp0492: return config.ElevatorScp0492Weight;
                default: return config.ElevatorHumanWeight;
            }
        }

        private void Break(Lift lift, bool overweight, string reason)
        {
            if (lift?.Base == null || brokenLifts.ContainsKey(lift))
                return;

            // Проверка иммунитета после restore
            if (restoreImmunity.TryGetValue(lift, out float immuneUntil) &&
                Time.time < immuneUntil)
            {
                return;
            }

            BrokenLift state = new BrokenLift
            {
                Lift = lift,
                LiftRoot = lift.Transform,
            };

            brokenLifts.Add(lift, state);

            GiveScannedToLiftPlayers(lift);
            CloseAndLockLift(lift);

            Timing.CallDelayed(0.15f, () =>
            {
                if (lift != null && brokenLifts.ContainsKey(lift))
                    CloseAndLockLift(lift);
            });

            state.SoundHandle = Audio.SoundService.PlayFollowing(
                lift,
                config.ElevatorBrokenSound,
                config.ElevatorBrokenSoundVolume,
                config.ElevatorBrokenSoundMinDistance,
                config.ElevatorBrokenSoundMaxDistance);

            if (overweight && !string.IsNullOrWhiteSpace(config.ElevatorBrokenSchematic))
            {
                state.BrokenSchematic = SpawnSchematic(
                    config.ElevatorBrokenSchematic, state.LiftRoot,
                    out state.SchematicLocalPosition, out state.SchematicLocalRotation);

                state.FollowHandle = Timing.RunCoroutine(FollowBrokenSchematic(state));
                state.UnlockHandle = Timing.RunCoroutine(UnlockAfter(state, config.ElevatorOverweightLockSeconds));
            }

            FileLog.Write($"[Elevator] СЛОМАН {lift.Name}: {reason}. Дверей: {lift.Doors.Count}, IsLocked={lift.IsLocked}");
            Log.Info($"[ElevatorBreak] Лифт {lift.Name} сломан: {reason}.");
        }

        private IEnumerator<float> UnlockAfter(BrokenLift state, float seconds)
        {
            yield return Timing.WaitForSeconds(Mathf.Max(0f, seconds));
            if (state?.Lift == null) yield break;
            RestoreLift(state.Lift, "истекло время блокировки после перегруза");
        }

        private void GiveScannedToLiftPlayers(Lift lift)
        {
            if (lift == null) return;
            foreach (Player player in lift.Players.ToArray())
            {
                if (player == null || !player.IsAlive || player.IsNPC) continue;
                player.EnableEffect(EffectType.Scanned, config.ElevatorScannedDuration);
            }
        }

        private static bool CloseAndLockLift(Lift lift)
        {
            if (lift == null) return false;
            bool foundDoor = false;

            try
            {
                foreach (var door in lift.Doors)
                {
                    if (door == null) continue;
                    foundDoor = true;
                    door.IsOpen = false;

                    // Lock — всегда ДОБАВЛЯЕТ флаг, не toggle.
                    door.Lock(DoorLockType.Warhead);
                }

                // НЕ вызывать lift.ChangeLock() — это toggle!
                // Если door.Lock уже поставил Warhead,
                // lift.ChangeLock его СНЯЛ бы.
            }
            catch (Exception exception)
            {
                Log.Error("[ElevatorBreak] Ошибка блокировки: " + exception);
            }

            return foundDoor;
        }

        private IEnumerator<float> FollowBrokenSchematic(BrokenLift state)
        {
            while (state != null && state.BrokenSchematic != null && state.LiftRoot != null && brokenLifts.ContainsKey(state.Lift))
            {
                yield return Timing.WaitForOneFrame;
                Vector3 worldPosition = state.LiftRoot.TransformPoint(state.SchematicLocalPosition);
                Quaternion worldRotation = state.LiftRoot.rotation * state.SchematicLocalRotation;
                SetMember(state.BrokenSchematic, "Position", worldPosition);
                SetMember(state.BrokenSchematic, "Rotation", worldRotation);
                SetMember(state.BrokenSchematic, "NetworkPosition", worldPosition);
                SetMember(state.BrokenSchematic, "NetworkRotation", worldRotation.eulerAngles);
            }
        }

        public int RestoreAll()
        {
            FileLog.Write($"[Elevator] elevat restore: сломанных лифтов в списке: {brokenLifts.Count}");
            int restoredCount = 0;
            foreach (Lift lift in brokenLifts.Keys.ToArray())
            {
                if (RestoreLift(lift, "команда restore"))
                    restoredCount++;
            }

            foreach (string userId in elevatorBlindedPlayers.ToArray())
            {
                Player player = Player.Get(userId);
                player?.DisableEffect(EffectType.Blinded);
            }

            elevatorBlindedPlayers.Clear();
            grenadeExplosions.Clear();
            passengerSignatures.Clear();
            return restoredCount;
        }

        private bool RestoreLift(Lift lift, string reason)
        {
            if (lift == null)
                return false;

            if (!brokenLifts.TryGetValue(lift, out BrokenLift state))
                return false;

            // Сначала убираем из сломанных, чтобы мониторы не мешали
            brokenLifts.Remove(lift);

            // Останавливаем зацикленный звук
            SoundService.StopHandle(state.SoundHandle);
            state.SoundHandle = null;

            // 5 секунд иммунитета — иначе перегруз/мониторы могут
            // сломать лифт обратно в ту же секунду
            restoreImmunity[lift] = Time.time + 5f;

            if (state.UnlockHandle.IsRunning)
                Timing.KillCoroutines(state.UnlockHandle);
            if (state.FollowHandle.IsRunning)
                Timing.KillCoroutines(state.FollowHandle);

            try
            {
                // Полная очистка блокировок — только прямой API без reflection.
                // lift.ChangeLock(DoorLockReason.None) снимает блокировку со всех дверей группы.
                lift.ChangeLock(DoorLockReason.None);

                foreach (ElevatorDoor door in lift.Doors)
                {
                    if (door == null)
                        continue;

                    door.DoorLockType = DoorLockType.None;
                    door.Unlock();
                }

                // КЛЮЧЕВОЕ: пересинхронизация шахты.
                // На тот же этаж TryStart — no-op, поэтому шлём лифт на ДРУГОЙ:
                // он приедет и сам откроет двери.
                int level = lift.CurrentLevel;

                Timing.CallDelayed(0.2f, () =>
                {
                    try
                    {
                        int target = level == 0 ? 1 : 0;
                        lift.TryStart(target, true);
                        FileLog.Write($"[Elevator] restore: TryStart({target}) ok");
                    }
                    catch (Exception e)
                    {
                        FileLog.WriteEx("[Elevator] restore TryStart", e);
                    }
                });
            }
            catch (Exception exception)
            {
                FileLog.Write($"[Elevator] ОШИБКА restore {lift.Name}: {exception}");
            }

            DestroyObject(state.BrokenSchematic);
            grenadeExplosions.Remove(lift);
            passengerSignatures.Remove(lift);

            foreach (Player player in lift.Players.ToArray())
            {
                if (player != null &&
                    elevatorBlindedPlayers.Remove(player.UserId))
                {
                    player.DisableEffect(EffectType.Blinded);
                }
            }

            FileLog.Write($"[Elevator] ВОССТАНОВЛЕН {lift.Name} ({reason}). IsLocked теперь: {lift.IsLocked}, этаж: {lift.CurrentLevel}");
            Log.Info($"[ElevatorBreak] {lift.Name} восстановлен ({reason}). IsLocked теперь: {lift.IsLocked}");
            return true;
        }

        private void RestoreExternallyUnlockedLifts()
        {
            foreach (Lift lift in brokenLifts.Keys.ToArray())
            {
                if (lift == null) continue;

                bool anyDoorUnlocked = false;
                try
                {
                    anyDoorUnlocked = lift.Doors.Any(door => door != null && !door.IsLocked);
                }
                catch (Exception exception)
                {
                    Log.Warn($"[ElevatorBreak] Проверка блокировки лифта {lift.Name}: {exception.Message}");
                    continue;
                }

                if (!lift.IsLocked || anyDoorUnlocked)
                    RestoreLift(lift, "блокировка снята администратором");
            }
        }

        private static object SpawnSchematic(string name, Transform liftRoot, out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            if (liftRoot == null || string.IsNullOrWhiteSpace(name)) return null;

            Vector3 worldPosition = liftRoot.position;
            Quaternion worldRotation = liftRoot.rotation;
            localPosition = liftRoot.InverseTransformPoint(worldPosition);
            localRotation = Quaternion.Inverse(liftRoot.rotation) * worldRotation;

            try
            {
                Type spawnerType = Type.GetType("ProjectMER.Features.ObjectSpawner, ProjectMER");
                Type schematicType = Type.GetType("ProjectMER.Features.Objects.SchematicObject, ProjectMER");
                if (spawnerType == null || schematicType == null)
                {
                    Log.Error("[ElevatorBreak] ProjectMER не найден.");
                    return null;
                }

                var method = spawnerType.GetMethod("TrySpawnSchematic",
                    new[] { typeof(string), typeof(Vector3), typeof(Quaternion), schematicType.MakeByRefType() });
                if (method == null)
                {
                    Log.Error("[ElevatorBreak] TrySpawnSchematic не найден.");
                    return null;
                }

                object[] args = { name, worldPosition, worldRotation, null };
                bool result = (bool)method.Invoke(null, args);
                if (!result)
                {
                    Log.Error($"[ElevatorBreak] Не удалось создать {name}.");
                    return null;
                }
                return args[3];
            }
            catch (Exception exception)
            {
                Log.Warn($"[ElevatorBreak] Не удалось создать {name}: {exception.Message}");
                return null;
            }
        }

        private static void DestroyObject(object obj)
        {
            if (obj == null) return;
            try { obj.GetType().GetMethod("Destroy", Type.EmptyTypes)?.Invoke(obj, null); }
            catch (Exception exception) { Log.Warn($"[ElevatorBreak] Ошибка удаления объекта: {exception.Message}"); }
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsInstanceOfType(value)) return value;
            if (targetType == typeof(Quaternion) && value is Vector3 euler) return Quaternion.Euler(euler);
            if (targetType == typeof(Vector3) && value is Quaternion quaternion) return quaternion.eulerAngles;
            return Convert.ChangeType(value, targetType);
        }

        private static bool SetMember(object target, string name, object value)
        {
            if (target == null) return false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            try
            {
                var property = target.GetType().GetProperty(name, flags);
                if (property?.CanWrite == true) { property.SetValue(target, ConvertValue(value, property.PropertyType)); return true; }
                var field = target.GetType().GetField(name, flags);
                if (field != null) { field.SetValue(target, ConvertValue(value, field.FieldType)); return true; }
            }
            catch { }
            return false;
        }
    }
}