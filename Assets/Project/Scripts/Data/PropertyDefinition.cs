using UnityEngine;

namespace EndlessDescent.Data
{
    [CreateAssetMenu(menuName = "Endless Descent/World/Property", fileName = "Property")]
    public class PropertyDefinition : ScriptableObject
    {
        [SerializeField] string id = "property.unnamed";
        [SerializeField] string displayName = "Unnamed";
        [SerializeField] LocationDefinition location;
        [SerializeField, Min(0)] int purchasePrice = 4000;
        [SerializeField, Min(0)] int rentPerDay = 20;
        [SerializeField, Min(0)] int storageSlots = 40;
        [SerializeField] bool hasBed = true;

        public string Id => id;
        public string DisplayName => displayName;
        public LocationDefinition Location => location;
        public int PurchasePrice => purchasePrice;
        public int RentPerDay => rentPerDay;
        public int StorageSlots => storageSlots;
        public bool HasBed => hasBed;
    }
}
