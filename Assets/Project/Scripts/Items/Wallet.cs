using System;
using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.Items
{
    [Serializable]
    public struct WalletState
    {
        public int Gold;
    }

    [DisallowMultipleComponent]
    public class Wallet : MonoBehaviour, ISaveable
    {
        [SerializeField, Min(0)] int gold = 50;
        [SerializeField] string saveKey = "wallet.player";

        public event Action<int> Changed;

        public int Gold => gold;
        public string SaveKey => saveKey;

        public bool CanAfford(int amount) => gold >= amount;

        public void Add(int amount)
        {
            if (amount <= 0)
                return;

            gold += amount;
            Changed?.Invoke(gold);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0)
                return true;

            if (gold < amount)
                return false;

            gold -= amount;
            Changed?.Invoke(gold);
            return true;
        }

        public string CaptureJson() => JsonUtility.ToJson(new WalletState { Gold = gold });

        public void RestoreJson(string json)
        {
            gold = JsonUtility.FromJson<WalletState>(json).Gold;
            Changed?.Invoke(gold);
        }
    }
}
