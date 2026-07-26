using System;
using System.Collections.Generic;
using EventHUD.Enums;
using MEC;

namespace EventHUD.Hud
{
    public class EffectScheduler
    {
        public EffectState State { get; } = new EffectState();

        private readonly Config _config;
        private CoroutineHandle _minutePulseHandle;
        private CoroutineHandle _flashHandle;
        private CoroutineHandle _hostReturnHandle;

        public EffectScheduler(Config config) => _config = config;

        public void Start()
        {
            _minutePulseHandle = Timing.RunCoroutine(MinutePulseLoop());
            _flashHandle       = Timing.RunCoroutine(FlashLoop());
            _hostReturnHandle  = Timing.RunCoroutine(HostReturnLoop());
        }

        public void Stop() =>
            Timing.KillCoroutines(_minutePulseHandle, _flashHandle, _hostReturnHandle);

        private IEnumerator<float> MinutePulseLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(_config.MinutePulseIntervalSeconds);

                if (EventManager.Instance?.Session?.State == EventState.None)
                    continue;

                if (State.IsFlashActive)
                    continue;

                yield return Timing.WaitUntilDone(RunPulseCycle(
                    _config.MinutePulseDuration,
                    active   => State.IsMinutePulseActive = active,
                    progress => State.MinutePulseProgress = progress));
            }
        }

        private IEnumerator<float> FlashLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(_config.ColorFlashIntervalSeconds);

                if (EventManager.Instance?.Session?.State != EventState.Running)
                    continue;

                yield return Timing.WaitUntilDone(RunPulseCycle(
                    _config.ColorFlashDuration,
                    active   => State.IsFlashActive  = active,
                    progress => State.FlashProgress  = progress));
            }
        }

        private IEnumerator<float> RunPulseCycle(
            float         duration,
            Action<bool>  setActive,
            Action<float> setProgress)
        {
            setActive(true);

            float stepInterval = duration / Math.Max(1, _config.EffectColorSteps);
            float half         = duration / 2f;
            float elapsed      = 0f;

            // Нарастание
            while (elapsed < half)
            {
                setProgress(EasingUtils.SmoothStep(elapsed / half));
                elapsed += stepInterval;
                yield return Timing.WaitForSeconds(stepInterval);
            }

            elapsed = 0f;

            // Спад
            while (elapsed < half)
            {
                setProgress(EasingUtils.SmoothStep(1f - elapsed / half));
                elapsed += stepInterval;
                yield return Timing.WaitForSeconds(stepInterval);
            }

            setProgress(0f);
            setActive(false);
        }

        private IEnumerator<float> HostReturnLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(0.5f);

                var session = EventManager.Instance.Session;

                if (session.ShowHostReturnHighlight &&
                    (DateTime.UtcNow - session.HostReturnedAt).TotalSeconds
                    >= _config.HostReturnHighlightDuration)
                {
                    session.ShowHostReturnHighlight = false;
                }
            }
        }
    }
}
 