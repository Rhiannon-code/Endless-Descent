using System.Collections.Generic;
using EndlessDescent.Data;
using UnityEditor;
using UnityEngine;

namespace EndlessDescent.EditorTools
{
    public static class GreyboxDialogueContent
    {
        public static DialogueDefinition[] Build(NpcDefinition clerk, NpcDefinition warden, FactionDefinition guild)
        {
            List<DialogueDefinition> built = new List<DialogueDefinition>
            {
                Clerk(clerk),
                Warden(warden, guild)
            };

            return built.ToArray();
        }

        static DialogueDefinition Clerk(NpcDefinition clerk)
        {
            DialogueLineData[] lines =
            {
                new DialogueLineData
                {
                    Id = "root",
                    Text = "{npc}, parish clerk. He has the register open and has not written in it.",
                    Choices = new[]
                    {
                        Go("What happened here?", "lamp"),
                        Offer("Is there work?", "quest.act1.lamp", "taken"),
                        TurnIn("About the undercroft.", "quest.act1.lamp", "thanks"),
                        Go("Who keeps the register?", "register"),
                        Leave("Good day.")
                    }
                },

                new DialogueLineData
                {
                    Id = "lamp",
                    Text = "The lamp in the Undercroft has burned every night for eleven years. It went " +
                           "out on Tuesday. Three went down to relight it. None came up. South-east of " +
                           "here, most of a day on foot, past the last of the fields.",
                    Choices = new[] { Go("And the parish did nothing?", "nothing") }
                },

                new DialogueLineData
                {
                    Id = "nothing",
                    Text = "The parish sent three. That was doing something.",
                    Choices = new[] { Go("...", "root") }
                },

                new DialogueLineData
                {
                    Id = "register",
                    Text = "I do. Eleven years out of date, and the Wardens will not release a body " +
                           "without it. You can see the difficulty.",
                    Choices = new[] { Go("I can.", "root") }
                },

                new DialogueLineData
                {
                    Id = "taken",
                    Text = "Then take a lamp of your own. The Undercroft is south-east of Ashmere, most " +
                           "of a day on foot. And do not go down there after dark, which is advice I am " +
                           "aware means nothing underground.",
                    Choices = new[] { Go("Understood.", "root") }
                },

                new DialogueLineData
                {
                    Id = "thanks",
                    Text = "You came back up. That already makes you the fourth and the only one.",
                    Choices = new[] { Go("There is more to say about it.", "root") }
                }
            };

            return Tree("Dialogue_Clerk", $"dialogue.{clerk.Id}", lines, clerk);
        }

        static DialogueDefinition Warden(NpcDefinition warden, FactionDefinition guild)
        {
            DialogueLineData[] lines =
            {
                new DialogueLineData
                {
                    Id = "root",
                    Text = "{npc} of the Gravewardens. She is counting something and does not stop to talk.",
                    Choices = new[]
                    {
                        Go("What do the Wardens want here?", "rites"),
                        Offer("Is there work?", "quest.act1.wardens", "fee"),
                        TurnIn("About the fee.", "quest.act1.wardens", "settled"),
                        Standing("We are on good terms, then.", "friend", guild, 25),
                        Leave("I will leave you to it.")
                    }
                },

                new DialogueLineData
                {
                    Id = "rites",
                    Text = "What we always want. The rites said, the fee paid, and the parish to stop " +
                           "pretending those are the same thing.",
                    Choices = new[] { Go("And if they are not paid?", "unpaid") }
                },

                new DialogueLineData
                {
                    Id = "unpaid",
                    Text = "Then the dead stay where they fell and we are blamed for it.",
                    Choices = new[] { Go("I see.", "root") }
                },

                new DialogueLineData
                {
                    Id = "fee",
                    Text = "Sixty. In my hand, not on the ledger. You will understand why before long.",
                    Choices = new[] { Go("Sixty it is.", "root") }
                },

                new DialogueLineData
                {
                    Id = "settled",
                    Text = "Paid twice and recorded once. Do not put that together out loud in here.",
                    Choices = new[] { Go("Noted.", "root") }
                },

                new DialogueLineData
                {
                    Id = "friend",
                    Text = "We are. Which is why I will say it plainly: the clerk's brother is on that " +
                           "register, and he wrote the entry himself.",
                    Choices = new[] { Go("Before or after?", "root") }
                }
            };

            return Tree("Dialogue_Warden", $"dialogue.{warden.Id}", lines, warden);
        }

        // Choice shapes
        static DialogueChoiceData Go(string text, string to) =>
            new DialogueChoiceData { Text = text, GoTo = to };

        static DialogueChoiceData Leave(string text) =>
            new DialogueChoiceData { Text = text, Action = DialogueAction.Leave };

        // Offered until it is taken and never again after it is done, without both conditions the
        // line sits there being chosen and doing nothing, which reads as a broken conversation
        static DialogueChoiceData Offer(string text, string questId, string to) => new DialogueChoiceData
        {
            Text = text,
            GoTo = to,
            Action = DialogueAction.OfferQuest,
            QuestId = questId,
            Requires = new[]
            {
                new QuestConditionData { Kind = QuestConditionKind.QuestNotActive, QuestId = questId },
                new QuestConditionData { Kind = QuestConditionKind.QuestNotCompleted, QuestId = questId }
            }
        };

        static DialogueChoiceData TurnIn(string text, string questId, string to) => new DialogueChoiceData
        {
            Text = text,
            GoTo = to,
            Action = DialogueAction.TurnInQuest,
            Requires = new[] { new QuestConditionData { Kind = QuestConditionKind.QuestActive, QuestId = questId } }
        };

        static DialogueChoiceData Standing(string text, string to, FactionDefinition faction, int at) =>
            new DialogueChoiceData
            {
                Text = text,
                GoTo = to,
                Requires = new[]
                {
                    new QuestConditionData
                    {
                        Kind = QuestConditionKind.MinFactionStanding, Faction = faction, Amount = at
                    }
                }
            };

        static DialogueDefinition Tree(string asset, string id, DialogueLineData[] lines, NpcDefinition owner)
        {
            DialogueDefinition tree = GreyboxContentBuilder.Create<DialogueDefinition>(asset);
            tree.Configure(id, "root", lines);

            if (!tree.IsWellFormed(out string problem))
                Debug.LogError($"Authored dialogue is broken: {problem}");

            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssetIfDirty(tree);

            DialogueDefinition saved = GreyboxContentBuilder.Load<DialogueDefinition>(asset);
            new AssetAuthoring(owner).Ref("dialogue", saved).Save();
            return saved;
        }
    }
}
