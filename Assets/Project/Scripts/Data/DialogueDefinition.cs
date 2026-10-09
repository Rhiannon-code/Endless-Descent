using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    // What a choice does besides move the conversation on. Anything the world already knows how to
    // do is a QuestConsequence; these are the four that need a screen of their own
    public enum DialogueAction { None, OfferQuest, TurnInQuest, OpenTrade, Leave }

    [Serializable]
    public class DialogueChoiceData
    {
        [TextArea] public string Text = "...";
        public string GoTo;

        // Tested against live world state with exactly the machinery quests use, so a line that is
        // only true when you are in a guild, carrying something or out after dark costs nothing new
        public QuestConditionData[] Requires;
        public QuestConsequenceData[] Effects;

        public DialogueAction Action = DialogueAction.None;
        public string QuestId;

        public bool Ends => Action == DialogueAction.Leave || string.IsNullOrEmpty(GoTo);
    }

    [Serializable]
    public class DialogueLineData
    {
        public string Id = "root";
        [TextArea] public string Text;
        public QuestConditionData[] Requires;
        public DialogueChoiceData[] Choices;
    }

    // Tokens: {npc} {player} {town} {situation}
    [CreateAssetMenu(menuName = "Endless Descent/World/Dialogue", fileName = "Dialogue")]
    public class DialogueDefinition : ScriptableObject
    {
        [SerializeField] string id = "dialogue.unnamed";
        [SerializeField] string rootId = "root";
        [SerializeField] DialogueLineData[] lines;

        public string Id => id;
        public string RootId => rootId;
        public IReadOnlyList<DialogueLineData> Lines => lines;

        public DialogueLineData Line(string lineId)
        {
            if (lines == null || string.IsNullOrEmpty(lineId))
                return null;

            foreach (DialogueLineData line in lines)
                if (line != null && line.Id == lineId) return line;

            return null;
        }

        public void Configure(string newId, string root, DialogueLineData[] newLines)
        {
            id = newId;
            rootId = root;
            lines = newLines;
        }

        // A tree whose choices point at lines that do not exist is a conversation that dead ends
        // with no way out, and nothing about it looks wrong until somebody is standing in it
        public bool IsWellFormed(out string problem)
        {
            if (lines == null || lines.Length == 0)
            {
                problem = $"'{id}' has no lines";
                return false;
            }

            if (Line(rootId) == null)
            {
                problem = $"'{id}' has no line '{rootId}' to open with";
                return false;
            }

            foreach (DialogueLineData line in lines)
            {
                if (line?.Choices == null)
                    continue;

                foreach (DialogueChoiceData choice in line.Choices)
                {
                    if (choice == null || choice.Ends || Line(choice.GoTo) != null)
                        continue;

                    problem = $"'{id}' line '{line.Id}' points at missing line '{choice.GoTo}'";
                    return false;
                }
            }

            problem = null;
            return true;
        }
    }
}
