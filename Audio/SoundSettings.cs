using System.ComponentModel;

namespace EventHUD.Audio
{
    public class SoundSettings
    {
        [Description("Включён ли звук")]
        public bool Enabled { get; set; } = true;

        [Description("true = объёмный 3D-звук из точки события, false = глобально на весь сервер")]
        public bool IsSpatial { get; set; } = true;

        [Description("Громкость (1 = 100%)")]
        public float Volume { get; set; } = 1f;

        [Description("Дистанция полной громкости (только для is_spatial)")]
        public float MinDistance { get; set; } = 3f;

        [Description("Дистанция слышимости (только для is_spatial)")]
        public float MaxDistance { get; set; } = 25f;

        [Description("Смещение от точки события; 0 0 0 = прямо на месте")]
        public float OffsetX { get; set; } = 0f;
        public float OffsetY { get; set; } = 0f;
        public float OffsetZ { get; set; } = 0f;
    }
}