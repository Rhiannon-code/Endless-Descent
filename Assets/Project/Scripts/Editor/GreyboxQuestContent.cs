using System.Collections.Generic;
using EndlessDescent.Data;
using UnityEditor;
using UnityEngine;

namespace EndlessDescent.EditorTools
{
    // The authored spine. Templated postings give a town work to offer; this gives the
    // demo something to follow from one end to the other, and it is the only content that exercises
    // deliver, pay, stage entry consequences, deadlines and failure at all
    public static class GreyboxQuestContent
    {
        public class Stage
        {
            public string Title;
            public string Summary;
            public readonly List<QuestObjectiveData> Objectives = new List<QuestObjectiveData>();
            public readonly List<QuestConsequenceData> OnEnter = new List<QuestConsequenceData>();

            public Stage Kill(EnemyDefinition enemy, int count, string text)
            {
                Objectives.Add(new QuestObjectiveData
                {
                    Kind = QuestObjectiveKind.KillEnemy, Enemy = enemy, Count = count, Description = text
                });
                return this;
            }

            public Stage Collect(ItemDefinition item, int count, string text)
            {
                Objectives.Add(new QuestObjectiveData
                {
                    Kind = QuestObjectiveKind.CollectItem, Item = item, Count = count, Description = text
                });
                return this;
            }

            public Stage Deliver(ItemDefinition item, int count, NpcDefinition to, string text)
            {
                Objectives.Add(new QuestObjectiveData
                {
                    Kind = QuestObjectiveKind.DeliverItem, Item = item, Count = count, Npc = to, Description = text
                });
                return this;
            }

            public Stage Pay(int gold, NpcDefinition to, string text)
            {
                Objectives.Add(new QuestObjectiveData
                {
                    Kind = QuestObjectiveKind.PayGold, Count = gold, Npc = to, Description = text
                });
                return this;
            }

            public Stage Talk(NpcDefinition npc, string text)
            {
                Objectives.Add(new QuestObjectiveData
                {
                    Kind = QuestObjectiveKind.TalkToNpc, Npc = npc, Count = 1, Description = text
                });
                return this;
            }

            public Stage Reach(LocationDefinition place, string text)
            {
                Objectives.Add(new QuestObjectiveData
                {
                    Kind = QuestObjectiveKind.ReachLocation, Location = place, Count = 1, Description = text
                });
                return this;
            }

            // Naming the dungeon means only that one counts. With none, any boss would clear every
            // quest that asked for a clearing at once
            public Stage Clear(LocationDefinition dungeon, string text)
            {
                Objectives.Add(new QuestObjectiveData
                {
                    Kind = QuestObjectiveKind.ClearDungeon, Location = dungeon, Count = 1, Description = text
                });
                return this;
            }

            public Stage Gives(int gold)
            {
                OnEnter.Add(new QuestConsequenceData { Kind = QuestConsequenceKind.GiveGold, Amount = gold });
                return this;
            }

            public Stage Hands(ItemDefinition item, int count)
            {
                OnEnter.Add(new QuestConsequenceData
                {
                    Kind = QuestConsequenceKind.GiveItem, Item = item, Amount = count
                });
                return this;
            }

            public Stage Says(NpcDefinition npc, string situation)
            {
                OnEnter.Add(new QuestConsequenceData
                {
                    Kind = QuestConsequenceKind.SetNpcSituation, Npc = npc, Text = situation
                });
                return this;
            }
        }

