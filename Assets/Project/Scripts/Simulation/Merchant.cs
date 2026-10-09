using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Items;
using UnityEngine;

namespace EndlessDescent.Simulation
{
    [DisallowMultipleComponent]
    public class Merchant : MonoBehaviour, IInteractable
    {
        [SerializeField] NpcActor actor;
        [SerializeField] Inventory stock;
        [SerializeField] EconomySystem economy;
        [SerializeField] WorldSimulation simulation;
        [SerializeField, Min(0)] int gold = 250;
        [SerializeField, Min(0)] int purse = 400;
        [SerializeField, Min(0)] int purseRecoveryPerDay = 60;
        [SerializeField, Min(1)] int restockEveryDays = 3;
        [SerializeField] EndlessDescent.Data.LootTableDefinition restockTable;
        [SerializeField, Min(1)] int restockRolls = 4;
        [SerializeField, Range(0, 23)] int opensHour = 8;
        [SerializeField, Range(0, 23)] int closesHour = 19;

        int lastRestockDay = -1;

        // What the player sold here, so the trade screen can say which shelf items were theirs an
        // hour ago. Cleared on restock, because by then it is simply stock
        readonly System.Collections.Generic.HashSet<EndlessDescent.Data.ItemDefinition> boughtFromPlayer =
            new System.Collections.Generic.HashSet<EndlessDescent.Data.ItemDefinition>();

        public bool WasSoldByPlayer(EndlessDescent.Data.ItemDefinition item) =>
            item != null && boughtFromPlayer.Contains(item);

        // Shopkeepers are prefabs built into towns at runtime, and a prefab cannot hold a reference to
        // the scene's systems, so they are found rather than wired
        void Start()
        {
            if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
            if (simulation == null) simulation = FindFirstObjectByType<WorldSimulation>();

            ledger = FindFirstObjectByType<ShopLedger>();
            ledger?.Open(this);
            simulation?.RegisterMerchant(this);
        }

        void OnDestroy()
        {
            ledger?.Close(this);
            simulation?.UnregisterMerchant(this);
        }

        ShopLedger ledger;

        public string ShopId => actor != null && actor.IsListed ? actor.Id : null;

        public ShopRecord Capture() => new ShopRecord
        {
            NpcId = ShopId,
            Gold = gold,
            LastRestockDay = lastRestockDay,
            Stock = stock != null ? stock.CaptureJson() : null
        };

        public void Restore(ShopRecord record)
        {
            gold = record.Gold;
            lastRestockDay = record.LastRestockDay;

            if (stock != null && !string.IsNullOrEmpty(record.Stock))
                stock.RestoreJson(record.Stock);
        }

        public NpcActor Actor => actor;
        public Inventory Stock => stock;
        public int Gold => gold;
        public int Purse => purse;

        // Why the last trade did not happen. A click that silently does nothing is the worst thing a
        // shop can do, and refusing for want of money is the common case now that the purse is real
        public string Refusal { get; private set; }

        public bool IsOpen
        {
            get
            {
                WorldClock clock = WorldClock.Instance;
                if (clock == null)
                    return true;

                return clock.Hour >= opensHour && clock.Hour < closesHour;
            }
        }

        public string Prompt => IsOpen ? $"Trade with {actor?.DisplayName}" : $"{actor?.DisplayName} is closed";

        public bool CanInteract(GameObject gameObjectActor)
        {
            return IsOpen && actor != null && actor.IsAlive;
        }

        public void Interact(GameObject gameObjectActor)
        {
            if (!IsOpen)
                return;

            // The screen is a scene singleton, the merchant only says who is trading
            TradeScreens.Current?.Open(this);
        }

        public int PriceToBuy(ItemInstance stack, ICharacterStats haggler = null) =>
            economy != null ? economy.BuyPrice(stack.UnitValue, actor?.Definition?.Faction, haggler) : stack.UnitValue;

