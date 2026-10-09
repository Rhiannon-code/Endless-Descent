using System.Collections.Generic;
using EndlessDescent.Data;
using EndlessDescent.Items;
using EndlessDescent.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // Buying and selling. It used to need authoring into a scene and no scene ever authored it, so
    // every merchant in the world opened nothing, it installs itself onto the HUD now, the way the
    // board and the dialogue screen do
    [DisallowMultipleComponent]
    public class TradeScreen : MonoBehaviour, ITradeScreen
    {
        [SerializeField] Inventory playerInventory;
        [SerializeField] Wallet playerWallet;

        GameObject panel;
        RectTransform buyContent;
        RectTransform sellContent;
        Text header;
        Text notice;

        readonly List<GameObject> rows = new List<GameObject>();

        Merchant merchant;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            Attach();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        static void Attach()
        {
            foreach (PlayerHud hud in FindObjectsByType<PlayerHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (hud.GetComponentInChildren<TradeScreen>(true) == null)
                    hud.gameObject.AddComponent<TradeScreen>();
        }

        // Mercantile and Personality are what the asking price answers to, and the sheet is on the
        // same object as the pack the trade moves things into
        ICharacterStats Haggler => playerInventory != null
            ? playerInventory.GetComponent<EndlessDescent.Player.CharacterSheet>()
            : null;

        void OnDestroy()
        {
            if (ReferenceEquals(TradeScreens.Current, this))
                TradeScreens.Current = null;
        }

        void Awake()
        {
            TradeScreens.Current = this;

            panel = new GameObject("Trade", typeof(RectTransform));
            panel.transform.SetParent(transform, false);

            RectTransform root = panel.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(900f, 560f);

            panel.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.97f);

            header = MenuWidgets.Label(root, new Vector2(20f, -16f), 760f, string.Empty, 17);
            header.color = new Color(0.95f, 0.85f, 0.5f);

            notice = MenuWidgets.Label(root, new Vector2(20f, -42f), 760f, string.Empty, 12);
            notice.color = new Color(1f, 0.66f, 0.6f);

            MenuWidgets.Button(root, new Vector2(800f, -14f), new Vector2(80f, 26f), Close).text = "Close";

            MenuWidgets.Label(root, new Vector2(20f, -70f), 400f, "THEIRS", 13);
            MenuWidgets.Label(root, new Vector2(470f, -70f), 400f, "YOURS", 13);

            buyContent = MenuWidgets.ScrollPanel(root, "Buy", new Vector2(20f, -94f), new Vector2(430f, 440f));
            sellContent = MenuWidgets.ScrollPanel(root, "Sell", new Vector2(470f, -94f), new Vector2(430f, 440f));

            panel.SetActive(false);
        }

        void Start()
        {
            if (playerInventory != null)
                return;

            // The HUD is built beside the player, so the pack and the purse are found rather than
            // wired, nothing authored this screen into the scene to wire them
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player == null)
                return;

            playerInventory = player.GetComponentInChildren<Inventory>();
            playerWallet = player.GetComponentInChildren<Wallet>();
        }

        public void Open(Merchant target)
        {
            merchant = target;
            panel.SetActive(true);
            ScreenFocus.Take();
            Rebuild();
        }

        public void Close()
        {
            if (!panel.activeSelf)
                return;

            merchant = null;
            panel.SetActive(false);
            ScreenFocus.Release();
        }

        void Update()
        {
            if (panel.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }

        void Rebuild()
        {
            foreach (GameObject row in rows)
                Destroy(row);

            rows.Clear();

            if (merchant == null)
                return;

            header.text = $"{merchant.Actor?.DisplayName}   :   their purse: {merchant.Gold}g   :   " +
                          $"yours: {(playerWallet != null ? playerWallet.Gold : 0)}g";

            notice.text = merchant.Refusal ?? string.Empty;

            if (merchant.Stock != null)
                Fill(buyContent, merchant.Stock.Stacks, true);

            if (playerInventory != null)
                Fill(sellContent, playerInventory.Stacks, false);
        }

        void Fill(RectTransform parent, IReadOnlyList<ItemInstance> stacks, bool buying)
        {
            float y = 0f;

            for (int i = 0; i < stacks.Count; i++)
            {
                ItemInstance stack = stacks[i];
                ItemDefinition item = stack.Item;

                if (item == null)
                    continue;

                int price = buying ? merchant.PriceToBuy(stack, Haggler) : merchant.PriceToSell(stack, Haggler);

                Text label = MenuWidgets.Button(parent, new Vector2(0f, y), new Vector2(424f, 26f), () =>
                {
                    bool traded = buying
                        ? merchant.PlayerBuys(stack, playerInventory, playerWallet, Haggler)
                        : merchant.PlayerSells(stack, playerInventory, playerWallet, Haggler);

                    // Mercantile trains by trading
                    if (traded)
                        (Haggler as EndlessDescent.Player.CharacterSheet)?.Use(SkillId.Mercantile);

                    Rebuild();
                });

                label.text = Describe(stack, item, price, buying);
                label.fontSize = 12;

                rows.Add(label.transform.parent.gameObject);
                y -= 30f;
            }

            MenuWidgets.Fit(parent, -y);
        }

        // What the piece is worth against what is being asked for it, so Mercantile is visible
        // rather than a number nobody can compare against anything
        string Describe(ItemInstance stack, ItemDefinition item, int price, bool buying)
        {
            int worth = stack.UnitValue;

            string margin = worth <= 0 ? string.Empty
                : price > worth ? $"  (+{100 * (price - worth) / worth}%)"
                : price < worth ? $"  (-{100 * (worth - price) / worth}%)"
                : string.Empty;

            string yours = buying && merchant.WasSoldByPlayer(item) ? "  [yours]" : string.Empty;

            return $"{stack.DisplayName} x{stack.Count}  -  {price}g{margin}{yours}";
        }
    }
}
