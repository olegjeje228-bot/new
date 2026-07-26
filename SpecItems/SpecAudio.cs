namespace EventHUD.SpecItems
{
    using UnityEngine;

    public static class SpecAudio
    {
        public static void PlayAt(Vector3 position, string fileName, float volume, float range)
        {
            string clip = System.IO.Path.GetFileNameWithoutExtension(fileName);
            EventHUD.Audio.SoundService.PlayAt(position, clip, volume, 1f, range);
        }
    }
}