        public static AuthoredQuestDefinition[] BuildActOne(NpcDefinition clerk, NpcDefinition warden,
            FactionDefinition guild, LocationDefinition undercroft, LocationDefinition crypt, ItemDefinition dust,
            ItemDefinition relic,
            EnemyDefinition skeleton)
        {
            List<AuthoredQuestDefinition> built = new List<AuthoredQuestDefinition>();

            built.Add(Quest("Quest_LampWentOut", "quest.act1.lamp", "The Lamp Went Out", clerk, guild,
                "The undercroft lamp has burned every night for eleven years and it went out on " +
                "Tuesday. Nobody who went to relight it has come back up.",
                "The clerk is not telling you that the last one down was his brother.",
                0, new[]
                {
                    new Stage
                    {
                        Title = "Get down there",
                        Summary = "The Undercroft is south-east of Ashmere, most of a day on foot. Find it and go in."
                    }.Reach(undercroft, "Reach the Undercroft, south-east of Ashmere"),

                    new Stage
                    {
                        Title = "Find out what is in it",
                        Summary = "Whatever put the lamp out is still down there."
                    }.Clear(undercroft, "Deal with what is in the Undercroft"),

                    new Stage
                    {
                        Title = "Tell him",
                        Summary = "He has been waiting at the top of the stair since Tuesday."
                    }.Talk(clerk, $"Tell {clerk.DisplayName} what you found")
                },
                Rewards(guild, 120, 8),
                null,
                next: "quest.act1.ledger"));

            built.Add(Quest("Quest_WhatTheLedgerSays", "quest.act1.ledger", "What the Ledger Says", clerk, guild,
                "The burial register for the undercroft is eleven years out of date, and the " +
                "Gravewardens will not release a body without one.",
                "Bringing the register up to date means naming who is down there, and one of the " +
                "names is going to be his brother's.",
                8, new[]
                {
                    new Stage
                    {
                        Title = "Ask the warden",
                        Summary = "Only the Gravewardens know what the register is supposed to say."
                    }.Talk(warden, $"Speak to {warden.DisplayName}"),

                    new Stage
                    {
                        Title = "Bring back what is left",
                        Summary = "Four names, and something of each of them to prove it."
                    }.Collect(dust, 4, "Collect 4 Bone Dust"),

                    new Stage
                    {
                        Title = "Hand it over",
                        Summary = "He will not read it in front of you."
                    }.Deliver(dust, 4, clerk, $"Take 4 Bone Dust to {clerk.DisplayName}")
                },
                Rewards(guild, 160, 10),
                Penalty(guild, -12),
                next: "quest.act1.wardens"));

            built.Add(Quest("Quest_PayingTheWardens", "quest.act1.wardens", "Paying the Wardens", warden, guild,
                "The rites cost what they cost, and the parish has not paid them since the lamp " +
                "went out. The Wardens are not a charity and have never pretended to be.",
                "The fee is being collected twice, and the warden taking it knows that.",
                12, new[]
                {
                    new Stage
                    {
                        Title = "The fee",
                        Summary = "Sixty gold, in his hand, before anything else happens."
                    }.Gives(60).Pay(60, warden, $"Pay {warden.DisplayName} 60 gold"),

                    new Stage
                    {
                        Title = "Clear the stair",
                        Summary = "The rites cannot be said over a stair with things still on it."
                    }.Kill(skeleton, 3, "Put down 3 of the risen"),

                    new Stage
                    {
                        Title = "Say so",
                        Summary = "He will want to hear it from you and not from the parish."
                    }.Says(clerk, "Knows the fee was paid, and is quiet about where it went.")
                     .Talk(warden, $"Report to {warden.DisplayName}")
                },
                Rewards(guild, 220, 12),
                Penalty(guild, -18),
                next: "quest.act1.register"));

            built.Add(Quest("Quest_NameOnTheRegister", "quest.act1.register", "The Name on the Register", clerk, guild,
                "The register is current for the first time in eleven years, and there is one name " +
                "on it that was written after the lamp went out.",
                "Somebody has been entering the dead before they died.",
                20, new[]
                {
                    new Stage
                    {
                        Title = "Ask him straight",
                        Summary = "He has had eleven years to think about what he would say."
                    }.Talk(clerk, $"Put it to {clerk.DisplayName}"),

                    new Stage
                    {
                        Title = "Go where the name points",
                        Summary = "Whoever wrote it did not expect anybody to go and look."
                    }.Reach(crypt, "Reach the Crypt, south-west of Ashmere"),

                    new Stage
                    {
                        Title = "Finish it",
                        Summary = "What is down there was put there on purpose."
                    }.Clear(crypt, "Clear what is waiting in the Crypt"),

                    new Stage
                    {
                        Title = "Bring it back",
                        Summary = "The Wardens will want the proof, not the story."
                    }.Deliver(relic, 1, warden, $"Take it to {warden.DisplayName}")
                },
                Rewards(guild, 500, 20),
                Penalty(guild, -25)));

            return built.ToArray();
        }

