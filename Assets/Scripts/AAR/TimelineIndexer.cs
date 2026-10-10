// ============================================================
// TimelineIndexer.cs — Timeline Indexer & O(log n) Seek Engine
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Implements:
// - board/contracts/scorecard.md v1 §"UI rendering contract"
// - board/contracts/telemetry.md v1 (TelemetryRecord.t semantics)
// - docs/SCHEMA.md Entity 4 (timelineIndex sorted array of t values)
// - docs/SCHEMA.md §"Index" note:
//     "NDJSON is sequential; Timeline Scrubber builds an in-memory index on t
//      at Scorecard load time (O(n) scan once, then O(log n) seek)."
// - missions/sohail-mission.md card AA-P1-B
//
// Provides:
// 1. BuildIndex(string ndjsonFilePath): single-pass scan of NDJSON parsing t
// 2. BuildIndexAsync(string ndjsonFilePath): non-blocking Task wrapper
// 3. BuildIndexFromRecords(IEnumerable<TelemetryRecord>): in-memory index builder
// 4. SeekToTime(string ndjsonFilePath, int[] index, int targetT): binary search
//    for closest t -> line seek -> deserialized TelemetryRecord (<20ms SLA)
// ============================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TrishulSim.AAR
{
    /// <summary>
    /// High-performance timeline indexer and scrubber seek engine for TRISHUL-SIM NDJSON session files.
    /// Builds sorted timestamp index for O(log n) time-seeking into recorded telemetry.
    /// </summary>
    public static class TimelineIndexer
    {
        private static readonly Dictionary<string, long[]> _fileByteOffsets = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Scans NDJSON file and extracts all valid `t` scenario-elapsed millisecond values.
        /// Returns a sorted int[] of timestamps. Caches byte offsets in memory for instant O(1) seek.
        /// </summary>
        /// <param name="ndjsonFilePath">Absolute or relative path to the .ndjson session file.</param>
        /// <returns>Sorted array of timestamps; empty array if file missing or empty.</returns>
        public static int[] BuildIndex(string ndjsonFilePath)
        {
            if (string.IsNullOrWhiteSpace(ndjsonFilePath) || !File.Exists(ndjsonFilePath))
            {
                return new int[0];
            }

            List<int> timestamps = new List<int>();
            List<long> byteOffsets = new List<long>();

            try
            {
                using (FileStream fs = new FileStream(ndjsonFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // Scan byte offsets for instant O(1) seek
                    byte[] buffer = new byte[8192];
                    long currentPos = 0;
                    byteOffsets.Add(0);
                    int bytesRead;
                    while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        for (int i = 0; i < bytesRead; i++)
                        {
                            if (buffer[i] == (byte)'\n')
                            {
                                byteOffsets.Add(currentPos + i + 1);
                            }
                        }
                        currentPos += bytesRead;
                    }

                    // Parse timestamps using StreamReader
                    fs.Seek(0, SeekOrigin.Begin);
                    using (StreamReader reader = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line))
                                continue;

                            int tVal;
                            if (TryExtractTimestamp(line, out tVal))
                            {
                                timestamps.Add(tVal);
                            }
                        }
                    }
                }

                lock (_fileByteOffsets)
                {
                    _fileByteOffsets[ndjsonFilePath] = byteOffsets.ToArray();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TimelineIndexer] Error building index from " + ndjsonFilePath + ": " + ex.Message);
                return new int[0];
            }

            int[] result = timestamps.ToArray();

            // Telemetry records are written sequentially, but verify/ensure sorted order
            bool isSorted = true;
            for (int i = 1; i < result.Length; i++)
            {
                if (result[i] < result[i - 1])
                {
                    isSorted = false;
                    break;
                }
            }

            if (!isSorted)
            {
                Array.Sort(result);
            }

            return result;
        }

        /// <summary>
        /// Asynchronously builds the timeline index on a background thread.
        /// </summary>
        public static Task<int[]> BuildIndexAsync(string ndjsonFilePath)
        {
            return Task.Run(() => BuildIndex(ndjsonFilePath));
        }

        /// <summary>
        /// Builds sorted timestamp index directly from an in-memory collection of TelemetryRecords.
        /// </summary>
        public static int[] BuildIndexFromRecords(IEnumerable<TelemetryRecord> records)
        {
            if (records == null)
                return new int[0];

            List<int> list = new List<int>();
            foreach (TelemetryRecord rec in records)
            {
                if (rec != null)
                    list.Add((int)rec.t);
            }

            int[] arr = list.ToArray();
            Array.Sort(arr);
            return arr;
        }

        /// <summary>
        /// Finds the array index of the timestamp closest to targetT using binary search (O(log n)).
        /// </summary>
        /// <param name="index">Sorted array of timestamps.</param>
        /// <param name="targetT">Requested scenario-elapsed millisecond.</param>
        /// <returns>Closest array index, or -1 if index array is null/empty.</returns>
        public static int FindClosestTimeIndex(int[] index, int targetT)
        {
            if (index == null || index.Length == 0)
                return -1;

            if (index.Length == 1)
                return 0;

            if (targetT <= index[0])
                return 0;

            if (targetT >= index[index.Length - 1])
                return index.Length - 1;

            int pos = Array.BinarySearch(index, targetT);
            if (pos >= 0)
                return pos;

            int insert = ~pos;
            if (insert <= 0)
                return 0;
            if (insert >= index.Length)
                return index.Length - 1;

            int diffBefore = Math.Abs(index[insert - 1] - targetT);
            int diffAfter = Math.Abs(index[insert] - targetT);

            return (diffBefore <= diffAfter) ? (insert - 1) : insert;
        }

        /// <summary>
        /// Seeks to the TelemetryRecord closest to targetT in the NDJSON session file.
        /// Performs binary search on index array (O(log n)), then seeks directly to that line.
        /// Completes in &lt;20 ms for typical scenario lengths (&lt;=1200 lines).
        /// </summary>
        /// <param name="ndjsonFilePath">Absolute or relative path to the .ndjson file.</param>
        /// <param name="index">Sorted timeline timestamp array.</param>
        /// <param name="targetT">Target timestamp in milliseconds.</param>
        /// <returns>Deserialized TelemetryRecord, or null if seeking/deserialization fails.</returns>
        public static TelemetryRecord SeekToTime(string ndjsonFilePath, int[] index, int targetT)
        {
            if (string.IsNullOrWhiteSpace(ndjsonFilePath) || !File.Exists(ndjsonFilePath))
            {
                Debug.LogWarning("[TimelineIndexer] Cannot seek: file does not exist: " + ndjsonFilePath);
                return null;
            }

            if (index == null || index.Length == 0)
            {
                // Fallback: build index on demand
                index = BuildIndex(ndjsonFilePath);
                if (index.Length == 0)
                    return null;
            }

            int targetLineIdx = FindClosestTimeIndex(index, targetT);
            if (targetLineIdx < 0)
                return null;

            try
            {
                using (FileStream fs = new FileStream(ndjsonFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long[] offsets = null;
                    lock (_fileByteOffsets)
                    {
                        _fileByteOffsets.TryGetValue(ndjsonFilePath, out offsets);
                    }

                    if (offsets != null && targetLineIdx < offsets.Length && offsets[targetLineIdx] < fs.Length)
                    {
                        fs.Seek(offsets[targetLineIdx], SeekOrigin.Begin);
                        using (StreamReader reader = new StreamReader(fs, Encoding.UTF8))
                        {
                            string targetLine = reader.ReadLine();
                            if (!string.IsNullOrWhiteSpace(targetLine))
                            {
                                TelemetryRecord rec;
                                string error;
                                if (TelemetryRecord.TryParse(targetLine, out rec, out error))
                                {
                                    return rec;
                                }
                            }
                        }
                    }
                    else
                    {
                        using (StreamReader reader = new StreamReader(fs, Encoding.UTF8))
                        {
                            // Fast skip up to targetLineIdx lines without allocating strings
                            for (int i = 0; i < targetLineIdx; i++)
                            {
                                int ch;
                                while ((ch = reader.Read()) != -1)
                                {
                                    if (ch == '\n')
                                        break;
                                }
                                if (ch == -1)
                                    return null;
                            }

                            string targetLine = reader.ReadLine();
                            if (!string.IsNullOrWhiteSpace(targetLine))
                            {
                                TelemetryRecord rec;
                                string error;
                                if (TelemetryRecord.TryParse(targetLine, out rec, out error))
                                {
                                    return rec;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TimelineIndexer] Exception seeking to t=" + targetT + " in " + ndjsonFilePath + ": " + ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Fast extractor for the integer "t" field from a JSON line without full object deserialization.
        /// Falls back to TelemetryRecord.TryParse if simple token scanning encounters unexpected formatting.
        /// </summary>
        public static bool TryExtractTimestamp(string jsonLine, out int timestamp)
        {
            timestamp = 0;
            if (string.IsNullOrWhiteSpace(jsonLine))
                return false;

            // Search for "t": in the JSON string
            int tKeyIdx = jsonLine.IndexOf("\"t\"", StringComparison.Ordinal);
            if (tKeyIdx >= 0)
            {
                int colonIdx = jsonLine.IndexOf(':', tKeyIdx + 3);
                if (colonIdx >= 0)
                {
                    int start = colonIdx + 1;
                    while (start < jsonLine.Length && (jsonLine[start] == ' ' || jsonLine[start] == '\t'))
                    {
                        start++;
                    }

                    int end = start;
                    while (end < jsonLine.Length && (char.IsDigit(jsonLine[end]) || jsonLine[end] == '-'))
                    {
                        end++;
                    }

                    if (end > start)
                    {
                        string numStr = jsonLine.Substring(start, end - start);
                        if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp))
                        {
                            return true;
                        }
                    }
                }
            }

            // Fallback: parse entire record
            TelemetryRecord rec;
            string err;
            if (TelemetryRecord.TryParse(jsonLine, out rec, out err))
            {
                timestamp = (int)rec.t;
                return true;
            }

            return false;
        }
    }
}
