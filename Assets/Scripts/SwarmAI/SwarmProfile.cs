// ============================================================
// SwarmProfile.cs — Swarm Configuration Profile
// TRISHUL-SIM · docs/SCHEMA.md Entity 1 · swarm-ai track (Uday)
// ============================================================

using System;

namespace TrishulSim.SwarmAI
{
    /// <summary>
    /// Swarm profile matching docs/SCHEMA.md Entity 1 and board/contracts/scenario-events.md.
    /// </summary>
    [Serializable]
    public class SwarmProfile
    {
        public int agentCount;
        public int decoyCount;
        public int attackerCount;
        public string tacticTree;
        public float speedKmh;
        public float altitudeM;
        public float[] rfFreqGHz;
        public float rfPowerDbm;

        public float SpeedMs
        {
            get { return speedKmh / 3.6f; }
        }

        public SwarmProfile()
        {
            agentCount = 6;
            decoyCount = 2;
            attackerCount = 4;
            tacticTree = "PINCER_LOW_FAST";
            speedKmh = 120.0f;
            altitudeM = 8.0f;
            rfFreqGHz = new float[] { 5.825f, 2.4f };
            rfPowerDbm = 23.0f;
        }

        public SwarmProfile(int agentCount, int decoyCount, int attackerCount, string tacticTree,
            float speedKmh, float altitudeM, float[] rfFreqGHz, float rfPowerDbm)
        {
            this.agentCount = agentCount;
            this.decoyCount = decoyCount;
            this.attackerCount = attackerCount;
            this.tacticTree = tacticTree ?? "PINCER_LOW_FAST";
            this.speedKmh = speedKmh;
            this.altitudeM = altitudeM;
            this.rfFreqGHz = rfFreqGHz ?? new float[] { 5.825f };
            this.rfPowerDbm = rfPowerDbm;
        }

        public SwarmProfile Clone()
        {
            float[] freqs = null;
            if (rfFreqGHz != null)
            {
                freqs = new float[rfFreqGHz.Length];
                Array.Copy(rfFreqGHz, freqs, rfFreqGHz.Length);
            }

            return new SwarmProfile(
                agentCount,
                decoyCount,
                attackerCount,
                tacticTree,
                speedKmh,
                altitudeM,
                freqs,
                rfPowerDbm
            );
        }
    }
}