        public int PriceToSell(ItemInstance stack, ICharacterStats haggler = null) =>
            economy != null ? economy.SellPrice(stack.UnitValue, actor?.Definition?.Faction, haggler) : stack.UnitValue;

        // One of the stack that was clicked, not one of whatever shares its definition. Selling by
        // definition took from the end of the pack, so selling one of two swords could hand over the
        // enchanted one, and buying back gave a fresh copy rather than the thing that was sold
        public bool PlayerBuys(ItemInstance stack, Inventory playerInventory, Wallet playerWallet, ICharacterStats haggler = null)
        {
            if (stack?.Item == null || stock == null || playerInventory == null || playerWallet == null || !IsOpen)
                return false;

            int price = PriceToBuy(stack, haggler);
            if (!playerWallet.TrySpend(price))
            {
                Refusal = $"You cannot afford {stack.DisplayName} at {price} gold.";
                return false;
            }

            if (!MoveOne(stack, stock, playerInventory))
            {
                playerWallet.Add(price);
                Refusal = "Your pack is full.";
                return false;
            }

            Refusal = null;
            gold += price;
            return true;
        }

        public bool PlayerSells(ItemInstance stack, Inventory playerInventory, Wallet playerWallet, ICharacterStats haggler = null)
        {
            if (stack?.Item == null || stock == null || playerInventory == null || playerWallet == null || !IsOpen)
                return false;

            int price = PriceToSell(stack, haggler);
            if (gold < price)
            {
                Refusal = $"{actor?.DisplayName} has {gold} gold and cannot cover {price}.";
                return false;
            }

            ItemDefinition item = stack.Item;

            if (!MoveOne(stack, playerInventory, stock))
            {
                Refusal = $"{actor?.DisplayName} has no room for it.";
                return false;
            }

            Refusal = null;
            gold -= price;
            playerWallet.Add(price);
            boughtFromPlayer.Add(item);
            return true;
        }

        // Gear moves as the same instance, wear and enchantment with it; goods move one of a count
        static bool MoveOne(ItemInstance stack, Inventory from, Inventory to)
        {
            if (stack.HasCondition)
            {
                if (!from.Remove(stack))
                    return false;

                if (to.AddInstance(stack))
                    return true;

                from.AddInstance(stack);
                return false;
            }

            if (!from.Remove(stack))
                return false;

            if (to.Add(stack.Item, 1) == 0)
                return true;

            from.Add(stack.Item, 1);
            return false;
        }

        public void RestockIfDue(int day)
        {
            if (lastRestockDay >= 0 && day - lastRestockDay < restockEveryDays)
                return;

            int days = lastRestockDay < 0 ? 1 : day - lastRestockDay;
            lastRestockDay = day;
            boughtFromPlayer.Clear();

            // The purse used to jump straight back to full, so emptying a shop cost the player
            // nothing but a walk around the block. It recovers at a rate instead, up to its own cap
            int ceiling = actor?.Definition != null ? Mathf.Max(purse, actor.Definition.StartingGold) : purse;
            gold = Mathf.Min(ceiling, gold + purseRecoveryPerDay * Mathf.Max(1, days));

            // Restoring the purse was never enough, an empty shelf stayed empty. Stock is rolled from
            // the same loot table machinery dungeons use, so a shop refills over time
            if (restockTable == null || stock == null)
                return;

            EndlessDescent.Core.DeterministicRandom rng =
                new EndlessDescent.Core.DeterministicRandom(day * 7919 + GetInstanceID());

            for (int i = 0; i < restockRolls; i++)
            {
                // Shallow, a shop sells what is common, not what is down at the bottom of a mine
                EndlessDescent.Items.LootResult roll = EndlessDescent.Items.LootRoller.Roll(restockTable, ref rng, 0.35f);

                foreach (EndlessDescent.Items.ItemInstance instance in roll.Items)
                    stock.Add(instance.Item, instance.Count);
            }
        }
    }
}
