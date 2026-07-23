using PlayerStatsSystem;

namespace EventHUD.Medicine
{
    public static class BodyPartExtensions
    {
        /// <summary>
        /// Сокращённое название части тела для HUD.
        /// </summary>
        public static string GetShortName(this BodyPart part) => part switch
        {
            BodyPart.Head      => "гол.",
            BodyPart.Neck      => "шея",
            BodyPart.Chest     => "груд.",
            BodyPart.Abdomen   => "жив.",
            BodyPart.Pelvis    => "таз",
            BodyPart.LeftArm   => "л.р.",
            BodyPart.RightArm  => "пр.р.",
            BodyPart.LeftHand  => "л.лад.",
            BodyPart.RightHand => "пр.лад.",
            BodyPart.LeftLeg   => "л.н.",
            BodyPart.RightLeg  => "пр.н.",
            _                  => "?"
        };

        /// <summary>
        /// Полное название части тела.
        /// </summary>
        public static string GetFullName(this BodyPart part) => part switch
        {
            BodyPart.Head      => "Голова",
            BodyPart.Neck      => "Шея",
            BodyPart.Chest     => "Грудь",
            BodyPart.Abdomen   => "Живот",
            BodyPart.Pelvis    => "Таз",
            BodyPart.LeftArm   => "Левая рука",
            BodyPart.RightArm  => "Правая рука",
            BodyPart.LeftHand  => "Левая ладонь",
            BodyPart.RightHand => "Правая ладонь",
            BodyPart.LeftLeg   => "Левая нога",
            BodyPart.RightLeg  => "Правая нога",
            _                  => "?"
        };

        /// <summary>
        /// Является ли часть тела жизненно важной (для определения артериального кровотечения).
        /// </summary>
        public static bool IsVital(this BodyPart part) => part switch
        {
            BodyPart.Head    => true,
            BodyPart.Neck    => true,
            BodyPart.Chest   => true,
            BodyPart.Abdomen => true,
            _                => false
        };

        /// <summary>
        /// Конвертация HitboxType из игры в наш BodyPart.
        /// HitboxType: Headshot, Body, Limb — грубое деление.
        /// Для более точного определения используем рандом по зонам.
        /// </summary>
        public static BodyPart FromHitbox(HitboxType hitbox)
        {
            switch (hitbox)
            {
                case HitboxType.Headshot:
                    // Голова или шея — 70/30
                    return UnityEngine.Random.value < 0.7f ? BodyPart.Head : BodyPart.Neck;

                case HitboxType.Body:
                    // Грудь 40%, Живот 30%, Таз 15%, Шея 15%
                    float bodyRoll = UnityEngine.Random.value;
                    if (bodyRoll < 0.40f) return BodyPart.Chest;
                    if (bodyRoll < 0.70f) return BodyPart.Abdomen;
                    if (bodyRoll < 0.85f) return BodyPart.Pelvis;
                    return BodyPart.Neck;

                case HitboxType.Limb:
                default:
                    // Руки 40%, Ноги 40%, Ладони 20%
                    float limbRoll = UnityEngine.Random.value;
                    if (limbRoll < 0.20f) return BodyPart.LeftArm;
                    if (limbRoll < 0.40f) return BodyPart.RightArm;
                    if (limbRoll < 0.50f) return BodyPart.LeftHand;
                    if (limbRoll < 0.60f) return BodyPart.RightHand;
                    if (limbRoll < 0.80f) return BodyPart.LeftLeg;
                    return BodyPart.RightLeg;
            }
        }
    }
}
 