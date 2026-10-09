using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Items;
using EndlessDescent.Player;
using UnityEngine;

namespace EndlessDescent.World
{
    // Buys a skill point outright. Daggerfall gates trainers by how good they are and charges more
    // the better you already are, so training is an early leg up rather than a route to 100
    [DisallowMultipleComponent]
    public class Trainer : MonoBehaviour, IInteractable
    {
        [SerializeField] SkillId skill = SkillId.Longsword;
        [SerializeField, Min(1)] int teachesUpTo = 50;
        [SerializeField, Min(1)] int goldPerPoint = 6;
        [SerializeField, Min(0)] int hoursPerSession = 4;

        public string Prompt => $"Trainer: {skill}";

        int Cost(int level) => goldPerPoint * Mathf.Max(1, level);

        public bool CanInteract(GameObject actor) => actor.GetComponentInParent<CharacterSheet>() != null;

        public void Interact(GameObject actor) =>
            ServiceScreens.Current?.Open($"Trainer: {skill}", () => Options(actor));

        IReadOnlyList<ServiceOption> Options(GameObject actor)
        {
            CharacterSheet sheet = actor.GetComponentInParent<CharacterSheet>();
            Wallet wallet = actor.GetComponentInParent<Wallet>();
            int level = sheet != null ? sheet.Skill(skill) : 0;

            if (level >= teachesUpTo)
                return new[] { new ServiceOption { Text = $"{skill} {level}: nothing left to teach you", Available = false } };

            return new[]
            {
                new ServiceOption
                {
                    Text = $"Train {skill} {level} to {level + 1}   {Cost(level)} gold, {hoursPerSession}h",
                    Available = wallet != null && wallet.CanAfford(Cost(level)),
                    Choose = () => Train(sheet, wallet)
                }
            };
        }

        string Train(CharacterSheet sheet, Wallet wallet)
        {
            if (sheet == null)
                return string.Empty;

            int level = sheet.Skill(skill);

            if (level >= teachesUpTo)
                return $"{skill} is already past what this trainer can teach.";

            if (wallet == null || !wallet.TrySpend(Cost(level)))
                return $"Training {skill} costs {Cost(level)} gold.";

            sheet.Train(skill);
            WorldClock.Instance?.Skip(hoursPerSession * WorldClock.MinutesPerHour);

            return $"Trained {skill} to {sheet.Skill(skill)}.";
        }
    }
}
