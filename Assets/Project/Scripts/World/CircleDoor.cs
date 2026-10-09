using EndlessDescent.Combat;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Items;
using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.World
{
    // How a circle sets its test. Each is something the game can already answer honestly, the six
    // tests that need conversation, escort or a province crossing errand are not here, and a circle
    // without one of these gets no door rather than a faked one
    public enum CircleTest
    {
        NeverKilledWithMagic,
        BringSomething,
        TakePart
    }

    // A door that is not marked, belonging to one of the nine circles. Knocking is the test, it is
    // asked and answered in one action, and getting it wrong shuts this circle for the rest of the
    // game. That is the point, there are nine doors and no appeals
    [DisallowMultipleComponent]
    public class CircleDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] FactionDefinition circle;
        [SerializeField] string contact = "someone";
        [SerializeField] CircleTest test = CircleTest.NeverKilledWithMagic;

        [Header("BringSomething")]
        [SerializeField] ItemDefinition wanted;

        GuildMembership found;
        GuildMembership Membership => found != null ? found : found = FindFirstObjectByType<GuildMembership>();

        public string Prompt
        {
            get
            {
                if (circle == null)
                    return "A door";

                GuildMembership membership = Membership;

                if (membership == null)
                    return "A door";

                if (membership.IsMember(circle))
                    return $"{circle.DisplayName} : {contact}";

                if (membership.IsRefused(circle))
                    return "The door is not answered";

                return Asking;
            }
        }

        string Asking
        {
            get
            {
                switch (test)
                {
                    case CircleTest.NeverKilledWithMagic:
                        return $"Knock. {contact} will ask you one question";
                    case CircleTest.BringSomething:
                        return wanted != null
                            ? $"Knock. {contact} takes only what they do not have"
                            : "Knock";
                    default:
                        return $"Knock. {contact} will invite you to take part";
                }
            }
        }

        public bool CanInteract(GameObject actor) => circle != null;

        public void Interact(GameObject actor)
        {
            GuildMembership membership = Membership;
            if (membership == null || membership.IsMember(circle) || membership.IsRefused(circle))
                return;

            if (Passes(actor))
            {
                membership.Admit(circle);
                Notice.Show($"{contact} steps aside. You are of the {circle.DisplayName}.");
                return;
            }

            membership.Refuse(circle);
            Notice.Show($"{contact} closes the door. The {circle.DisplayName} will not open it again.");
        }

        bool Passes(GameObject actor)
        {
            switch (test)
            {
                case CircleTest.NeverKilledWithMagic:
                    SpellRecord record = actor != null ? actor.GetComponentInParent<SpellRecord>() : null;
                    return record == null || !record.HasKilledWithMagic;

                case CircleTest.BringSomething:
                    Inventory bag = actor != null ? actor.GetComponentInParent<Inventory>() : null;
                    return wanted != null && bag != null && bag.Remove(wanted, 1);

                // Agreeing is the failure. The player is admitted to the circle the other eight would
                // inform on, which is what agreeing costs
                default:
                    return true;
            }
        }
    }
}
