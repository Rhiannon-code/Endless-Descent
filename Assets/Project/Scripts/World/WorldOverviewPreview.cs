using UnityEngine;

namespace EndlessDescent.World
{
    // The coarse whole empire bake is scenery for the editor, something to look at and to edit by
    // hand. At play time the streamer lays the same ground down at thirty times the resolution and
    // the two interpenetrate, so this stands aside the moment the game runs
    [DisallowMultipleComponent]
    public class WorldOverviewPreview : MonoBehaviour
    {
        void Awake() => gameObject.SetActive(false);
    }
}
