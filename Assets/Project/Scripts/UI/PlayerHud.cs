using EndlessDescent.Combat;
using EndlessDescent.Data;
using EndlessDescent.Core;
using EndlessDescent.Items;
using EndlessDescent.Player;
using EndlessDescent.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    [DisallowMultipleComponent]
    public class PlayerHud : MonoBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] PlayerStamina stamina;
        [SerializeField] SurvivalNeeds needs;
        [SerializeField] Wallet wallet;
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] EndlessDescent.Player.Mana mana;
        [SerializeField] EndlessDescent.Player.Spellcasting spellcasting;
        [SerializeField] EndlessDescent.Player.AfflictionTracker afflictions;
        [SerializeField] EndlessDescent.Player.PlayerStance stance;

        [Header("Widgets")]
        [SerializeField] Slider healthBar;
        [SerializeField] Slider staminaBar;
        [SerializeField] Slider hungerBar;
        [SerializeField] Slider fatigueBar;
        [SerializeField] Slider manaBar;
        [SerializeField] Text goldLabel;
        [SerializeField] Text clockLabel;
        [SerializeField] Text promptLabel;
        [SerializeField] Text spellLabel;
        [SerializeField] Text afflictionLabel;
        [SerializeField] Image damageFlash;

        void OnEnable()
        {
            if (health != null) health.Changed += OnHealthChanged;
            if (health != null) health.Damaged += OnDamaged;
            if (stamina != null) stamina.Changed += OnStaminaChanged;
            if (needs != null) needs.Changed += OnNeedsChanged;
            if (wallet != null) wallet.Changed += OnGoldChanged;
            if (interactor != null) interactor.PromptChanged += OnPromptChanged;
            if (afflictions != null) afflictions.Changed += ShowAfflictions;

            ShowAfflictions();
        }

        void OnDisable()
        {
            if (health != null) health.Changed -= OnHealthChanged;
            if (health != null) health.Damaged -= OnDamaged;
            if (stamina != null) stamina.Changed -= OnStaminaChanged;
            if (needs != null) needs.Changed -= OnNeedsChanged;
            if (wallet != null) wallet.Changed -= OnGoldChanged;
            if (interactor != null) interactor.PromptChanged -= OnPromptChanged;
            if (afflictions != null) afflictions.Changed -= ShowAfflictions;
        }

        float hurtUntil;

        // What the labels last said. Each string was built and assigned every frame, which is garbage
        // and a canvas rebuild sixty times a second for text that changes once a minute
        long shownMinute = -1;
        SpellDefinition shownSpell;
        int shownStance = -1;

        // Being hit needs to be legible without watching the health bar, so the screen answers
        void OnDamaged(DamageInfo info)
        {
            if (info.Amount > 0)
                hurtUntil = Time.time + 0.35f;
        }

        void Update()
        {
            if (damageFlash != null && (damageFlash.enabled || Time.time < hurtUntil))
            {
                float remaining = Mathf.Max(0f, hurtUntil - Time.time);
                Color colour = damageFlash.color;
                colour.a = remaining / 0.35f * 0.45f;
                damageFlash.color = colour;
                damageFlash.enabled = remaining > 0f;
            }

            WorldClock clock = WorldClock.Instance;
            long minute = clock != null ? (long)clock.TotalMinutes : -1;

            if (clockLabel != null && minute != shownMinute)
            {
                shownMinute = minute;
                clockLabel.text = clock != null ? $"Day {clock.Day}  {clock.Hour:00}:{clock.Minute:00}" : string.Empty;
            }

            if (mana != null && manaBar != null)
                manaBar.value = mana.Normalized;

            // What the left mouse button will do right now, because that is the thing the player
            // has to know and cannot see anywhere else
            SpellDefinition spell = spellcasting != null ? spellcasting.Selected : null;
            bool casting = stance != null && stance.IsCasting;

            if (spellLabel != null && (spell != shownSpell || (casting ? 1 : 0) != shownStance))
            {
                shownSpell = spell;
                shownStance = casting ? 1 : 0;

                spellLabel.text = casting
                    ? $"SPELL  {(spell != null ? spell.DisplayName : "none")}" +
                      $"{(spell != null ? $"  ({spell.ManaCost} magicka)" : string.Empty)}   [F] next   [R] weapon"
                    : "WEAPON   [R] spells";

                spellLabel.color = casting ? new Color(0.6f, 0.72f, 1f) : new Color(0.9f, 0.86f, 0.72f);
            }
        }

        void ShowAfflictions()
        {
            if (afflictions == null || afflictionLabel == null)
                return;

            string text = string.Empty;
            foreach (System.Collections.Generic.KeyValuePair<EndlessDescent.Core.AfflictionId, float> pair in afflictions.Active)
                text += (text.Length > 0 ? ", " : string.Empty) + Afflictions.Describe(pair.Key);

            afflictionLabel.text = text;
            afflictionLabel.color = new Color(0.9f, 0.5f, 0.45f);
        }

        void OnHealthChanged(int current, int max) => SetBar(healthBar, max > 0 ? current / (float)max : 0f);
        void OnStaminaChanged(float current, float max) => SetBar(staminaBar, max > 0f ? current / max : 0f);
        void OnGoldChanged(int gold) { if (goldLabel != null) goldLabel.text = gold.ToString(); }
        void OnPromptChanged(string prompt) { if (promptLabel != null) promptLabel.text = prompt; }

        void OnNeedsChanged(float hunger, float fatigue)
        {
            SetBar(hungerBar, hunger / 100f);
            SetBar(fatigueBar, fatigue / 100f);
        }

        static void SetBar(Slider bar, float normalized)
        {
            if (bar != null)
                bar.value = Mathf.Clamp01(normalized);
        }
    }
}
