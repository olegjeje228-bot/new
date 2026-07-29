using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventHUD.Audio;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Pickups;
using MEC;
using PlayerRoles;
using UnityEngine;

namespace EventHUD.Cube
{
    public sealed class CubeLootSystem
    {
        public static CubeLootSystem Instance { get; private set; }

        private bool active;
        private CoroutineHandle scanHandle;

        private readonly HashSet<Room> visitedRooms = new HashSet<Room>();
        private readonly HashSet<Room> generatedRooms = new HashSet<Room>();
        private readonly HashSet<Pickup> spawnedPickups = new HashSet<Pickup>();
        private readonly Dictionary<Room, HashSet<Room>> graph =
            new Dictionary<Room, HashSet<Room>>();
        private bool surfaceGenerated;

        private readonly Dictionary<Room, List<Vector3>> roomSpawnPoints =
            new Dictionary<Room, List<Vector3>>();

        private readonly List<CubeRollRecord> currentKubLog =
            new List<CubeRollRecord>();

        private int kubSessionNumber;
        private DateTime kubStartedAt;
        private DateTime? kubStoppedAt;

        public int CurrentLuck { get; private set; }

        private readonly List<ItemType> nextRoomItems =
            new List<ItemType>();

        private bool ConfigEnabled => Plugin.Instance?.Config.CubeLootEnabled ?? false;
        private int PreloadDistance => Plugin.Instance?.Config.CubePreloadRoomDistance ?? 2;
        private float ScanInterval => Plugin.Instance?.Config.CubeRoomScanInterval ?? 0.35f;
        private float LootSpacing => Plugin.Instance?.Config.CubeLootSpacing ?? 0.65f;

        private static readonly Dictionary<string, string[]> ItemAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["SurfaceAccessPass"] = new[] { "KeycardCustomSite02", "KeycardCustomManagement", "KeycardCustomMetalCase" },
            ["Lantern"] = new[] { "Lantern" },
            ["GunA7"] = new[] { "GunA7", "A7" },
            ["SCP127"] = new[] { "SCP127", "GunSCP127" },
            ["AntiSCP207"] = new[] { "AntiSCP207", "SCP207Anti" },
        };

