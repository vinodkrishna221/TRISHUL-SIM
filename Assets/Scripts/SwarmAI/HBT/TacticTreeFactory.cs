// ============================================================
// TacticTreeFactory.cs — Swarm Behavior Tree Factory
// TRISHUL-SIM · SA-P2-A · swarm-ai track (Uday)
//
// Instantiates deterministic HBT tactic trees matching SwarmProfile.tacticTree
// Contract: docs/SCHEMA.md Entity 1, board/contracts/scenario-events.md
// ============================================================

using System;
using System.Collections.Generic;

namespace TrishulSim.SwarmAI.HBT
{
    /// <summary>
    /// Factory for creating tactic tree instances based on SwarmProfile.tacticTree.
    /// </summary>
    public static class TacticTreeFactory
    {
        public const string PincerLowFast = "PINCER_LOW_FAST";
        public const string SaturationDive = "SATURATION_DIVE";
        public const string MultiAxisSplit = "MULTI-AXIS_SPLIT";

        private static readonly string[] AvailableTactics = new string[]
        {
            PincerLowFast,
            SaturationDive,
            MultiAxisSplit
        };

        /// <summary>
        /// Creates a TacticTree instance matching the specified tactic name.
        /// Throws ArgumentException for unknown tactic trees (or falls back if allowFallback=true).
        /// </summary>
        public static TacticTree Create(string tacticTree, bool allowFallback = true)
        {
            if (string.IsNullOrEmpty(tacticTree))
            {
                if (allowFallback)
                    return new PincerLowFastTactic();
                throw new ArgumentNullException("tacticTree", "Tactic tree name cannot be null or empty.");
            }

            string clean = tacticTree.Trim().ToUpperInvariant();

            // Normalise hyphens and underscores
            if (clean.Equals(PincerLowFast, StringComparison.OrdinalIgnoreCase) ||
                clean.Equals("PINCER-LOW-FAST", StringComparison.OrdinalIgnoreCase))
            {
                return new PincerLowFastTactic();
            }

            if (clean.Equals(SaturationDive, StringComparison.OrdinalIgnoreCase) ||
                clean.Equals("SATURATION-DIVE", StringComparison.OrdinalIgnoreCase))
            {
                return new SaturationDiveTactic();
            }

            if (clean.Equals(MultiAxisSplit, StringComparison.OrdinalIgnoreCase) ||
                clean.Equals("MULTI_AXIS_SPLIT", StringComparison.OrdinalIgnoreCase))
            {
                return new MultiAxisSplitTactic();
            }

            if (allowFallback)
            {
                // Fallback to standard PINCER_LOW_FAST
                return new PincerLowFastTactic();
            }

            throw new ArgumentException(string.Format("Unknown tactic tree '{0}'. Valid options: {1}",
                tacticTree, string.Join(", ", AvailableTactics)), "tacticTree");
        }

        /// <summary>
        /// Verifies whether the tactic tree name is recognized by the factory.
        /// </summary>
        public static bool IsValidTactic(string tacticTree)
        {
            if (string.IsNullOrEmpty(tacticTree))
                return false;

            string clean = tacticTree.Trim().ToUpperInvariant();
            return clean.Equals(PincerLowFast, StringComparison.OrdinalIgnoreCase)
                || clean.Equals("PINCER-LOW-FAST", StringComparison.OrdinalIgnoreCase)
                || clean.Equals(SaturationDive, StringComparison.OrdinalIgnoreCase)
                || clean.Equals("SATURATION-DIVE", StringComparison.OrdinalIgnoreCase)
                || clean.Equals(MultiAxisSplit, StringComparison.OrdinalIgnoreCase)
                || clean.Equals("MULTI_AXIS_SPLIT", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns all officially supported tactic tree names.
        /// </summary>
        public static string[] GetAvailableTactics()
        {
            string[] copy = new string[AvailableTactics.Length];
            Array.Copy(AvailableTactics, copy, AvailableTactics.Length);
            return copy;
        }
    }
}
