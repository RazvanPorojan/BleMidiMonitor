using System;
using System.Collections.Generic;

namespace BleMidiMonitor
{
    public static class MidiMessageParser
    {
        private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        public static List<MidiMessage> ParseBleMidiPacket(byte[] data)
        {
            var messages = new List<MidiMessage>();

            if (data == null || data.Length < 3)
            {
                return messages;
            }

            try
            {
                // BLE MIDI packet format: [Header][Timestamp][MIDI Status][Data...]
                // Header byte (index 0) contains timing info
                // Timestamp byte (index 1) contains low 7 bits of timestamp

                int index = 2; // Start after header and timestamp

                while (index < data.Length)
                {
                    // Check if this is a status byte (MSB = 1)
                    if ((data[index] & 0x80) == 0x80)
                    {
                        byte status = data[index];
                        byte messageType = (byte)(status & 0xF0);
                        byte channel = (byte)((status & 0x0F) + 1); // Channels 1-16

                        index++;

                        if (index >= data.Length)
                            break;

                        var message = new MidiMessage
                        {
                            Timestamp = DateTime.Now,
                            Channel = channel
                        };

                        switch (messageType)
                        {
                            case 0x80: // Note Off
                                if (index + 1 < data.Length)
                                {
                                    message.MessageType = "Note Off";
                                    message.Data1 = data[index];
                                    message.Data2 = data[index + 1];
                                    message.FormattedMessage = $"Note Off: Ch{channel} Note={GetNoteName(message.Data1)}({message.Data1}) Vel={message.Data2}";
                                    index += 2;
                                }
                                break;

                            case 0x90: // Note On
                                if (index + 1 < data.Length)
                                {
                                    message.MessageType = "Note On";
                                    message.Data1 = data[index];
                                    message.Data2 = data[index + 1];

                                    // Note On with velocity 0 is actually Note Off
                                    if (message.Data2 == 0)
                                    {
                                        message.MessageType = "Note Off";
                                        message.FormattedMessage = $"Note Off: Ch{channel} Note={GetNoteName(message.Data1)}({message.Data1}) Vel=0";
                                    }
                                    else
                                    {
                                        message.FormattedMessage = $"Note On: Ch{channel} Note={GetNoteName(message.Data1)}({message.Data1}) Vel={message.Data2}";
                                    }
                                    index += 2;
                                }
                                break;

                            case 0xA0: // Polyphonic Key Pressure (Aftertouch)
                                if (index + 1 < data.Length)
                                {
                                    message.MessageType = "Aftertouch";
                                    message.Data1 = data[index];
                                    message.Data2 = data[index + 1];
                                    message.FormattedMessage = $"Aftertouch: Ch{channel} Note={GetNoteName(message.Data1)}({message.Data1}) Pressure={message.Data2}";
                                    index += 2;
                                }
                                break;

                            case 0xB0: // Control Change
                                if (index + 1 < data.Length)
                                {
                                    message.MessageType = "Control Change";
                                    message.Data1 = data[index];
                                    message.Data2 = data[index + 1];
                                    message.FormattedMessage = $"CC: Ch{channel} Controller={message.Data1} Value={message.Data2}";
                                    index += 2;
                                }
                                break;

                            case 0xC0: // Program Change
                                if (index < data.Length)
                                {
                                    message.MessageType = "Program Change";
                                    message.Data1 = data[index];
                                    message.Data2 = 0;
                                    message.FormattedMessage = $"Program Change: Ch{channel} Program={message.Data1}";
                                    index++;
                                }
                                break;

                            case 0xD0: // Channel Pressure (Aftertouch)
                                if (index < data.Length)
                                {
                                    message.MessageType = "Channel Pressure";
                                    message.Data1 = data[index];
                                    message.Data2 = 0;
                                    message.FormattedMessage = $"Channel Pressure: Ch{channel} Pressure={message.Data1}";
                                    index++;
                                }
                                break;

                            case 0xE0: // Pitch Bend
                                if (index + 1 < data.Length)
                                {
                                    message.MessageType = "Pitch Bend";
                                    message.Data1 = data[index];
                                    message.Data2 = data[index + 1];
                                    int pitchValue = (message.Data2 << 7) | message.Data1;
                                    message.FormattedMessage = $"Pitch Bend: Ch{channel} Value={pitchValue - 8192}";
                                    index += 2;
                                }
                                break;

                            case 0xF0: // System Messages
                                message.MessageType = "System";
                                message.Data1 = status;
                                message.Data2 = 0;
                                message.FormattedMessage = $"System: 0x{status:X2}";
                                index++;
                                break;

                            default:
                                // Unknown message type, skip
                                index++;
                                continue;
                        }

                        if (!string.IsNullOrEmpty(message.FormattedMessage))
                        {
                            messages.Add(message);
                        }
                    }
                    else
                    {
                        // Data byte without status, skip or handle running status
                        index++;
                    }
                }
            }
            catch (Exception)
            {
                // Corrupted data, return what we parsed so far
            }

            return messages;
        }

        private static string GetNoteName(int noteNumber)
        {
            if (noteNumber < 0 || noteNumber > 127)
                return "?";

            int octave = (noteNumber / 12) - 1;
            int note = noteNumber % 12;
            return $"{NoteNames[note]}{octave}";
        }
    }
}
