using System;
using System.Collections.Generic;
using EndlessDescent.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EndlessDescent.Simulation
{
    [Serializable]
    public struct ShopRecord
    {
        public string NpcId;
        public int Gold;
        public int LastRestockDay;
        public string Stock;
    }

    [Serializable]
    public struct ShopLedgerState
    {
        public ShopRecord[] Shops;
    }

    // A town is rebuilt every time it streams back in, and its shopkeepers with it, so an emptied purse
    // came back full after a walk around the block. What each shopkeeper had is kept here by who they
    // are, handed back when they are built again, and saved
    [DisallowMultipleComponent]
    public class ShopLedger : MonoBehaviour, ISaveable
    {
        [SerializeField] string saveKey = "shops";

        readonly Dictionary<string, ShopRecord> records = new Dictionary<string, ShopRecord>();
        readonly List<Merchant> open = new List<Merchant>();

        public string SaveKey => saveKey;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            Attach();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        // Beside the simulation the shopkeepers already register with, so no scene needs rebuilding
        static void Attach()
        {
            WorldSimulation simulation = FindFirstObjectByType<WorldSimulation>();

            if (simulation != null && simulation.GetComponent<ShopLedger>() == null)
                simulation.gameObject.AddComponent<ShopLedger>();
        }

        public void Open(Merchant merchant)
        {
            if (merchant == null || string.IsNullOrEmpty(merchant.ShopId))
                return;

            if (records.TryGetValue(merchant.ShopId, out ShopRecord record))
                merchant.Restore(record);

            open.Add(merchant);
        }

        public void Close(Merchant merchant)
        {
            Keep(merchant);
            open.Remove(merchant);
        }

        void Keep(Merchant merchant)
        {
            if (merchant != null && !string.IsNullOrEmpty(merchant.ShopId))
                records[merchant.ShopId] = merchant.Capture();
        }

        public string CaptureJson()
        {
            foreach (Merchant merchant in open)
                Keep(merchant);

            ShopRecord[] shops = new ShopRecord[records.Count];
            records.Values.CopyTo(shops, 0);

            return JsonUtility.ToJson(new ShopLedgerState { Shops = shops });
        }

        public void RestoreJson(string json)
        {
            records.Clear();

            ShopLedgerState state = JsonUtility.FromJson<ShopLedgerState>(json);

            if (state.Shops != null)
                foreach (ShopRecord record in state.Shops) records[record.NpcId] = record;

            foreach (Merchant merchant in open)
                if (records.TryGetValue(merchant.ShopId, out ShopRecord record)) merchant.Restore(record);
        }
    }
}
