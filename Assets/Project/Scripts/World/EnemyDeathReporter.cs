using EndlessDescent.Combat;
using EndlessDescent.Quests;
using UnityEngine;

namespace EndlessDescent.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class EnemyDeathReporter : MonoBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] EnemyBrain brain;
        [SerializeField] QuestJournal journal;
        [SerializeField] float despawnDelay = 6f;

        void Awake()
        {
            if (health == null)
                health = GetComponent<Health>();

            if (brain == null)
                brain = GetComponent<EnemyBrain>();

            // Enemies are instantiated as a dungeon is populated, so the journal is found rather than
            // wired, without it nothing a kill objective asks for ever counts
            if (journal == null)
                journal = FindFirstObjectByType<QuestJournal>();

            health.Died += OnDied;
        }

        void OnDestroy()
        {
            if (health != null)
                health.Died -= OnDied;
        }

        void OnDied()
        {
            if (journal != null && brain != null)
                journal.ReportKill(brain.Definition);

            if (despawnDelay > 0f)
                Destroy(gameObject, despawnDelay);
        }
    }
}
