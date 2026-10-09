using UnityEngine;

namespace EndlessDescent.Data
{
    public enum LocationKind { Settlement, Shop, Home, Guild, Wilderness, DungeonEntrance, Interior }

    [CreateAssetMenu(menuName = "Endless Descent/World/Location", fileName = "Location")]
    public class LocationDefinition : ScriptableObject
    {
        [SerializeField] string id = "loc.unnamed";
        [SerializeField] string displayName = "Unnamed";
        [SerializeField] LocationKind kind = LocationKind.Interior;
        [SerializeField] LocationDefinition parent;

        public string Id => id;
        public string DisplayName => displayName;
        public LocationKind Kind => kind;
        public LocationDefinition Parent => parent;
    }
}
