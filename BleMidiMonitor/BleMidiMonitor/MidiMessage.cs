using System;

namespace BleMidiMonitor
{
    public class MidiMessage
    {
        public DateTime Timestamp { get; set; }
        public string MessageType { get; set; }
        public int Channel { get; set; }
        public int Data1 { get; set; }
        public int Data2 { get; set; }
        public string FormattedMessage { get; set; }

        public MidiMessage()
        {
            Timestamp = DateTime.Now;
            MessageType = string.Empty;
            FormattedMessage = string.Empty;
        }
    }
}
