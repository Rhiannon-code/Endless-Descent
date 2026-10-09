using System;
using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.Simulation
{
    public enum CrimeKind { Trespass, Theft, Assault, Murder }

    [Serializable]
    public struct CrimeRecordState
    {
        public int Bounty;
        public int Crimes;
    }

    // One running bounty. Guards read it, constables clear it, and it is what makes a reputation
    // something you can lose as well as build
    [DisallowMultipleComponent]
    public class CrimeRecord : MonoBehaviour, ISaveable
    {
        [SerializeField] string saveKey = "crime.player";

        int bounty;
        int crimes;

        public event Action<int> BountyChanged;

        public string SaveKey => saveKey;
        public int Bounty => bounty;
        public int Crimes => crimes;
        public bool IsWanted => bounty > 0;

        public static int FineFor(CrimeKind kind)
        {
            switch (kind)
            {
                case CrimeKind.Trespass: return 20;
                case CrimeKind.Theft: return 60;
                case CrimeKind.Assault: return 150;
                default: return 500;
            }
        }

        public void Report(CrimeKind kind)
        {
            bounty += FineFor(kind);
            crimes++;
            BountyChanged?.Invoke(bounty);
            Notice.Show($"Crime witnessed: {kind}. Bounty now {bounty}.");
        }

        public void Clear()
        {
            if (bounty == 0)
                return;

            bounty = 0;
            BountyChanged?.Invoke(bounty);
        }

        public string CaptureJson() =>
            JsonUtility.ToJson(new CrimeRecordState { Bounty = bounty, Crimes = crimes });

        public void RestoreJson(string json)
        {
            CrimeRecordState state = JsonUtility.FromJson<CrimeRecordState>(json);
            bounty = Mathf.Max(0, state.Bounty);
            crimes = Mathf.Max(0, state.Crimes);
            BountyChanged?.Invoke(bounty);
        }
    }
}
