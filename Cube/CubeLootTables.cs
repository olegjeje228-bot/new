using System.Collections.Generic;

namespace EventHUD.Cube
{
    public static class CubeLootTables
    {
        public static readonly Dictionary<CubeRoomProfile,
            List<WeightedEntry<CubeLootCategory>>> Categories =
            new Dictionary<CubeRoomProfile, List<WeightedEntry<CubeLootCategory>>>
        {
            [CubeRoomProfile.ZoneLight] = CategoriesOf(
                (CubeLootCategory.Keycards, 18),
                (CubeLootCategory.Utilities, 30),
                (CubeLootCategory.Medicine, 24),
                (CubeLootCategory.Armor, 5),
                (CubeLootCategory.Ammo, 10),
                (CubeLootCategory.Grenades, 2),
                (CubeLootCategory.Weapons, 3),
                (CubeLootCategory.ScpItems, 8)),

            [CubeRoomProfile.ZoneHeavy] = CategoriesOf(
                (CubeLootCategory.Keycards, 10),
                (CubeLootCategory.Utilities, 12),
                (CubeLootCategory.Medicine, 22),
                (CubeLootCategory.Armor, 15),
                (CubeLootCategory.Ammo, 15),
                (CubeLootCategory.Grenades, 6),
                (CubeLootCategory.Weapons, 14),
                (CubeLootCategory.ScpItems, 6)),

            [CubeRoomProfile.ZoneEntrance] = CategoriesOf(
                (CubeLootCategory.Keycards, 14),
                (CubeLootCategory.Utilities, 18),
                (CubeLootCategory.Medicine, 20),
                (CubeLootCategory.Armor, 14),
                (CubeLootCategory.Ammo, 12),
                (CubeLootCategory.Grenades, 7),
                (CubeLootCategory.Weapons, 12),
                (CubeLootCategory.ScpItems, 3)),

            [CubeRoomProfile.Surface] = CategoriesOf(
                (CubeLootCategory.Keycards, 4),
                (CubeLootCategory.Utilities, 6),
                (CubeLootCategory.Medicine, 50),
                (CubeLootCategory.Armor, 12),
                (CubeLootCategory.Ammo, 8),
                (CubeLootCategory.Grenades, 4),
                (CubeLootCategory.Weapons, 14),
                (CubeLootCategory.ScpItems, 2)),

            [CubeRoomProfile.Gr18] = CategoriesOf(
                (CubeLootCategory.Medicine, 40),
                (CubeLootCategory.Utilities, 25),
                (CubeLootCategory.Keycards, 15),
                (CubeLootCategory.ScpItems, 12),
                (CubeLootCategory.Weapons, 5),
                (CubeLootCategory.Armor, 3)),

            [CubeRoomProfile.Scp914] = CategoriesOf(
                (CubeLootCategory.Keycards, 35),
                (CubeLootCategory.Utilities, 25),
                (CubeLootCategory.Medicine, 20),
                (CubeLootCategory.Armor, 10),
                (CubeLootCategory.ScpItems, 8),
                (CubeLootCategory.Weapons, 2)),

            [CubeRoomProfile.Armory] = CategoriesOf(
                (CubeLootCategory.Weapons, 35),
                (CubeLootCategory.Ammo, 30),
                (CubeLootCategory.Armor, 20),
                (CubeLootCategory.Grenades, 10),
                (CubeLootCategory.Medicine, 5)),

            [CubeRoomProfile.Scp049] = CategoriesOf(
                (CubeLootCategory.Medicine, 40),
                (CubeLootCategory.Armor, 20),
                (CubeLootCategory.Ammo, 15),
                (CubeLootCategory.Weapons, 10),
                (CubeLootCategory.Utilities, 8),
                (CubeLootCategory.Keycards, 5),
                (CubeLootCategory.ScpItems, 2)),

            [CubeRoomProfile.OtherScpRoom] = CategoriesOf(
                (CubeLootCategory.Medicine, 30),
                (CubeLootCategory.Utilities, 20),
                (CubeLootCategory.Armor, 15),
                (CubeLootCategory.Ammo, 12),
                (CubeLootCategory.Weapons, 10),
                (CubeLootCategory.ScpItems, 8),
                (CubeLootCategory.Keycards, 5)),
        };

