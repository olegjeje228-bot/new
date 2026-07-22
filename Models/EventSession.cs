using System;
using EventHUD.Enums;

namespace EventHUD.Models
{
    public class EventSession
    {
        public EventState State { get; set; } = EventState.None;

        public string  EventName    { get; set; }
        public RPType  RpType       { get; set; }
        public string  HostUserId   { get; set; }
        public string  HostNickname { get; set; }
        public bool    HostIsOnline { get; set; } = true;

        public DateTime StartedAt  { get; set; }
        public DateTime StoppedAt  { get; set; }

        public DateTime HostReturnedAt          { get; set; }
        public bool     ShowHostReturnHighlight  { get; set; }

        public TimeSpan Elapsed =>
            StartedAt == default ? TimeSpan.Zero : DateTime.UtcNow - StartedAt;

        public void Reset()
        {
            State                   = EventState.None;
            EventName               = null;
            HostUserId              = null;
            HostNickname            = null;
            HostIsOnline            = true;
            StartedAt               = default;
            StoppedAt               = default;
            HostReturnedAt          = default;
            ShowHostReturnHighlight = false;
        }
    }
}
 