using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using EventHUD.Hud;
using MEC;
using UnityEngine;

namespace EventHUD.Tripwire
{
    public enum WireState { Armed, Broken, Disarmed, Empty }

    public static class TripwireSystem
    {
        private class Wire
        {
            public int Id;
            public object Schematic;
            public Vector3 Pos;
            public Quaternion Rot;
            public string OwnerUserId;
            public double PlacedAt;
            public bool Grabbed;
            public string GrabbedBy;
            public WireState State = WireState.Armed;
            public Lift Lift;
            public Vector3 LiftLocalPos;
            public Quaternion LiftLocalRot;
        }

        private static readonly List<Wire> Wires = new List<Wire>();
        private static int _nextId = 1;
        private static CoroutineHandle _loop;

        private static readonly Vector3 GrenadeLocalOffset = new Vector3(0.8144f, 0.1111f, -0.001799583f);

        // ── Disarm sessions ──
        private class DisarmSession { public Wire Wire; public float Elapsed; }
        private static readonly Dictionary<string, DisarmSession> Sessions = new();

        // ── Knife wear ──
        public static readonly Dictionary<string, int> KnifeWear = new();
        public static readonly Dictionary<string, int> KnifeLimit = new();
        public static readonly HashSet<string> Sharpening = new();

        private static Func<object, Vector3> _schematicGetPos;
        private static Action<object, Vector3> _schematicSetPos;
        private static Func<object, bool> _schematicDestroy;
        private static bool _merChecked;