        static QuestConsequenceData[] Rewards(FactionDefinition guild, int gold, int standing) => new[]
        {
            new QuestConsequenceData { Kind = QuestConsequenceKind.GiveGold, Amount = gold },
            new QuestConsequenceData { Kind = QuestConsequenceKind.FactionStanding, Faction = guild, Amount = standing }
        };

        static QuestConsequenceData[] Penalty(FactionDefinition guild, int standing) => new[]
        {
            new QuestConsequenceData { Kind = QuestConsequenceKind.FactionStanding, Faction = guild, Amount = standing }
        };

        static AuthoredQuestDefinition Quest(string asset, string id, string title, NpcDefinition giver,
            FactionDefinition faction, string reason, string complication, int deadlineDays, Stage[] stages,
            QuestConsequenceData[] onComplete, QuestConsequenceData[] onFail, string next = null)
        {
            AuthoredQuestDefinition quest = GreyboxContentBuilder.Create<AuthoredQuestDefinition>(asset);
            AssetAuthoring author = new AssetAuthoring(quest);

            author.Str("id", id).Str("title", title)
                .Ref("giver", giver).Ref("faction", faction)
                .Str("reason", reason).Str("complication", complication)
                .Int("deadlineDays", deadlineDays)
                .Bool("failsIfGiverDies", true);

            author.Apply("stages", p =>
            {
                p.arraySize = stages.Length;

                for (int i = 0; i < stages.Length; i++)
                {
                    SerializedProperty stage = p.GetArrayElementAtIndex(i);
                    stage.FindPropertyRelative("Title").stringValue = stages[i].Title;
                    stage.FindPropertyRelative("Summary").stringValue = stages[i].Summary;

                    WriteObjectives(stage.FindPropertyRelative("Objectives"), stages[i].Objectives);
                    WriteConsequences(stage.FindPropertyRelative("OnEnter"), stages[i].OnEnter);
                }
            });

            // The chain is a consequence like any other, so finishing one posts the next without
            // anything having to watch for it
            List<QuestConsequenceData> completion = new List<QuestConsequenceData>(onComplete);

            if (!string.IsNullOrEmpty(next))
                completion.Add(new QuestConsequenceData { Kind = QuestConsequenceKind.StartQuest, QuestId = next });

            author.Apply("onComplete", p => WriteConsequences(p, completion));

            if (onFail != null)
                author.Apply("onFail", p => WriteConsequences(p, new List<QuestConsequenceData>(onFail)));

            author.Save();
            return GreyboxContentBuilder.Load<AuthoredQuestDefinition>(asset);
        }

        static void WriteObjectives(SerializedProperty list, List<QuestObjectiveData> objectives)
        {
            list.arraySize = objectives.Count;

            for (int i = 0; i < objectives.Count; i++)
            {
                QuestObjectiveData data = objectives[i];
                SerializedProperty entry = list.GetArrayElementAtIndex(i);

                entry.FindPropertyRelative("Kind").enumValueIndex = (int)data.Kind;
                entry.FindPropertyRelative("Description").stringValue = data.Description;
                entry.FindPropertyRelative("Item").objectReferenceValue = data.Item;
                entry.FindPropertyRelative("Npc").objectReferenceValue = data.Npc;
                entry.FindPropertyRelative("Location").objectReferenceValue = data.Location;
                entry.FindPropertyRelative("Enemy").objectReferenceValue = data.Enemy;
                entry.FindPropertyRelative("Count").intValue = Mathf.Max(1, data.Count);
            }
        }

        static void WriteConsequences(SerializedProperty list, List<QuestConsequenceData> consequences)
        {
            list.arraySize = consequences.Count;

            for (int i = 0; i < consequences.Count; i++)
            {
                QuestConsequenceData data = consequences[i];
                SerializedProperty entry = list.GetArrayElementAtIndex(i);

                entry.FindPropertyRelative("Kind").enumValueIndex = (int)data.Kind;
                entry.FindPropertyRelative("Faction").objectReferenceValue = data.Faction;
                entry.FindPropertyRelative("Item").objectReferenceValue = data.Item;
                entry.FindPropertyRelative("Npc").objectReferenceValue = data.Npc;
                entry.FindPropertyRelative("QuestId").stringValue = data.QuestId;
                entry.FindPropertyRelative("Text").stringValue = data.Text;
                entry.FindPropertyRelative("Amount").intValue = data.Amount;
            }
        }
    }
}