        public static readonly Dictionary<CubeLootCategory,
            List<WeightedEntry<string>>> Items =
            new Dictionary<CubeLootCategory, List<WeightedEntry<string>>>
        {
            [CubeLootCategory.Keycards] = ItemsOf(
                ("KeycardJanitor", 10),
                ("KeycardScientist", 25),
                ("KeycardResearchCoordinator", 20),
                ("KeycardZoneManager", 20),
                ("KeycardGuard", 7.5),
                ("KeycardMTFPrivate", 9.1),
                ("KeycardContainmentEngineer", 3),
                ("KeycardMTFOperative", 2),
                ("KeycardMTFCaptain", 1),
                ("KeycardFacilityManager", 0.7),
                ("KeycardChaosInsurgency", 0.4),
                ("KeycardO5", 0.3),
                ("SurfaceAccessPass", 1)),

            [CubeLootCategory.Utilities] = ItemsOf(
                ("Radio", 30),
                ("Flashlight", 35),
                ("Coin", 30),
                ("Lantern", 5)),

            [CubeLootCategory.Medicine] = ItemsOf(
                ("Medkit", 45),
                ("Adrenaline", 20),
                ("Painkillers", 35)),

            [CubeLootCategory.Armor] = ItemsOf(
                ("ArmorLight", 55),
                ("ArmorCombat", 32),
                ("ArmorHeavy", 13)),

            [CubeLootCategory.Ammo] = ItemsOf(
                ("Ammo12gauge", 18),
                ("Ammo556x45", 22),
                ("Ammo44cal", 12),
                ("Ammo762x39", 20),
                ("Ammo9x19", 28)),

            [CubeLootCategory.Grenades] = ItemsOf(
                ("GrenadeHE", 45),
                ("GrenadeFlash", 55)),

            [CubeLootCategory.Weapons] = ItemsOf(
                ("GunCOM15", 17),
                ("GunCOM18", 13),
                ("GunCOM45", 2),
                ("GunE11SR", 7),
                ("GunCrossvec", 10),
                ("GunFSP9", 12),
                ("GunLogicer", 6),
                ("GunRevolver", 8),
                ("GunAK", 6),
                ("GunShotgun", 8),
                ("MicroHID", 1.2),
                ("ParticleDisruptor", 0.5),
                ("Jailbird", 0.8),
                ("GunFRMG0", 3),
                ("GunA7", 2.5),
                ("SCP127", 3)),

            [CubeLootCategory.ScpItems] = ItemsOf(
                ("SCP500", 22),
                ("SCP207", 15),
                ("AntiSCP207", 8),
                ("SCP018", 10),
                ("SCP268", 8),
                ("SCP330", 10),
                ("SCP2176", 8),
                ("SCP244a", 4),
                ("SCP244b", 4),
                ("SCP1853", 5),
                ("SCP1576", 3),
                ("SCP1344", 3)),
        };

        public static readonly List<WeightedEntry<string>> Scp330Items = ItemsOf(
            ("CATEGORY:Medicine", 45),
            ("Coin", 20),
            ("SCP500", 10),
            ("CHOICE:SCP207|AntiSCP207", 10),
            ("CHOICE:Flashlight|Radio", 10),
            ("RARE", 5));

        public static readonly List<WeightedEntry<string>> RareItems = ItemsOf(
            ("MicroHID", 20),
            ("ParticleDisruptor", 10),
            ("Jailbird", 15),
            ("GunCOM45", 15),
            ("GunA7", 15),
            ("SCP127", 15),
            ("SCP500", 10));

        public static CubeSpawnPlan GetPlayerPlan(int players)
        {
            if (players <= 3)
            {
                return new CubeSpawnPlan
                {
                    Guaranteed = 2,
                    ExtraChances = { 70, 40, 20 },
                };
            }

            if (players <= 6)
            {
                return new CubeSpawnPlan
                {
                    Guaranteed = 3,
                    ExtraChances = { 70, 45, 25, 10 },
                };
            }

            return new CubeSpawnPlan
            {
                Guaranteed = 4,
                ExtraChances = { 70, 50, 30, 15 },
            };
        }

        private static List<WeightedEntry<CubeLootCategory>> CategoriesOf(
            params (CubeLootCategory, double)[] entries)
        {
            var result = new List<WeightedEntry<CubeLootCategory>>();
            foreach (var entry in entries)
                result.Add(new WeightedEntry<CubeLootCategory>(entry.Item1, entry.Item2));
            return result;
        }

        private static List<WeightedEntry<string>> ItemsOf(
            params (string, double)[] entries)
        {
            var result = new List<WeightedEntry<string>>();
            foreach (var entry in entries)
                result.Add(new WeightedEntry<string>(entry.Item1, entry.Item2));
            return result;
        }
    }
}