        private static void InitMer()
        {
            if (_merChecked) return;
            _merChecked = true;
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!a.FullName.StartsWith("ProjectMER")) continue;
                    var schematicType = a.GetType("ProjectMER.Features.Objects.SchematicObject");
                    if (schematicType != null)
                    {
                        var posProp = schematicType.GetProperty("Position");
                        if (posProp != null)
                        {
                            _schematicGetPos = obj => (Vector3)posProp.GetValue(obj);
                            _schematicSetPos = (obj, v) => posProp.SetValue(obj, v);
                        }
                        var destroyMethod = schematicType.GetMethod("Destroy", Type.EmptyTypes);
                        if (destroyMethod != null)
                            _schematicDestroy = obj => { destroyMethod.Invoke(obj, null); return true; };
                    }
                    break;
                }
            }
            catch { }
        }

        private static void SetSchematicPos(object schematic, Vector3 pos)
        {
            if (schematic == null) return;
            if (_schematicSetPos != null) { try { _schematicSetPos(schematic, pos); } catch { } return; }
            try { var prop = schematic.GetType().GetProperty("Position"); prop?.SetValue(schematic, pos); } catch { }
        }

        private static void SetSchematicRot(object schematic, Quaternion rot)
        {
            if (schematic == null) return;
            try { var prop = schematic.GetType().GetProperty("Rotation"); prop?.SetValue(schematic, rot); } catch { }
        }

        private static void DestroySchematic(object schematic)
        {
            if (schematic == null) return;
            if (_schematicDestroy != null) { try { _schematicDestroy(schematic); } catch { } return; }
            try { var method = schematic.GetType().GetMethod("Destroy", Type.EmptyTypes); method?.Invoke(schematic, null); } catch { }
        }

        private static bool TryFindFloor(Vector3 origin, out Vector3 floor)
        {
            floor = default;
            float best = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(origin + Vector3.up * 0.5f, Vector3.down, 30f))
            {
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.collider.GetComponentInParent<ReferenceHub>() != null) continue;
                if (_merChecked && _merSchematicType != null && hit.collider.GetComponent(_merSchematicType) != null) continue;
                if (hit.distance < best) { best = hit.distance; floor = hit.point; }
            }
            return best != float.MaxValue;
        }

        private static Type _merSchematicType;

        private static void BindToLift(Wire wire)
        {
            wire.Lift = Lift.Get(wire.Pos);
            if (wire.Lift?.Base == null) { wire.Lift = null; return; }
            Transform ch = wire.Lift.Base.transform;
            wire.LiftLocalPos = ch.InverseTransformPoint(wire.Pos);
            wire.LiftLocalRot = Quaternion.Inverse(ch.rotation) * wire.Rot;
        }

        public static bool Place(Player player, out int id, out string error)
        {
            id = 0;
            error = null;
            if (!TryFindFloor(player.Position, out Vector3 floor)) { error = "Пол под игроком не найден."; return false; }

            InitMer();
            Quaternion rot = Quaternion.Euler(0f, player.Rotation.eulerAngles.y, 0f);
            object schematic = null;
            try
            {
                var spawnerType = Type.GetType("ProjectMER.Features.ObjectSpawner, ProjectMER");
                _merSchematicType = Type.GetType("ProjectMER.Features.Objects.SchematicObject, ProjectMER");
                if (spawnerType != null && _merSchematicType != null)
                {
                    var trySpawnMethod = spawnerType.GetMethod("TrySpawnSchematic", new[] { typeof(string), typeof(Vector3), typeof(Quaternion), _merSchematicType.MakeByRefType() });
                    if (trySpawnMethod != null)
                    {
                        var args = new object[] { "NF_Tripwire_BD", floor, rot, null };
                        bool ok = (bool)trySpawnMethod.Invoke(null, args);
                        if (ok && args[3] != null) schematic = args[3];
                        else Logging.DebugFileLog.Write("[Tripwire] TrySpawnSchematic вернул false");
                    }
                }
            }
            catch (Exception e) { Logging.DebugFileLog.Write($"[Tripwire] Ошибка спавна: {e}"); }

            var wire = new Wire { Id = _nextId++, Schematic = schematic, Pos = floor, Rot = rot, OwnerUserId = player.UserId, PlacedAt = Timing.LocalTime };
            BindToLift(wire);
            Wires.Add(wire);
            if (!_loop.IsRunning) _loop = Timing.RunCoroutine(Loop());
            return true;
        }

        public static string Grab(Player player)
        {
            for (int i = 0; i < Wires.Count; i++)
            {
                Wire carried = Wires[i];
                if (!carried.Grabbed || carried.GrabbedBy != player.UserId) continue;
                if (carried.State != WireState.Armed) return "Растяжка уже использована.";
                carried.Grabbed = false; carried.GrabbedBy = null; carried.OwnerUserId = player.UserId; carried.PlacedAt = Timing.LocalTime;
                BindToLift(carried);
                return $"Растяжка #{carried.Id} поставлена.";
            }
            Wire wire = FindByLook(player);
            if (wire == null) return "Ты не смотришь ни на одну растяжку (до 15м).";
            if (wire.State != WireState.Armed) return "Растяжка уже использована.";
            wire.Grabbed = true; wire.GrabbedBy = player.UserId;
            wire.Lift = null;
            return $"Растяжка #{wire.Id} взята — наведи куда поставить и снова введи rast grab.";
        }

        private static Wire FindByLook(Player player, float maxDist = 15f)
        {
            Vector3 origin = player.CameraTransform.position;
            Vector3 forward = player.CameraTransform.forward;
            Wire best = null;
            float bestDist = float.MaxValue;
            foreach (Wire wire in Wires)
            {
                Vector3 to = wire.Pos - origin;
                float dist = to.magnitude;
                if (dist > maxDist || Vector3.Dot(forward, to.normalized) <= 0f) continue;
                if (Vector3.Cross(forward, to).magnitude > 0.8f) continue;
                if (dist < bestDist) { bestDist = dist; best = wire; }
            }
            return best;
        }

        public static bool Remove(int id)
        {
            for (int i = 0; i < Wires.Count; i++)
            {
                if (Wires[i].Id != id) continue;
                DropSessionsFor(Wires[i]);
                DestroySchematic(Wires[i].Schematic);
                Wires.RemoveAt(i);
                return true;
            }
            return false;
        }

        public static bool RemoveByLook(Player player, out int removedId)
        {
            removedId = 0;
            Wire wire = FindByLook(player);
            if (wire == null) return false;
            removedId = wire.Id;
            return Remove(wire.Id);
        }

        public static string ListWires()
        {
            if (Wires.Count == 0) return "Активных растяжек нет.";
            var sb = new StringBuilder("Растяжки:\n");
            foreach (Wire w in Wires)
            {
                sb.Append($"#{w.Id} — {w.Pos}");
                if (w.State != WireState.Armed) sb.Append($" ({w.State})");
                if (w.Grabbed) sb.Append(" (в руках)");
                if (w.Lift != null) sb.Append(" (в лифте)");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static void ClearAll()
        {
            foreach (Wire w in Wires) DestroySchematic(w.Schematic);
            Wires.Clear();
            _nextId = 1;
            Sessions.Clear();
            KnifeWear.Clear();
            KnifeLimit.Clear();
            Sharpening.Clear();
        }

        public static bool IsScissorsType(ItemType t) =>
            t.ToString().Contains("1509") || t == ItemType.Jailbird;

        private static bool HasScissors(Player player) =>
            player.Items.Any(i => i != null && IsScissorsType(i.Type));

        private static bool IsKnifeDull(Player p)
        {
            if (!KnifeLimit.TryGetValue(p.UserId, out int lim))
                KnifeLimit[p.UserId] = lim = UnityEngine.Random.Range(10, 16);
            return KnifeWear.TryGetValue(p.UserId, out int w) && w >= lim;
        }

        private static void AddKnifeWear(Player p) =>
            KnifeWear[p.UserId] = KnifeWear.TryGetValue(p.UserId, out int w) ? w + 1 : 1;

        private static void DropSessionsFor(Wire wire)
        {
            foreach (var kv in Sessions.Where(kv => kv.Value.Wire == wire).ToList())
                Sessions.Remove(kv.Key);
        }

        public static string GetDisarmHudLine(Player player)
        {
            if (!Sessions.TryGetValue(player.UserId, out var s)) return null;

            if (s.Wire == null || s.Wire.State != WireState.Armed ||
                s.Wire.Grabbed || !Wires.Contains(s.Wire))
            {
                Sessions.Remove(player.UserId);
                return null;
            }

            int total = 12;
            int filled = Mathf.Clamp(Mathf.RoundToInt(s.Elapsed / 3f * total), 0, total);
            string bar = new string('█', filled) + new string('░', total - filled);
            return $"Перерезание проволоки [{bar}] {s.Elapsed:0.0}/3.0с";
        }

        // ── Бинд 9040: старт автоматического перерезания (3 сек) ──
        public static void TryDisarmByScissors(Player player)
        {
            if (Sessions.ContainsKey(player.UserId)) return;

            if (!IsScissorsType(player.CurrentItem?.Type ?? ItemType.None))
            { HudNoticeService.Show(player, "<color=red>Возьмите ножницы (SCP-1509) в руки</color>", 1.5f); return; }

            if (IsKnifeDull(player))
            { HudNoticeService.Show(player, "<color=red>Ножницы затупились — заточите (.rewind)</color>", 2f); return; }

            if (Sharpening.Contains(player.UserId))
            { HudNoticeService.Show(player, "<color=red>Вы уже точите ножницы</color>", 1.5f); return; }

            Wire wire = FindByLook(player, 3.5f);
            if (wire == null || wire.State != WireState.Armed)
            { HudNoticeService.Show(player, "<color=red>Растяжка не найдена</color>", 1.5f); return; }

            var s = new DisarmSession { Wire = wire, Elapsed = 0f };
            Sessions[player.UserId] = s;
            Timing.RunCoroutine(DisarmCoroutine(player, s));
        }

        private static IEnumerator<float> DisarmCoroutine(Player player, DisarmSession s)
        {
            while (s.Elapsed < 3f)
            {
                yield return Timing.WaitForSeconds(0.25f);
                s.Elapsed += 0.25f;

                if (player == null || !player.IsAlive ||
                    !IsScissorsType(player.CurrentItem?.Type ?? ItemType.None) ||
                    s.Wire == null || s.Wire.State != WireState.Armed || !Wires.Contains(s.Wire) ||
                    Vector3.Distance(player.Position, s.Wire.Pos) > 3.5f)
                {
                    Sessions.Remove(player.UserId);
                    yield break;
                }
            }

            Sessions.Remove(player.UserId);
            Disarm(s.Wire);
            AddKnifeWear(player);
            HudNoticeService.Show(player, "<color=green>Растяжка обезврежена</color>", 2f);
        }

        // ── Бинд 9041: взять гранату ──
        public static void TryTakeGrenade(Player player)
        {
            Wire wire = FindByLook(player, 3.5f);
            if (wire == null)
            { HudNoticeService.Show(player, "<color=red>Растяжка не найдена</color>", 1.5f); return; }

            if (wire.State == WireState.Armed)
            { HudNoticeService.Show(player, "<color=red>Сначала обезвредьте растяжку</color>", 1.5f); return; }

            if (wire.State != WireState.Disarmed)
            { HudNoticeService.Show(player, "<color=red>Гранаты здесь нет</color>", 1.5f); return; }

            if (player.Items.Count >= 8)
            { HudNoticeService.Show(player, "<color=red>Инвентарь полон</color>", 1.5f); return; }

            DestroySchematic(wire.Schematic);
            wire.Schematic = SpawnPermanent("NF_Tripwire_BD3", wire.Pos, wire.Rot);
            wire.State = WireState.Empty;

            player.AddItem(ItemType.GrenadeHE);
            HudNoticeService.Show(player, "<color=green>Вы забрали гранату</color>", 2f);
        }

        // ── Урон SCP-1509 = 15 ──
        public static void OnHurting(Exiled.Events.EventArgs.Player.HurtingEventArgs ev)
        {
            if (ev.Attacker == null || ev.Player == null) return;

            // Во время сессии обезвреживания — никакого урона
            if (Sessions.ContainsKey(ev.Attacker.UserId))
            { ev.IsAllowed = false; return; }

            ItemType t = ev.Attacker.CurrentItem?.Type ?? ItemType.None;
            if (!IsScissorsType(t)) return;

            ev.Amount = 15f;
        }

        private static object SpawnPermanent(string name, Vector3 pos, Quaternion rot)
        {
            try
            {
                var spawnerType = Type.GetType("ProjectMER.Features.ObjectSpawner, ProjectMER");
                var schematicType = Type.GetType("ProjectMER.Features.Objects.SchematicObject, ProjectMER");
                if (spawnerType != null && schematicType != null)
                {
                    var trySpawnMethod = spawnerType.GetMethod("TrySpawnSchematic", new[] { typeof(string), typeof(Vector3), typeof(Quaternion), schematicType.MakeByRefType() });
                    if (trySpawnMethod != null)
                    {
                        var args = new object[] { name, pos, rot, null };
                        if ((bool)trySpawnMethod.Invoke(null, args) && args[3] != null)
                            return args[3];
                        else Logging.DebugFileLog.Write($"[Tripwire] Не удалось заспавнить {name}");
                    }
                }
            }
            catch (Exception e) { Logging.DebugFileLog.Write($"[Tripwire] {name}: {e.Message}"); }
            return null;
        }

        private static void Disarm(Wire wire)
        {
            DropSessionsFor(wire);
            DestroySchematic(wire.Schematic);
            wire.Schematic = SpawnPermanent("NF_Tripwire_BD2", wire.Pos, wire.Rot);
            wire.State = WireState.Disarmed;
        }

        public static void OnShotFired(Player player)
        {
            if (player == null || player.CameraTransform == null) return;
            Vector3 origin = player.CameraTransform.position;
            Vector3 dir = player.CameraTransform.forward;
            for (int i = Wires.Count - 1; i >= 0; i--)
            {
                Wire wire = Wires[i];
                if (wire.State != WireState.Armed || wire.Grabbed) continue;
                Vector3 grenadePos = wire.Pos + wire.Rot * GrenadeLocalOffset;
                Vector3 to = grenadePos - origin;
                float along = Vector3.Dot(to, dir);
                if (along < 0f || along > 60f) continue;
                float distToRay = Vector3.Cross(dir, to).magnitude;
                if (distToRay > 0.35f) continue;
                DeactivateByShot(wire);
                break;
            }
        }

        private static void DeactivateByShot(Wire wire)
        {
            DropSessionsFor(wire);
            DestroySchematic(wire.Schematic);
            wire.Schematic = SpawnPermanent("NF_Tripwire_BD1", wire.Pos, wire.Rot);
            wire.State = WireState.Broken;
            Audio.SoundService.Play("tripwire", wire.Pos);
            Vector3 grenadePos = wire.Pos + wire.Rot * GrenadeLocalOffset;
            Pickup grenade = Pickup.CreateAndSpawn(ItemType.GrenadeHE, grenadePos + Vector3.up * 0.1f, wire.Rot);
            Rigidbody rb = grenade?.Base != null ? grenade.Base.GetComponent<Rigidbody>() : null;
            if (rb != null) rb.velocity = Vector3.up * 2.5f + wire.Rot * Vector3.forward * 1.5f;
        }

        private static void Trigger(Wire wire)
        {
            DropSessionsFor(wire);
            DestroySchematic(wire.Schematic);
            wire.Schematic = SpawnPermanent("NF_Tripwire_BD1", wire.Pos, wire.Rot);
            wire.State = WireState.Broken;
            Audio.SoundService.Play("tripwire", wire.Pos);
            Vector3 grenadePos = wire.Pos + wire.Rot * GrenadeLocalOffset;
            ExplosiveGrenade grenade = (ExplosiveGrenade)Item.Create(ItemType.GrenadeHE);
            grenade.FuseTime = 1.5f;
            grenade.SpawnActive(grenadePos);
        }

        private static IEnumerator<float> Loop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(0.1f);
                try
                {
                    for (int i = Wires.Count - 1; i >= 0; i--)
                    {
                        Wire wire = Wires[i];

                        if (wire.Lift?.Base != null)
                        {
                            Transform ch = wire.Lift.Base.transform;
                            Vector3 newPos = ch.TransformPoint(wire.LiftLocalPos);
                            if ((newPos - wire.Pos).sqrMagnitude > 0.0001f)
                            {
                                wire.Pos = newPos;
                                wire.Rot = ch.rotation * wire.LiftLocalRot;
                                SetSchematicPos(wire.Schematic, newPos);
                                SetSchematicRot(wire.Schematic, wire.Rot);
                            }
                            if (wire.State == WireState.Armed && wire.Lift.IsMoving)
                                continue;
                        }

                        if (wire.State != WireState.Armed) continue;

                        if (wire.Grabbed)
                        {
                            Player carrier = Player.Get(wire.GrabbedBy);
                            if (carrier == null || carrier.IsDead) { wire.Grabbed = false; wire.GrabbedBy = null; wire.PlacedAt = Timing.LocalTime; BindToLift(wire); continue; }
                            Vector3 aim = Physics.Raycast(carrier.CameraTransform.position, carrier.CameraTransform.forward, out RaycastHit aimHit, 10f) ? aimHit.point : carrier.CameraTransform.position + carrier.CameraTransform.forward * 6f;
                            if (TryFindFloor(aim, out Vector3 floor)) { wire.Pos = floor; SetSchematicPos(wire.Schematic, floor); }
                            continue;
                        }

                        foreach (Player player in Player.List)
                        {
                            if (player.IsDead || player.IsNPC) continue;
                            if (player.UserId == wire.OwnerUserId && Timing.LocalTime - wire.PlacedAt < 3.0) continue;
                            if (player.Role is Exiled.API.Features.Roles.FpcRole fpc && !fpc.FirstPersonController.FpcModule.IsGrounded) continue;
                            Vector3 d = player.Position - wire.Pos;
                            float horizontal = new Vector2(d.x, d.z).magnitude;
                            if (horizontal <= 0.4f && d.y > 0.2f && d.y < 1.4f) { Trigger(wire); break; }
                        }

                        if (Wires.Count <= i || Wires[i] != wire) continue;

                        foreach (Pickup pickup in Pickup.List)
                        {
                            if (pickup == null) continue;
                            if (pickup.Weight < 0.3f) continue;
                            Vector3 pd = pickup.Position - wire.Pos;
                            Vector3 ph = new Vector3(pd.x, 0f, pd.z);
                            if (ph.magnitude > 0.5f || pd.y < -0.3f || pd.y > 1.4f) continue;
                            Rigidbody rb = pickup.Base != null ? pickup.Base.GetComponent<Rigidbody>() : null;
                            if (rb == null || rb.velocity.magnitude < 0.4f) continue;
                            Trigger(wire);
                            break;
                        }
                    }
                }
                catch { }
            }
        }
    }
}