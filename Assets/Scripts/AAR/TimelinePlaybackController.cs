// ============================================================
// TimelinePlaybackController.cs — Scrubber Playback Controller
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Implements:
// - missions/sohail-mission.md card AA-P1-B
// - plans/aar-engine-phase-plan.md card AA-P1-B
// - board/contracts/scorecard.md v1 §"UI rendering contract"
//
// Provides observable playback & timeline scrubbing interface consumed by
// Rajeswari's UX-P1-D (ScorecardRenderer & Timeline Scrubber UI).
//
// Key API:
// - void SetScrubPosition(int targetT): seeks in NDJSON via TimelineIndexer
// - TelemetryRecord CurrentRecord { get; } (observable state)
// - event Action<TelemetryRecord> OnScrubRecordChanged
// - Emits Console log: "Seeked to t=<t> — agentCount=<n>, threatState=<state>"
// ============================================================

using System;
using UnityEngine;

namespace TrishulSim.AAR
{
    /// <summary>
    /// MonoBehaviour coordinating playback position and timeline scrubbing for the AAR Scorecard.
    /// Exposes CurrentRecord and SetScrubPosition() for UITK scrub bar binding.
    /// </summary>
    public class TimelinePlaybackController : MonoBehaviour
    {
        public static TimelinePlaybackController Instance { get; private set; }

        [SerializeField] private string sessionFilePath;
        [SerializeField] private int[] timelineIndex = new int[0];

        private TelemetryRecord _currentRecord;

        /// <summary>
        /// Fires whenever the scrub position changes and a new TelemetryRecord is loaded.
        /// </summary>
        public event Action<TelemetryRecord> OnScrubRecordChanged;

        /// <summary>
        /// The currently loaded TelemetryRecord at the scrubber position.
        /// Observable property bound by UI-UX (UX-P1-D).
        /// </summary>
        public TelemetryRecord CurrentRecord
        {
            get { return _currentRecord; }
            private set { _currentRecord = value; }
        }

        /// <summary>
        /// Path to the active session NDJSON file.
        /// </summary>
        public string SessionFilePath
        {
            get { return sessionFilePath; }
            set { sessionFilePath = value; }
        }

        /// <summary>
        /// Cached sorted array of scenario timestamps.
        /// </summary>
        public int[] TimelineIndex
        {
            get { return timelineIndex; }
            set { timelineIndex = value ?? new int[0]; }
        }

        /// <summary>
        /// Scenario-elapsed time of the current scrub record in milliseconds.
        /// </summary>
        public int CurrentTimeMs
        {
            get { return _currentRecord != null ? (int)_currentRecord.t : 0; }
        }

        /// <summary>
        /// Total number of indexed ticks available for scrubbing.
        /// </summary>
        public int TickCount
        {
            get { return timelineIndex != null ? timelineIndex.Length : 0; }
        }

        public void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        public void OnDestroy()
        {
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Initializes the scrubber controller with a session file path and its timeline index.
        /// </summary>
        public void Initialize(string filePath, int[] index)
        {
            sessionFilePath = filePath;
            timelineIndex = index ?? new int[0];

            if (timelineIndex.Length > 0)
            {
                // Load the initial record (first timestamp)
                SetScrubPosition(timelineIndex[0]);
            }
        }

        /// <summary>
        /// Initializes the scrubber controller directly from a completed Scorecard.
        /// </summary>
        public void Initialize(Scorecard scorecard)
        {
            if (scorecard == null)
                return;

            sessionFilePath = scorecard.sessionFilePath;
            timelineIndex = scorecard.timelineIndex ?? new int[0];

            if (timelineIndex.Length > 0)
            {
                SetScrubPosition(timelineIndex[0]);
            }
        }

        /// <summary>
        /// Seeks the simulation replay to the closest recorded tick at or near targetT.
        /// Queries TimelineIndexer.SeekToTime, updates CurrentRecord, logs exact test line,
        /// and notifies observers.
        /// </summary>
        /// <param name="targetT">Desired scenario time in milliseconds.</param>
        /// <returns>The seeked TelemetryRecord, or null if seek failed.</returns>
        public TelemetryRecord SetScrubPosition(int targetT)
        {
            TelemetryRecord record = TimelineIndexer.SeekToTime(sessionFilePath, timelineIndex, targetT);
            if (record != null)
            {
                _currentRecord = record;

                int agentCount = (record.swarmSnapshot != null) ? record.swarmSnapshot.Length : 0;
                string threat = !string.IsNullOrEmpty(record.threatState) ? record.threatState : "GREEN";

                // Emit required contract / test console message
                Debug.Log(string.Format("Seeked to t={0} — agentCount={1}, threatState={2}", record.t, agentCount, threat));

                Action<TelemetryRecord> handler = OnScrubRecordChanged;
                if (handler != null)
                {
                    handler(record);
                }
            }
            else
            {
                Debug.LogWarning(string.Format("[TimelinePlaybackController] Seek failed for targetT={0} in file {1}", targetT, sessionFilePath));
            }

            return record;
        }
    }
}
