using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EndlessDescent.World
{
    // Tells the journal which place the player is standing at, which is what finishes a "reach"
    // objective. The trigger volume meant to do it was never placed anywhere, so no reach objective in
    // the game could be completed
    [DisallowMultipleComponent]
    public class PlaceArrival : MonoBehaviour
    {
        const float Interval = 0.5f;

        // A town is reached on its streets, a dungeon at its door
        const float TownKm = 0.4f;
        const float DungeonKm = 0.1f;

        PlaceLoader place;
        QuestJournal journal;
        float next;

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
            PlaceLoader loader = FindFirstObjectByType<PlaceLoader>();

            if (loader != null && loader.GetComponent<PlaceArrival>() == null)
                loader.gameObject.AddComponent<PlaceArrival>();
        }

        void Awake() => place = GetComponent<PlaceLoader>();

        void Update()
        {
            if (Time.time < next || Transition.Busy)
                return;

            next = Time.time + Interval;

            if (journal == null)
                journal = FindFirstObjectByType<QuestJournal>();

            string here = Here();

            if (journal != null && here != null)
                journal.ReportArrival(here);
        }

        string Here()
        {
            RegionMap region = place != null ? place.Region : null;

            if (region == null)
                return null;

            if (place.Inside)
                return region.Has(place.PreparedDungeon) ? region.Locations[place.PreparedDungeon].DisplayName : null;

            Vector2 km = place.HereKm;

            for (int i = 0; i < region.Locations.Count; i++)
            {
                RegionLocation location = region.Locations[i];
                float reach = location.IsSettlement ? TownKm : DungeonKm;

                if (Vector2.Distance(km, location.Position) <= reach)
                    return location.DisplayName;
            }

            return null;
        }
    }
}
