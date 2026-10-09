using EndlessDescent.Combat;
using EndlessDescent.Data;
using EndlessDescent.Items;
using UnityEngine;

namespace EndlessDescent.Player
{
    // The one place the sheet's derived numbers are pushed into the components that use them, so
    // health, magicka, stamina and carry weight cannot drift apart from the attributes behind them
    [DisallowMultipleComponent]
    public class CharacterStatsBinder : MonoBehaviour
    {
        [SerializeField] CharacterSheet sheet;
        [SerializeField] Health health;
        [SerializeField] Mana mana;
        [SerializeField] PlayerStamina stamina;
        [SerializeField] Inventory inventory;

        void OnEnable()
        {
            if (sheet == null)
                return;

            sheet.Changed += Apply;
            sheet.LevelledUp += OnLevelled;
            Apply();
        }

        void OnDisable()
        {
            if (sheet == null)
                return;

            sheet.Changed -= Apply;
            sheet.LevelledUp -= OnLevelled;
        }

        void OnLevelled(int level)
        {
            Apply();
            health?.Heal(sheet.MaxHealth);
        }

        void Apply()
        {
            health?.SetMax(sheet.MaxHealth);
            mana?.Configure(sheet.MaxMagicka);
            stamina?.Configure(sheet.MaxStamina);
            inventory?.SetCapacity(sheet.CarryWeight);
        }
    }
}