        private static string GetItemDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "?";
            return name;
        }

        private static string GetRussianRoomName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return "neizvestnaya komnata";

            string lower = typeName.ToLowerInvariant();

            if (lower.Contains("330")) return "Komnata SCP-330";
            if (lower.Contains("914")) return "Komnata SCP-914";
            if (lower.Contains("049")) return "Komnata SCP-049";
            if (lower.Contains("096")) return "Komnata SCP-096";
            if (lower.Contains("106")) return "Komnata SCP-106";
            if (lower.Contains("939")) return "Komnata SCP-939";
            if (lower.Contains("173")) return "Komnata SCP-173";
            if (lower.Contains("079")) return "Komnata SCP-079";
            if (lower.Contains("457")) return "Komnata SCP-457";
            if (lower.Contains("966")) return "Komnata SCP-966";
            if (lower.Contains("gr18") || lower.Contains("glass")) return "GR-18";
            if (lower.Contains("armory") || lower.Contains("armoury")) return "Oruzheynaya";
            if (lower.Contains("shelter")) return "Ubezhishche";
            if (lower.Contains("surface")) return "Poverkhnost";
            if (lower.Contains("lcz") || lower.Contains("light")) return "Svetlaya zona";
            if (lower.Contains("hcz") || lower.Contains("heavy")) return "Tyazhelaya zona";
            if (lower.Contains("ez") || lower.Contains("entrance")) return "Vkhodnaya zona";

            return typeName;
        }

        private static string GetRussianProfileName(CubeRoomProfile profile)
        {
            switch (profile)
            {
                case CubeRoomProfile.ZoneLight: return "Svetlaya zona";
                case CubeRoomProfile.ZoneHeavy: return "Tyazhelaya zona";
                case CubeRoomProfile.ZoneEntrance: return "Vkhodnaya zona";
                case CubeRoomProfile.Surface: return "Poverkhnost";
                case CubeRoomProfile.Gr18: return "GR-18";
                case CubeRoomProfile.Scp330: return "SCP-330";
                case CubeRoomProfile.Scp914: return "SCP-914";
                case CubeRoomProfile.Armory: return "Oruzheynaya";
                case CubeRoomProfile.Scp049: return "SCP-049";
                case CubeRoomProfile.OtherScpRoom: return "SCP-komnata";
                default: return profile.ToString();
            }
        }

        public void Register()
        {
            Instance = this;
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundEnded += OnRoundEnded;
        }

        public void Unregister()
        {
            Disable(removeSpawnedLoot: true);
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEnded;
            Instance = null;
        }

        public void Enable()
        {
            if (!ConfigEnabled)
                return;

            Disable(removeSpawnedLoot: true);

            active = true;
            visitedRooms.Clear();
            generatedRooms.Clear();
            spawnedPickups.Clear();
            roomSpawnPoints.Clear();
            currentKubLog.Clear();
            surfaceGenerated = false;

            CurrentLuck = Math.Max(1, Math.Min(1000000, Plugin.Instance?.Config.CubeDefaultLuck ?? 1));

            kubSessionNumber++;
            kubStartedAt = DateTime.Now;
            kubStoppedAt = null;

            CubeHistoryLog.Append("");
            CubeHistoryLog.Append(
                $"========== KUB #{kubSessionNumber} NACHAT " +
                $"{kubStartedAt:dd.MM.yyyy HH:mm:ss} ==========");

            BuildRoomGraph();

            if (graph.Count == 0)
            {
                Timing.CallDelayed(2f, () =>
                {
                    if (!active) return;
                    BuildRoomGraph();
                });
            }

            scanHandle = Timing.RunCoroutine(ScanLoop());

            FileLog.Write(
                $"[Cube] Nachat novyy zapusk Kuba #{kubSessionNumber}. " +
                $"Komnat v grafe: {graph.Count}, distantsiya: {PreloadDistance}");
        }

        public void Disable(bool removeSpawnedLoot)
        {
            active = false;

            if (scanHandle.IsRunning)
                Timing.KillCoroutines(scanHandle);

            if (removeSpawnedLoot)
            {
                foreach (Pickup pickup in spawnedPickups.ToList())
                {
                    try
                    {
                        if (pickup != null && pickup.Base != null)
                            pickup.Destroy();
                    }
                    catch (Exception e)
                    {
                        FileLog.WriteEx("[Cube] Oshibka udaleniya pikapa", e);
                    }
                }
            }

            if (kubStartedAt != default)
            {
                kubStoppedAt = DateTime.Now;

                CubeHistoryLog.Append(
                    $"========== KUB #{kubSessionNumber} ZAVERSHYON " +
                    $"{kubStoppedAt:dd.MM.yyyy HH:mm:ss}; " +
                    $"komnat obrabotano: {currentKubLog.Count} ==========");

                CubeHistoryLog.Append("");
            }

            spawnedPickups.Clear();
            visitedRooms.Clear();
            generatedRooms.Clear();
            roomSpawnPoints.Clear();
            graph.Clear();
            surfaceGenerated = false;

            FileLog.Write("[Cube] Viklyuchen.");
        }

        public string GetStatus()
        {
            return
                $"Kub: {(active ? "vklyuchyon" : "viklyuchen")}\n" +
                $"Komnat v grafe: {graph.Count}\n" +
                $"Poseshcheno: {visitedRooms.Count}\n" +
                $"Obrabotano dlya luta: {generatedRooms.Count}\n" +
                $"Aktivnykh sozdannykh predmetov: {spawnedPickups.Count}";
        }

        private void OnRoundStarted()
        {
            if (active)
                Disable(removeSpawnedLoot: true);

            string eventName = EventManager.Instance?.Session?.EventName;
            if (!string.IsNullOrEmpty(eventName))
            {
                string lower = eventName.ToLowerInvariant();
                if (lower.Contains("cub") || lower.Contains("cube") ||
                    lower.Contains("kub") || lower.Contains("kyb") ||
                    lower.Contains("kubik"))
                {
                    Enable();
                }
            }
        }

        private void OnRoundEnded(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev)
        {
            if (active)
            {
                bool remove = Plugin.Instance?.Config.CubeRemoveLootOnRoundEnd ?? true;
                Disable(removeSpawnedLoot: remove);
            }
        }

        private void BuildRoomGraph()
        {
            graph.Clear();

            foreach (Room room in Room.List)
                graph[room] = new HashSet<Room>();

            foreach (Door door in Door.List)
            {
                if (door == null)
                    continue;

                try
                {
                    Vector3 forward = door.Transform.forward;
                    Vector3 center = door.Position;

                    Room a = Room.Get(center + forward * 2.2f);
                    Room b = Room.Get(center - forward * 2.2f);

                    if (a == null || b == null || a == b)
                        continue;

                    graph[a].Add(b);
                    graph[b].Add(a);
                }
                catch { }
            }

            foreach (var pair in graph)
                FileLog.Write($"[Cube] Graf: {pair.Key.Type}, soedineniy: {pair.Value.Count}");
        }

        private IEnumerator<float> ScanLoop()
        {
            while (active)
            {
                try
                {
                    HashSet<Room> occupied = GetOccupiedRooms();

                    foreach (Room currentRoom in occupied)
                    {
                        if (currentRoom == null)
                            continue;

                        TryGenerateRoom(currentRoom, "igrok voshyol v komnatu");

                        bool firstVisit = visitedRooms.Add(currentRoom);

                        if (!firstVisit)
                            continue;

                        FileLog.Write(
                            $"[Cube] Pervoe poseshchenie komnaty: {currentRoom.Type}");

                        foreach (Room target in GetRoomsAtExactDistance(
                            currentRoom, PreloadDistance))
                        {
                            TryGenerateRoom(
                                target,
                                $"predvaritelnyy spavn iz {currentRoom.Type}");
                        }
                    }
                }
                catch (Exception e)
                {
                    FileLog.WriteEx("[Cube] Oshibka tsikla komnat", e);
                }

                yield return Timing.WaitForSeconds(ScanInterval);
            }
        }

        private HashSet<Room> GetOccupiedRooms()
        {
            var result = new HashSet<Room>();

            foreach (Player player in Player.List)
            {
                if (player == null || !player.IsConnected)
                    continue;

                if (player.Role.Type == RoleTypeId.Spectator ||
                    player.Role.Type == RoleTypeId.Overwatch)
                    continue;

                Room room = Room.Get(player.Position);

                if (room != null)
                    result.Add(room);
            }

            return result;
        }

        private int GetCubePlayerCount()
        {
            int count = Player.List.Count(player =>
                player != null &&
                player.IsConnected &&
                player.Role.Type != RoleTypeId.Spectator &&
                player.Role.Type != RoleTypeId.Overwatch);

            return Math.Max(1, count);
        }

        private IEnumerable<Room> GetRoomsAtExactDistance(Room start, int distance)
        {
            if (start == null || distance < 1)
                yield break;

            var seen = new HashSet<Room> { start };
            var queue = new Queue<(Room Room, int Depth)>();
            queue.Enqueue((start, 0));

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();

                if (node.Depth == distance)
                {
                    yield return node.Room;
                    continue;
                }

                if (!graph.TryGetValue(node.Room, out HashSet<Room> neighbours))
                    continue;

                foreach (Room neighbour in neighbours)
                {
                    if (neighbour == null || !seen.Add(neighbour))
                        continue;

                    queue.Enqueue((neighbour, node.Depth + 1));
                }
            }
        }

        private void TryGenerateRoom(Room room, string reason)
        {
            if (!active || room == null)
                return;

            if (visitedRooms.Contains(room))
                return;

            if (!generatedRooms.Add(room))
                return;

            CubeRoomProfile profile = DetectProfile(room);

            if (profile == CubeRoomProfile.Surface)
            {
                if (surfaceGenerated)
                {
                    FileLog.Write($"[Cube] {room.Type}: poverkhnost uzhe obrabotana, propusk");
                    return;
                }
                surfaceGenerated = true;
            }

            int playerCount = GetCubePlayerCount();
            int requestedCount = RollItemCount(profile, playerCount);

            var record = new CubeRollRecord
            {
                RoomName = room.Type.ToString(),
                Profile = profile,
                PlayerCount = playerCount,
                ItemCount = requestedCount,
            };

            int spawnedCount = SpawnRoomLoot(room, profile, requestedCount, record);

            record.ItemCount = spawnedCount;

            if (spawnedCount > 0)
            {
                record.CountProbability =
                    CalculateItemCountProbability(playerCount, requestedCount, profile);
                record.CompositionProbability =
                    CalculateCategoryCompositionProbability(profile, record.Categories);

                currentKubLog.Add(record);
                CubeHistoryLog.Append(FormatHistoryLine(record));
            }

            int pointsCount = roomSpawnPoints.TryGetValue(room, out var pts) ? pts.Count : 0;

            FileLog.Write(
                $"[Cube] {room.Type}: zaplanirovano={requestedCount}, " +
                $"realno sozdano={spawnedCount}, " +
                $"bezopasnykh tochek={pointsCount}; " +
                $"profil={profile}; prichina={reason}");
        }

        private string FormatHistoryLine(CubeRollRecord record)
        {
            double chance = record.TotalProbability * 100d;

            string items = record.Items.Count == 0
                ? "nichego ne sozdano"
                : string.Join(", ", record.Items.Select(GetItemDisplayName));

            return
                $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] " +
                $"Kub #{kubSessionNumber}; " +
                $"komnata: {GetRussianRoomName(record.RoomName)}; " +
                $"profil: {GetRussianProfileName(record.Profile)}; " +
                $"predmetov: {record.ItemCount}; " +
                $"sostav: {DescribeCompositionRussian(record)}; " +
                $"shans kombinatsii: {chance:0.####}%; " +
                $"vypalo: {items}";
        }

        private CubeRoomProfile DetectProfile(Room room)
        {
            string type = room.Type.ToString().ToLowerInvariant();

            if (type.Contains("330"))
                return CubeRoomProfile.Scp330;

            if (type.Contains("914"))
                return CubeRoomProfile.Scp914;

            if (type.Contains("gr18") || type.Contains("glass"))
                return CubeRoomProfile.Gr18;

            if (IsArmory(room, type))
                return CubeRoomProfile.Armory;

            if (type.Contains("049"))
                return CubeRoomProfile.Scp049;

            if (IsOtherScpRoom(type))
                return CubeRoomProfile.OtherScpRoom;

            switch (room.Zone)
            {
                case ZoneType.LightContainment:
                    return CubeRoomProfile.ZoneLight;
                case ZoneType.HeavyContainment:
                    return CubeRoomProfile.ZoneHeavy;
                case ZoneType.Entrance:
                    return CubeRoomProfile.ZoneEntrance;
                case ZoneType.Surface:
                    return CubeRoomProfile.Surface;
                default:
                    return CubeRoomProfile.ZoneHeavy;
            }
        }

        private bool IsArmory(Room room, string typeLower)
        {
            if (typeLower.Contains("armory") || typeLower.Contains("armoury"))
                return true;

            if (room.Zone == ZoneType.LightContainment && typeLower.Contains("armory"))
                return true;

            if (room.Zone == ZoneType.HeavyContainment && typeLower.Contains("armory"))
                return true;

            if (typeLower.Contains("nuke") && typeLower.Contains("armory"))
                return true;

            if (typeLower.Contains("049") && typeLower.Contains("armory"))
                return true;

            return false;
        }

        private bool IsOtherScpRoom(string typeLower)
        {
            if (typeLower.Contains("scp") || typeLower.Contains("containment"))
            {
                if (typeLower.Contains("330") || typeLower.Contains("914") ||
                    typeLower.Contains("049") || typeLower.Contains("gr18"))
                    return false;

                return true;
            }

            return false;
        }

        private int RollSurfaceItemCount()
        {
            int count = 50;

            if (UnityEngine.Random.Range(0f, 100f) <= 50f)
                count = 100;

            if (UnityEngine.Random.Range(0f, 100f) <= 20f)
                count = 150;

            if (UnityEngine.Random.Range(0f, 100f) <= 5f)
                count = 200;

            return count;
        }

        private int RollItemCount(CubeRoomProfile profile, int players)
        {
            int minimum = Math.Max(1, Plugin.Instance?.Config.CubeMinimumItemsPerRoom ?? 2);
            int maximum = Math.Max(minimum, Plugin.Instance?.Config.CubeNormalMaximumItemsPerRoom ?? 8);

            if (profile == CubeRoomProfile.Surface)
                return RollSurfaceItemCount();

            CubeSpawnPlan plan = CubeLootTables.GetPlayerPlan(players);
            int count = plan.Guaranteed;

            foreach (double chance in plan.ExtraChances)
            {
                if (UnityEngine.Random.Range(0f, 100f) <= (float)chance)
                    count++;
            }

            if (profile == CubeRoomProfile.Scp914 &&
                UnityEngine.Random.Range(0f, 100f) <= 20f)
            {
                count++;
            }

            if (CurrentLuck > 0)
            {
                double startingChance = CurrentLuck / 10000d;
                int bonusNumber = 0;
                int maxWithLuck = Plugin.Instance?.Config.CubeMaximumItemsPerRoom ?? 100;

                while (count < maxWithLuck)
                {
                    double chance = startingChance - bonusNumber;
                    if (chance <= 0d) break;
                    if (UnityEngine.Random.Range(0f, 100f) > (float)chance) break;
                    count++;
                    bonusNumber++;
                }
            }

            if (count < minimum) count = minimum;
            if (count > 100) count = 100;
            if (count > maximum && CurrentLuck <= 0) count = maximum;

            return count;
        }

        private int SpawnRoomLoot(Room room, CubeRoomProfile profile, int count, CubeRollRecord record)
        {
            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                if (TrySpawnRandomItem(room, profile, i, record))
                    spawned++;
            }

            return spawned;
        }

        private bool TrySpawnRandomItem(Room room, CubeRoomProfile profile, int index, CubeRollRecord record)
        {
            if (!TryGetSpawnPoint(room, index, out Vector3 position))
            {
                FileLog.Write(
                    $"[Cube] OSHIBKA: {room.Type}: net bezopasnoy tochki. " +
                    "Predmet ne sozdan, chtoby ne popast za kartu.");
                return false;
            }

            for (int attempt = 1; attempt <= 10; attempt++)
            {
                CubeLootCategory category;
                string itemName = RollItem(profile, out category);

                if (!TrySpawn(itemName, position))
                    continue;

                record.Categories.Add(category);
                record.Items.Add(itemName);
                return true;
            }

            if (TrySpawn("Coin", position))
            {
                record.Categories.Add(CubeLootCategory.Utilities);
                record.Items.Add("Coin");
                return true;
            }

            return false;
        }

        private string RollItem(CubeRoomProfile profile, out CubeLootCategory category)
        {
            if (profile == CubeRoomProfile.Scp330)
            {
                string raw = RollScp330Item();
                category = CubeLootCategory.ScpItems;
                return raw;
            }

            if (!CubeLootTables.Categories.TryGetValue(profile, out var categoryList) ||
                categoryList.Count == 0)
            {
                category = CubeLootCategory.Utilities;
                return "Coin";
            }

            category = WeightedPick(categoryList);

            if (!CubeLootTables.Items.TryGetValue(category, out var itemList) ||
                itemList.Count == 0)
            {
                return "Coin";
            }

            return WeightedPick(itemList);
        }

        private string RollScp330Item()
        {
            string raw = WeightedPick(CubeLootTables.Scp330Items);

            if (raw == "RARE")
                return WeightedPick(CubeLootTables.RareItems);

            if (raw.StartsWith("CATEGORY:"))
            {
                string categoryName = raw.Substring(9);
                if (Enum.TryParse(categoryName, out CubeLootCategory cat) &&
                    CubeLootTables.Items.TryGetValue(cat, out var items) &&
                    items.Count > 0)
                    return WeightedPick(items);
                return null;
            }

            if (raw.StartsWith("CHOICE:"))
            {
                string options = raw.Substring(7);
                string[] parts = options.Split('|');
                return parts[UnityEngine.Random.Range(0, parts.Length)];
            }

            return raw;
        }

        private T WeightedPick<T>(IReadOnlyList<WeightedEntry<T>> entries)
        {
            double total = entries.Sum(e => Math.Max(0, e.Weight));

            if (total <= 0)
                return default;

            double roll = UnityEngine.Random.Range(0f, (float)total);
            double current = 0;

            foreach (WeightedEntry<T> entry in entries)
            {
                current += Math.Max(0, entry.Weight);

                if (roll <= current)
                    return entry.Value;
            }

            return entries[entries.Count - 1].Value;
        }

        private bool TryValidatePoint(Room expectedRoom, Vector3 origin, out Vector3 result)
        {
            result = default;

            if (!Physics.Raycast(
                origin + Vector3.up * 3f,
                Vector3.down,
                out RaycastHit hit,
                12f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            Vector3 point = hit.point + Vector3.up * 0.2f;

            Room actualRoom = Room.Get(point + Vector3.up * 0.5f);

            if (actualRoom == null || actualRoom != expectedRoom)
                return false;

            if (Vector3.Dot(hit.normal, Vector3.up) < 0.7f)
                return false;

            result = point;
            return true;
        }

        private List<Vector3> BuildSpawnPoints(Room room)
        {
            var points = new List<Vector3>();
            float spacing = Math.Max(0.5f, LootSpacing);

            for (int x = -6; x <= 6; x++)
            {
                for (int z = -6; z <= 6; z++)
                {
                    Vector3 origin =
                        room.Position +
                        room.Transform.right * (x * spacing) +
                        room.Transform.forward * (z * spacing) +
                        Vector3.up * 2f;

                    if (!TryValidatePoint(room, origin, out Vector3 valid))
                        continue;

                    if (points.Any(p => Vector3.Distance(p, valid) < spacing))
                        continue;

                    points.Add(valid);
                }
            }

            if (points.Count == 0 &&
                TryValidatePoint(room, room.Position + Vector3.up * 2f, out Vector3 center))
            {
                points.Add(center);
            }

            roomSpawnPoints[room] = points;

            FileLog.Write(
                $"[Cube] {room.Type}: naydeno bezopasnykh tochek: {points.Count}");

            return points;
        }

        private bool TryGetSpawnPoint(Room room, int index, out Vector3 position)
        {
            if (!roomSpawnPoints.TryGetValue(room, out List<Vector3> points))
                points = BuildSpawnPoints(room);

            if (points.Count == 0)
            {
                position = default;
                return false;
            }

            int selected = (index + UnityEngine.Random.Range(0, points.Count)) % points.Count;
            position = points[selected];
            return true;
        }

        private bool TrySpawn(string configuredName, Vector3 position)
        {
            if (!TryResolveItemType(configuredName, out ItemType type))
            {
                FileLog.Write($"[Cube] Predmet otsutstvuet v etoy sborke: {configuredName}");
                return false;
            }

            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    Pickup pickup = Pickup.CreateAndSpawn(
                        type, position, Quaternion.identity);

                    if (pickup == null)
                        return false;

                    spawnedPickups.Add(pickup);
                    return true;
                }
                catch (Exception e)
                {
                    FileLog.WriteEx($"[Cube] Ne udalos sozdat {configuredName} (popytka {attempt + 1})", e);
                }
            }

            try
            {
                Pickup fallback = Pickup.CreateAndSpawn(ItemType.Coin, position, Quaternion.identity);
                if (fallback != null)
                {
                    spawnedPickups.Add(fallback);
                    return true;
                }
            }
            catch { }

            return false;
        }

        private bool TryResolveItemType(string name, out ItemType type)
        {
            if (Enum.TryParse(name, true, out type) &&
                Enum.IsDefined(typeof(ItemType), type))
                return true;

            if (ItemAliases.TryGetValue(name, out var aliases))
            {
                foreach (string alias in aliases)
                {
                    if (Enum.TryParse(alias, true, out type) &&
                        Enum.IsDefined(typeof(ItemType), type))
                        return true;
                }
            }

            type = ItemType.None;
            return false;
        }

        private Dictionary<int, double> BuildCountDistribution(CubeRoomProfile profile, int players)
        {
            CubeSpawnPlan plan = CubeLootTables.GetPlayerPlan(players);

            var probabilities = new Dictionary<int, double>
            {
                [plan.Guaranteed] = 1d
            };

            foreach (double chancePercent in plan.ExtraChances)
            {
                double chance = chancePercent / 100d;
                var next = new Dictionary<int, double>();

                foreach (var pair in probabilities)
                {
                    AddProbability(next, pair.Key, pair.Value * (1d - chance));
                    AddProbability(next, pair.Key + 1, pair.Value * chance);
                }

                probabilities = next;
            }

            if (profile == CubeRoomProfile.Scp914)
            {
                const double extra914 = 0.20d;
                var next = new Dictionary<int, double>();

                foreach (var pair in probabilities)
                {
                    AddProbability(next, pair.Key, pair.Value * (1d - extra914));
                    AddProbability(next, Math.Min(6, pair.Key + 1), pair.Value * extra914);
                }

                probabilities = next;
            }

            return probabilities;
        }

        private static void AddProbability(Dictionary<int, double> values, int key, double probability)
        {
            if (values.ContainsKey(key))
                values[key] += probability;
            else
                values[key] = probability;
        }

        private double CalculateItemCountProbability(int players, int itemCount, CubeRoomProfile profile)
        {
            Dictionary<int, double> distribution = BuildCountDistribution(profile, players);
            return distribution.TryGetValue(itemCount, out double probability) ? probability : 0d;
        }

        private double CalculateCategoryCompositionProbability(
            CubeRoomProfile profile,
            IReadOnlyList<CubeLootCategory> rolledCategories)
        {
            if (rolledCategories == null || rolledCategories.Count == 0)
                return 0d;

            if (!CubeLootTables.Categories.TryGetValue(
                profile,
                out List<WeightedEntry<CubeLootCategory>> table))
            {
                return 1d;
            }

            double totalWeight = table.Sum(x => Math.Max(0d, x.Weight));

            if (totalWeight <= 0)
                return 0d;

            Dictionary<CubeLootCategory, int> counts =
                rolledCategories
                    .GroupBy(x => x)
                    .ToDictionary(x => x.Key, x => x.Count());

            double probability = Factorial(rolledCategories.Count);

            foreach (var pair in counts)
            {
                WeightedEntry<CubeLootCategory> entry =
                    table.FirstOrDefault(x => EqualityComparer<CubeLootCategory>
                        .Default.Equals(x.Value, pair.Key));

                if (entry == null)
                    return 0d;

                double categoryProbability = entry.Weight / totalWeight;

                probability /= Factorial(pair.Value);
                probability *= Math.Pow(categoryProbability, pair.Value);
            }

            return probability;
        }

        private static double Factorial(int value)
        {
            double result = 1d;

            for (int i = 2; i <= value; i++)
                result *= i;

            return result;
        }

        private static string ChanceColor(double percent)
        {
            if (percent >= 20d)  return "#55FF55";
            if (percent >= 10d)  return "#AAFF55";
            if (percent >= 5d)   return "#FFFF55";
            if (percent >= 2d)   return "#FFAA33";
            if (percent >= 1d)   return "#FF6633";
            if (percent >= 0.1d) return "#FF3333";
            return "#AA0000";
        }

        private static string GetRussianCategoryName(CubeLootCategory category, int count)
        {
            switch (category)
            {
                case CubeLootCategory.Keycards:
                    return count == 1 ? "klyuch-karta" : "klyuch-karty";
                case CubeLootCategory.Utilities:
                    return count == 1 ? "utilit" : "utility";
                case CubeLootCategory.Medicine:
                    return count == 1 ? "meditsina" : "meditsiny";
                case CubeLootCategory.Armor:
                    return count == 1 ? "bronya" : "broni";
                case CubeLootCategory.Ammo:
                    return count == 1 ? "boepripas" : "boepripasy";
                case CubeLootCategory.Grenades:
                    return count == 1 ? "granata" : "granaty";
                case CubeLootCategory.Weapons:
                    return count == 1 ? "oruzhie" : "oruzhiya";
                case CubeLootCategory.ScpItems:
                    return count == 1 ? "SCP-predmet" : "SCP-predmeta";
                default:
                    return category.ToString();
            }
        }

        private static string DescribeCompositionRussian(CubeRollRecord record)
        {
            if (record.Categories.Count == 0)
                return "predmety ne sozdany";

            return string.Join(
                ", ",
                record.Categories
                    .GroupBy(category => category)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key)
                    .Select(group =>
                    {
                        int cnt = group.Count();
                        return $"{cnt} {GetRussianCategoryName(group.Key, cnt)}";
                    }));
        }

        public void SetLuck(int value)
        {
            CurrentLuck = Math.Max(0, Math.Min(1000000, value));

            CubeHistoryLog.Append(
                $"Udacha izmenena: {CurrentLuck}/1000000");
        }

        public bool QueueItem(int id, out string response)
        {
            if (id < 0 || id > 76 ||
                !Enum.IsDefined(typeof(ItemType), (ItemType)id))
            {
                response = "Predmet s takim ID ne nayden.";
                return false;
            }

            if (nextRoomItems.Count >= 100)
            {
                response = "V sleduyushchuyu komnatu uzhe dobavleno 100 predmetov.";
                return false;
            }

            ItemType type = (ItemType)id;
            nextRoomItems.Add(type);

            response = $"Dobavleno: {GetItemDisplayName(type.ToString())}. Predmet poyavitsya v sleduyushchey komnate.";
            return true;
        }

        private double GetLuckFactor()
        {
            double luck = Math.Max(1, Math.Min(1000000, CurrentLuck));
            return (luck - 1d) / 999999d;
        }

        private double GetItemWeight(WeightedEntry<string> entry)
        {
            double luck01 = GetLuckFactor();
            double mult = 1d + luck01 * entry.LuckTier * 1.5d;
            return Math.Max(0.0001d, entry.Weight * mult);
        }

        public string BuildItemChanceList(int? section = null)
        {
            var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in CubeLootTables.Items)
            {
                CubeLootCategory category = pair.Key;

                if (section.HasValue && CategoryToSection(category) != section.Value)
                    continue;

                foreach (var entry in pair.Value)
                {
                    string item = entry.Value;
                    double w = GetItemWeight(entry);

                    if (!map.ContainsKey(item))
                        map[item] = 0d;

                    map[item] += w;
                }
            }

            double total = map.Values.Sum();
            if (total <= 0d)
                return "Predmetov net.";

            var sorted = map
                .Select(x => new
                {
                    Name = GetItemDisplayName(x.Key),
                    Chance = x.Value / total * 100d
                })
                .OrderByDescending(x => x.Chance)
                .ThenBy(x => x.Name)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Udacha: {CurrentLuck}");
            sb.AppendLine();

            int i = 1;
            foreach (var row in sorted)
            {
                sb.AppendLine($"{i}. {row.Name} - {row.Chance:0.##}%");
                i++;
            }

            return sb.ToString();
        }

        private static int CategoryToSection(CubeLootCategory category)
        {
            switch (category)
            {
                case CubeLootCategory.Keycards: return 0;
                case CubeLootCategory.Utilities: return 1;
                case CubeLootCategory.Medicine: return 2;
                case CubeLootCategory.Armor: return 3;
                case CubeLootCategory.Ammo: return 4;
                case CubeLootCategory.Grenades: return 5;
                case CubeLootCategory.Weapons: return 6;
                case CubeLootCategory.ScpItems: return 7;
                default: return 8;
            }
        }

        public string BuildItemSections()
        {
            return
                "0. Klyuch-karty\n" +
                "1. Poleznoe\n" +
                "2. Meditsina\n" +
                "3. Bronya\n" +
                "4. Patrony\n" +
                "5. Granaty\n" +
                "6. Oruzhie\n" +
                "7. SCP predmety\n" +
                "8. Ostalnoe\n" +
                "Napishi kub list 6, chtoby posmotret oruzhie.";
        }

        public string BuildItemList(int section)
        {
            List<KubItemInfo> items = GetKubItems()
                .Where(item => item.Section == section)
                .ToList();

            if (items.Count == 0)
                return "V etom razdele predmetov net.";

            var builder = new StringBuilder();

            foreach (KubItemInfo item in items)
            {
                builder.AppendLine($"{item.Id}. {item.Name}");
            }

            builder.AppendLine();
            builder.AppendLine("Chtoby dobavit predmet v sleduyushchuyu komnatu: kub item ID");

            return builder.ToString();
        }

        private List<KubItemInfo> GetKubItems()
        {
            var result = new List<KubItemInfo>();

            foreach (ItemType type in Enum.GetValues(typeof(ItemType)))
            {
                int id = (int)type;

                if (id < 0 || id > 76)
                    continue;

                result.Add(new KubItemInfo
                {
                    Id = id,
                    Type = type,
                    Name = GetItemDisplayName(type.ToString()),
                    Section = GetItemSection(type),
                });
            }

            return result
                .OrderBy(item => item.Id)
                .ToList();
        }

        private int GetItemSection(ItemType type)
        {
            string name = type.ToString();

            if (name.StartsWith("Keycard"))
                return 0;

            if (name == "Radio" || name == "Flashlight" || name == "Coin" || name == "Lantern")
                return 1;

            if (name == "Medkit" || name == "Adrenaline" || name == "Painkillers")
                return 2;

            if (name.StartsWith("Armor"))
                return 3;

            if (name.StartsWith("Ammo"))
                return 4;

            if (name.StartsWith("Grenade"))
                return 5;

            if (name.StartsWith("Gun") || name == "MicroHID" || name == "ParticleDisruptor" || name == "Jailbird")
                return 6;

            if (name.StartsWith("SCP") || name.StartsWith("AntiSCP"))
                return 7;

            return 8;
        }

        public string BuildCurrentKubLog()
        {
            if (currentKubLog.Count == 0)
                return "V etom zapuske Kuba predmety poka ne poyavilis.";

            var builder = new StringBuilder();
            int number = 1;

            foreach (CubeRollRecord record in currentKubLog)
            {
                string room = GetRussianRoomName(record.RoomName);

                string items = record.Items.Count > 0
                    ? string.Join(", ", record.Items.Select(GetItemDisplayName))
                    : "nichego";

                double chance = record.TotalProbability * 100d;

                builder.AppendLine(
                    $"{number}. {room}, predmety: {items}. Shans: {chance:0.####}%");

                number++;
            }

            return builder.ToString();
        }
    }
}