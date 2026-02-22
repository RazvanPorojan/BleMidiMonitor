using System;
using System.Collections.Generic;
using System.Linq;

namespace BleMidiMonitor
{
    public class ChordDetector
    {
        // Chord database: chord suffix -> interval pattern (semitones from root)
        private readonly Dictionary<string, int[]> _chordPatterns = new Dictionary<string, int[]>
        {
            // Triads
            { "", new[] { 0, 4, 7 } },                    // Major
            { "m", new[] { 0, 3, 7 } },                   // Minor
            { "dim", new[] { 0, 3, 6 } },                 // Diminished
            { "aug", new[] { 0, 4, 8 } },                 // Augmented
            { "sus2", new[] { 0, 2, 7 } },                // Suspended 2nd
            { "sus4", new[] { 0, 5, 7 } },                // Suspended 4th
            { "5", new[] { 0, 7 } },                      // Power chord

            // 7th chords
            { "7", new[] { 0, 4, 7, 10 } },               // Dominant 7th
            { "maj7", new[] { 0, 4, 7, 11 } },            // Major 7th
            { "m7", new[] { 0, 3, 7, 10 } },              // Minor 7th
            { "m7b5", new[] { 0, 3, 6, 10 } },            // Half-diminished 7th
            { "dim7", new[] { 0, 3, 6, 9 } },             // Diminished 7th
            { "mMaj7", new[] { 0, 4, 7, 11 } },           // Minor-major 7th

            // Extended chords
            { "6", new[] { 0, 4, 7, 9 } },                // Major 6th
            { "m6", new[] { 0, 3, 7, 9 } },               // Minor 6th
            { "9", new[] { 0, 4, 7, 10, 14 } },           // Dominant 9th (14 = 2 + 12)
            { "maj9", new[] { 0, 4, 7, 11, 14 } },        // Major 9th
            { "m9", new[] { 0, 3, 7, 10, 14 } },          // Minor 9th
            { "add9", new[] { 0, 4, 7, 14 } },            // Add 9 (no 7th)
            { "7sus4", new[] { 0, 5, 7, 10 } },           // 7th suspended 4th
        };

        // Open string notes for standard tuning (chromatic scale index)
        // E=4, A=9, D=2, G=7, B=11, e=4
        private readonly int[] _openStringNotes = { 4, 11, 7, 2, 9, 4 }; // High to low: e, B, G, D, A, E
        private readonly string[] _noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        public ChordResult DetectChord(Dictionary<int, int> activeFrets)
        {
            // No frets pressed
            if (activeFrets == null || activeFrets.Count == 0)
            {
                return new ChordResult
                {
                    ChordName = "--",
                    Confidence = 0,
                    IsValid = false,
                    NoteNames = new List<string>()
                };
            }

            // Calculate notes from active frets
            var notes = CalculateNotes(activeFrets);
            var uniqueNotes = notes.Distinct().OrderBy(n => n).ToList();

            // Need at least 2 notes for a chord
            if (uniqueNotes.Count < 2)
            {
                if (uniqueNotes.Count == 1)
                {
                    return new ChordResult
                    {
                        ChordName = _noteNames[uniqueNotes[0]],
                        Confidence = 50,
                        IsValid = false,
                        NoteNames = new List<string> { _noteNames[uniqueNotes[0]] }
                    };
                }
                return new ChordResult
                {
                    ChordName = "--",
                    Confidence = 0,
                    IsValid = false,
                    NoteNames = new List<string>()
                };
            }

            // Identify bass note (prefer lower strings - higher string numbers)
            int bassNote = GetBassNote(activeFrets);

            // Try each unique note as potential root
            ChordMatch bestMatch = null;
            foreach (var root in uniqueNotes)
            {
                var match = TryMatchChord(root, uniqueNotes, bassNote);
                if (bestMatch == null || match.Score > bestMatch.Score)
                {
                    bestMatch = match;
                }
            }

            if (bestMatch != null && bestMatch.Score >= 60)
            {
                return new ChordResult
                {
                    ChordName = _noteNames[bestMatch.Root] + bestMatch.Suffix,
                    Confidence = bestMatch.Score,
                    IsValid = true,
                    NoteNames = uniqueNotes.Select(n => _noteNames[n]).ToList()
                };
            }

            // No good match found
            return new ChordResult
            {
                ChordName = "?",
                Confidence = bestMatch?.Score ?? 0,
                IsValid = false,
                NoteNames = uniqueNotes.Select(n => _noteNames[n]).ToList()
            };
        }

        private List<int> CalculateNotes(Dictionary<int, int> activeFrets)
        {
            var notes = new List<int>();
            foreach (var kvp in activeFrets)
            {
                int stringNumber = kvp.Key;
                int fretNumber = kvp.Value;

                // Convert string number (1-based) to array index (0-based)
                int stringIndex = stringNumber - 1;
                if (stringIndex >= 0 && stringIndex < _openStringNotes.Length)
                {
                    int note = (_openStringNotes[stringIndex] + fretNumber) % 12;
                    notes.Add(note);
                }
            }
            return notes;
        }

        private int GetBassNote(Dictionary<int, int> activeFrets)
        {
            // Find the lowest note on the lowest strings (strings 4-6 have priority)
            int bassStringNumber = 0;
            int bassNote = -1;

            foreach (var kvp in activeFrets.OrderByDescending(x => x.Key))
            {
                int stringNumber = kvp.Key;
                int fretNumber = kvp.Value;
                int stringIndex = stringNumber - 1;

                if (stringIndex >= 0 && stringIndex < _openStringNotes.Length)
                {
                    if (bassStringNumber == 0 || stringNumber > bassStringNumber)
                    {
                        bassStringNumber = stringNumber;
                        bassNote = (_openStringNotes[stringIndex] + fretNumber) % 12;
                    }
                }
            }

            return bassNote;
        }

        private ChordMatch TryMatchChord(int root, List<int> uniqueNotes, int bassNote)
        {
            ChordMatch bestMatch = null;

            foreach (var pattern in _chordPatterns)
            {
                var intervals = pattern.Value;
                var expectedNotes = intervals.Select(i => (root + i) % 12).ToHashSet();

                // Calculate match score
                int matchedNotes = uniqueNotes.Count(n => expectedNotes.Contains(n));
                int totalExpected = expectedNotes.Count;
                int extraNotes = uniqueNotes.Count - matchedNotes;

                // Base score: percentage of matched notes
                int score = (matchedNotes * 100) / totalExpected;

                // Penalty for extra notes that don't fit the chord
                score -= extraNotes * 20;

                // Bonus if bass note is the root
                if (bassNote == root)
                {
                    score += 10;
                }

                // Bonus for exact match
                if (matchedNotes == totalExpected && extraNotes == 0)
                {
                    score += 20;
                }

                // Keep best match
                if (bestMatch == null || score > bestMatch.Score)
                {
                    bestMatch = new ChordMatch
                    {
                        Root = root,
                        Suffix = pattern.Key,
                        Score = Math.Max(0, Math.Min(100, score))
                    };
                }
            }

            return bestMatch;
        }

        private class ChordMatch
        {
            public int Root { get; set; }
            public string Suffix { get; set; }
            public int Score { get; set; }
        }
    }

    public class ChordResult
    {
        public string ChordName { get; set; } = "";
        public int Confidence { get; set; }
        public bool IsValid { get; set; }
        public List<string> NoteNames { get; set; } = new List<string>();
    }
}
