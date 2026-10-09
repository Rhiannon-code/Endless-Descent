using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Quests;
using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.World
{
    // Talking to somebody used to be one key press that silently started whichever quest would
    // start. It opens a conversation now, the tree is theirs if they have one, and made out of what
    // is true about them if they do not
    [DisallowMultipleComponent]
    public class NpcDialogue : MonoBehaviour, IInteractable
    {
        [SerializeField] NpcActor actor;
        [SerializeField] QuestJournal journal;
        [SerializeField] AuthoredQuestDefinition[] offers;
        [SerializeField] QuestTemplateDefinition[] templates;
        [SerializeField] int generatedQuestSeed = 1;

        [SerializeField] FactionRegistry factions;
        [SerializeField] CrimeRecord crimes;

        DialogueDefinition generated;

        public string Prompt => actor != null ? $"Talk to {actor.DisplayName} ({Mood()})" : "Talk";

        // Reputation is read at the moment of speaking rather than stored, so a favour done five
        // minutes ago changes the greeting immediately
        string Mood()
        {
            if (crimes != null && crimes.IsWanted)
                return "wary of you";

            FactionDefinition faction = actor != null && actor.Definition != null
                ? actor.Definition.Faction
                : null;

            int standing = factions != null && faction != null ? factions.StandingOf(faction) : 0;

            if (standing >= 60) return "warm";
            if (standing >= 20) return "friendly";
            if (standing > -20) return "neutral";
            if (standing > -60) return "cold";
            return "hostile";
        }

        public bool CanInteract(GameObject gameObjectActor) => actor != null && actor.IsAlive;

        void Start()
        {
            // Townspeople are built with the settlement rather than placed in the scene, so what they
            // need is found rather than wired
            if (actor == null) actor = GetComponent<NpcActor>();
            if (journal == null) journal = FindFirstObjectByType<QuestJournal>();
            if (factions == null) factions = FindFirstObjectByType<FactionRegistry>();
            if (crimes == null) crimes = FindFirstObjectByType<CrimeRecord>();

            if ((templates == null || templates.Length == 0) && journal != null && journal.Database != null)
            {
                IReadOnlyList<QuestTemplateDefinition> known = journal.Database.QuestTemplates;

                if (known != null)
                {
                    templates = new QuestTemplateDefinition[known.Count];
                    for (int i = 0; i < known.Count; i++) templates[i] = known[i];
                }
            }
        }

        public void Interact(GameObject gameObjectActor)
        {
            if (journal == null || actor == null)
                return;

            // Walking up to somebody is still what finishes a "speak to" objective and what puts a
            // parcel in their hands, the conversation is where anything you have to decide happens
            journal.ReportTalk(actor.Definition);

            DialogueDefinition tree = actor.Definition != null ? actor.Definition.Dialogue : null;

            if (tree == null)
                tree = Generate();

            DialogueScreens.Current?.Open(new DialogueRunner(tree, actor, journal.Context));
        }

        // Made fresh each time, because what there is to say depends on what is true right now,
        // and destroyed each time, because a ScriptableObject made at runtime is not collected with
        // the conversation that used it
        DialogueDefinition Generate()
        {
            if (generated != null)
                Destroy(generated);

            generated = GeneratedDialogue.For(actor, journal.Context, FindOffer(), GetComponent<Merchant>() != null);
            return generated;
        }

        void OnDestroy()
        {
            if (generated != null)
                Destroy(generated);
        }

        // What this person would offer if asked, read by the F4 test marker. A written conversation
        // offers its work through its own choices; anybody else offers what FindOffer finds
        public string OfferedTitle()
        {
            if (journal == null || actor == null || !actor.IsAlive)
                return null;

            DialogueDefinition tree = actor.Definition != null ? actor.Definition.Dialogue : null;

            if (tree == null)
                return FindOffer()?.Title;

            foreach (DialogueLineData line in tree.Lines)
            {
                if (line.Choices == null)
                    continue;

                foreach (DialogueChoiceData choice in line.Choices)
                {
                    if (choice.Action != DialogueAction.OfferQuest || journal.Database == null)
                        continue;

                    AuthoredQuestDefinition quest = journal.Database.Quest(choice.QuestId);

                    if (journal.CanOffer(quest))
                        return quest.Title;
                }
            }

            return null;
        }

        // A new piece of work each week, as the board turns over. The seed was fixed, so each person had
        // exactly one job of each kind to give in the whole game, and once it was done never another
        const int DaysPerOffer = 7;

        int OfferSeed => generatedQuestSeed + (WorldClock.Instance != null ? WorldClock.Instance.Day / DaysPerOffer : 0) * 104729;

        // The first thing this person could credibly ask for, shown but not taken. An authored quest
        // outranks a generated one, it was written for them
        Quest FindOffer()
        {
            if (offers != null)
            {
                foreach (AuthoredQuestDefinition offer in offers)
                    if (journal.CanOffer(offer)) return Quest.FromAuthored(offer);
            }

            if (templates == null)
                return null;

            foreach (QuestTemplateDefinition template in templates)
            {
                Quest quest = journal.Offer(template, actor, OfferSeed);
                if (quest != null)
                    return quest;
            }

            return null;
        }
    }
}
