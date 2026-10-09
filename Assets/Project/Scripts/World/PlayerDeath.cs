using EndlessDescent.Combat;
using EndlessDescent.Core;
using EndlessDescent.Items;
using EndlessDescent.Player;
using UnityEngine;

namespace EndlessDescent.World
{
    // Dying used to do nothing at all. You wake somewhere safe, a day later, lighter in the purse. Not
    // Daggerfall's way, there death is a funeral and a reload; this is a deliberate difference
    [DisallowMultipleComponent]
    public class PlayerDeath : MonoBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] Wallet wallet;
        [SerializeField] CharacterController controller;
        [SerializeField] EndlessDescent.Simulation.CrimeRecord crimes;
        [SerializeField, Range(0f, 1f)] float goldLost = 0.5f;
        [SerializeField] int hoursUnconscious = 24;

        void OnEnable()
        {
            if (health == null)
                health = GetComponent<Health>();

            if (health != null)
                health.Died += OnDied;
        }

        void OnDisable()
        {
            if (health != null)
                health.Died -= OnDied;
        }

        void OnDied()
        {
            int lost = wallet != null ? Mathf.RoundToInt(wallet.Gold * goldLost) : 0;
            wallet?.TrySpend(lost);

            WorldClock.Instance?.Skip(hoursUnconscious * WorldClock.MinutesPerHour);

            // Back to the nearest town rather than a reload: the world has moved on without you
            // Never a teleport to a coordinate, with a floating origin, the scene's origin
            // is under the terrain wherever you happen to be standing
            PlaceLoader place = FindFirstObjectByType<PlaceLoader>();

            if (place != null)
            {
                place.RespawnWhenFree();
            }
            else if (controller != null)
            {
                // A playtest scene with no loader, the whole scene is one place, so its origin is safe
                controller.enabled = false;
                transform.position = new Vector3(0f, 1.2f, 0f);
                controller.enabled = true;
            }

            health.Configure(health.Max);
            health.Heal(health.Max);
            crimes?.Clear();

            Notice.Show($"You wake a day later, {lost} gold lighter.");
        }
    }
}
