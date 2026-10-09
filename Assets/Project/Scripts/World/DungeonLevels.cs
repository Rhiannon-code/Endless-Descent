using System.Collections;
using EndlessDescent.Core;
using EndlessDescent.Dungeons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EndlessDescent.World
{
    // A dungeon is filled a level at a time, as the player reaches it. Populating a great dungeon in
    // one pass is several hundred enemies, their loot and their traps instantiated before anybody has
    // walked down a corridor
    [DisallowMultipleComponent]
    public class DungeonLevels : MonoBehaviour
    {
        [SerializeField] PlaceLoader place;
        [SerializeField] DungeonGenerator dungeons;
        [SerializeField] DungeonPopulator populator;
        [SerializeField, Min(0.1f)] float interval = 0.5f;
        [SerializeField, Min(6f)] float budgetMs = 6f;

        float next;
        Coroutine filling;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            Attach();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        // On whatever object holds the generator, so no scene needs rebuilding for it
        static void Attach()
        {
            DungeonGenerator generator = FindFirstObjectByType<DungeonGenerator>();

            if (generator != null && generator.GetComponent<DungeonLevels>() == null)
                generator.gameObject.AddComponent<DungeonLevels>();
        }

        void Start()
        {
            if (place == null) place = FindFirstObjectByType<PlaceLoader>();
            if (dungeons == null) dungeons = FindFirstObjectByType<DungeonGenerator>();
            if (populator == null) populator = FindFirstObjectByType<DungeonPopulator>();
        }

        void Update()
        {
            if (place == null || dungeons == null || populator == null || filling != null)
                return;

            if (!place.Inside || dungeons.Current == null || place.Player == null || Time.time < next)
                return;

            next = Time.time + interval;

            float below = dungeons.Current.Root.transform.position.y - place.Player.position.y;
            int level = Mathf.Clamp(Mathf.RoundToInt(below / DungeonGridLayout.Storey), 0,
                Mathf.Max(0, dungeons.Current.LevelCount - 1));

            filling = StartCoroutine(Fill(level));
        }

        // The level they are on and the one below it, so the way down is never into an empty floor
        IEnumerator Fill(int level)
        {
            try
            {
                for (int at = level; at <= level + 1; at++)
                {
                    IEnumerator work = Steps.Budgeted(populator.PopulatingLevel(at).GetEnumerator(), budgetMs);

                    while (work.MoveNext())
                        yield return work.Current;
                }
            }
            finally
            {
                // However it ended, a dungeon cleared under it included, the next floor can still fill
                filling = null;
            }
        }
    }
}
