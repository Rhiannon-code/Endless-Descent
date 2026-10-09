using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Simulation
{
    [DisallowMultipleComponent]
    public class EconomySystem : MonoBehaviour
    {
        [SerializeField] FactionRegistry factions;
        [SerializeField, Min(1f)] float merchantMarkup = 1.6f;
        [SerializeField, Range(0.05f, 1f)] float merchantBuyback = 0.45f;
        [SerializeField, Range(0f, 0.5f)] float bestStandingDiscount = 0.25f;

        public int BuyPrice(int value, FactionDefinition merchantFaction, ICharacterStats buyer = null)
        {
            if (value <= 0)
                return 0;

            float price = value * merchantMarkup * (1f - DiscountFor(merchantFaction))
                          * CharacterMaths.PriceScale(buyer, true);

            return Mathf.Max(1, Mathf.RoundToInt(price));
        }

        public int SellPrice(int value, FactionDefinition merchantFaction, ICharacterStats seller = null)
        {
            if (value <= 0)
                return 0;

            float price = value * merchantBuyback * (1f + DiscountFor(merchantFaction))
                          * CharacterMaths.PriceScale(seller, false);

            return Mathf.Max(1, Mathf.RoundToInt(price));
        }

        float DiscountFor(FactionDefinition faction)
        {
            if (factions == null || faction == null)
                return 0f;

            float normalised = Mathf.InverseLerp(-100f, 100f, factions.StandingOf(faction));
            return Mathf.Lerp(-bestStandingDiscount, bestStandingDiscount, normalised);
        }
    }
}
