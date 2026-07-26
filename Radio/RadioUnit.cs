using UnityEngine;

namespace EventHUD.Radio
{
    public sealed class RadioUnit
    {
        public int Number;
        public Vector3 Position;
        public string RoomName;
        public object Schematic;
        public bool IsOn;
        public bool Disabled;
        public int Volume = 1;
        public int BatteryLevel = 1;
        public float BatteryLeft = 100f;
        public string CurrentTrack = "";
        public string PlayerName = "";
        public object AudioHandle;   // ссылка на созданный AudioPlayer
    }
}