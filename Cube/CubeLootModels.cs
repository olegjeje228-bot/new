using System;
using System.Collections.Generic;

namespace EventHUD.Cube
{
    public enum CubeLootCategory
    {
        Keycards,
        Utilities,
        Medicine,
        Armor,
        Ammo,
        Grenades,
        Weapons,
        ScpItems,
    }

    public enum CubeRoomProfile
    {
        ZoneLight,
        ZoneHeavy,
        ZoneEntrance,
        Surface,

        Gr18,
        Scp330,
        Scp914,
        Armory,
        Scp049,
        OtherScpRoom,
    }

    public sealed class WeightedEntry<T>
    {
        public T Value;
        public double Weight;
        public int LuckTier;

        public WeightedEntry(T value, double weight, int luckTier = 0)
        {
            Value = value;
            Weight = weight;
            LuckTier = luckTier;
        }
    }

    public sealed class CubeSpawnPlan
    {
        public int Guaranteed;
        public List<double> ExtraChances = new List<double>();
    }

    public sealed class CubeRollRecord
    {
        public string RoomName;
        public CubeRoomProfile Profile;

        public int PlayerCount;
        public int ItemCount;

        public readonly List<CubeLootCategory> Categories =
            new List<CubeLootCategory>();

        public readonly List<string> Items =
            new List<string>();

        public double CountProbability;
        public double CompositionProbability;

        public double TotalProbability =>
            CountProbability * CompositionProbability;
    }

    public sealed class KubItemInfo
    {
        public int Id;
        public ItemType Type;
        public string Name;
        public int Section;
    }
}