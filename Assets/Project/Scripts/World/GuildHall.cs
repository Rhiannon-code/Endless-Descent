using System.Collections.Generic;
using EndlessDescent.Combat;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Items;
using EndlessDescent.Player;
using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.World
{
    // Joining, then whatever your rank has unlocked, so a hall is a reason to keep a faction happy
    [DisallowMultipleComponent]
    public class GuildHall : MonoBehaviour, IInteractable
    {
        [SerializeField] FactionDefinition guild;
        [SerializeField] SkillId trains = SkillId.Restoration;
        [SerializeField, Min(1)] int trainingGold = 40;
        [SerializeField, Min(1)] int healingGold = 25;
        [SerializeField, Min(0)] int trainingHours = 4;

        public string Prompt => guild == null ? "Empty hall" : $"The {guild.DisplayName}";

        public bool CanInteract(GameObject actor) => guild != null;

        public void Interact(GameObject actor)
        {
            GuildMembership membership = FindFirstObjectByType<GuildMembership>();

            if (membership != null)
                ServiceScreens.Current?.Open(Title(membership), () => Options(actor, membership));
        }

        string Title(GuildMembership membership) => membership.IsMember(guild)
            ? $"{guild.DisplayName}   ({membership.RankTitle(guild)})"
            : guild.DisplayName;

        IReadOnlyList<ServiceOption> Options(GameObject actor, GuildMembership membership)
        {
            List<ServiceOption> options = new List<ServiceOption>();

            if (!membership.IsMember(guild))
            {
                bool welcome = membership.CanJoin(guild);

                options.Add(new ServiceOption
                {
                    Text = welcome ? $"Join the {guild.DisplayName}" : $"The {guild.DisplayName} will not have you yet",
                    Available = welcome,
                    Choose = () => membership.Join(guild) ? $"Joined the {guild.DisplayName}." : "They turn you away."
                });

                return options;
            }

            Wallet wallet = actor.GetComponentInParent<Wallet>();
            Health health = actor.GetComponentInParent<Health>();
            Equipment equipment = actor.GetComponentInParent<Equipment>();
            AfflictionTracker afflictions = actor.GetComponentInParent<AfflictionTracker>();

            bool Afford(int gold) => wallet != null && wallet.CanAfford(gold);
            bool unwell = (health != null && health.Current < health.Max) ||
                          (afflictions != null && afflictions.Active.Count > 0);

            Offer(options, membership, GuildService.Healing, $"Healing and cure   {healingGold} gold",
                Afford(healingGold) && unwell, () => Heal(actor, wallet, health));

            Offer(options, membership, GuildService.Training, $"Train {trains}   {trainingGold} gold, {trainingHours}h",
                Afford(trainingGold), () => Train(actor, wallet));

            Offer(options, membership, GuildService.Repair, "Repair your equipment   free",
                equipment != null && equipment.MissingCondition > 0,
                () => equipment.RepairAll(0, _ => true, out _) ? "Your equipment is repaired." : "Nothing needs it.");

            return options;
        }

        // Only what this hall offers at all is listed, and what your rank does not reach yet is shown
        // but not offered, so there is something to work towards
        void Offer(List<ServiceOption> options, GuildMembership membership, GuildService service, string text,
            bool possible, System.Func<string> choose)
        {
            if (guild.RankRequiredFor(service) == int.MaxValue)
                return;

            bool ranked = membership.CanUse(guild, service);

            options.Add(new ServiceOption
            {
                Text = ranked ? text : $"{text}   (your rank is too low)",
                Available = ranked && possible,
                Choose = choose
            });
        }

        string Heal(GameObject actor, Wallet wallet, Health health)
        {
            if (wallet == null || !wallet.TrySpend(healingGold))
                return $"Healing costs {healingGold} gold.";

            health?.Heal(health.Max);

            // Healing that leaves the disease behind is not healing
            actor.GetComponentInParent<AfflictionTracker>()?.CureAll();
            return $"Healed and cured by the {guild.DisplayName}.";
        }

        string Train(GameObject actor, Wallet wallet)
        {
            if (wallet == null || !wallet.TrySpend(trainingGold))
                return $"Training costs {trainingGold} gold.";

            CharacterSheet sheet = actor.GetComponentInParent<CharacterSheet>();
            sheet?.Train(trains);
            WorldClock.Instance?.Skip(trainingHours * WorldClock.MinutesPerHour);

            return $"Trained {trains} to {(sheet != null ? sheet.Skill(trains) : 0)}.";
        }
    }
}
