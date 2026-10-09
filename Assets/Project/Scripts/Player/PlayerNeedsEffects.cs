using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.Player
{
    [DisallowMultipleComponent]
    public class PlayerNeedsEffects : MonoBehaviour
    {
        [SerializeField] SurvivalNeeds needs;
        [SerializeField] PlayerStamina stamina;

        void OnEnable()
        {
            if (needs != null)
                needs.Changed += OnNeedsChanged;

            Apply();
        }

        void OnDisable()
        {
            if (needs != null)
                needs.Changed -= OnNeedsChanged;
        }

        void OnNeedsChanged(float hunger, float fatigue) => Apply();

        void Apply()
        {
            if (needs != null && stamina != null)
                stamina.SetMultipliers(needs.StaminaCapacityMultiplier, needs.StaminaRegenMultiplier);
        }
    }
}
