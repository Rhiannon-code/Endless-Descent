using System;
using System.Collections.Generic;
using EndlessDescent.Data;
using EndlessDescent.Player;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    [DisallowMultipleComponent]
    public class CharacterCreationScreen : MonoBehaviour
    {
        [SerializeField] CharacterSheet sheet;
        [SerializeField] GameDatabase database;
        [SerializeField] RectTransform root;
        [SerializeField] Behaviour[] suspendWhileOpen;
        [SerializeField] int attributePool = 30;
        [SerializeField] int specialPool = 12;

        const int PrimaryCount = 3;
        const int MajorCount = 3;
        const int MinorCount = 6;

        enum Step { Name, Class, Attributes, Skills, Specials, Birthsign, Background, Review }
        enum Assignment { None, Primary, Major, Minor }

        public event Action<CharacterBlueprint> Finished;

        readonly int[] spend = new int[8];
        readonly Assignment[] assignments = new Assignment[Skills.Count];
        readonly List<CharacterSpecial> chosen = new List<CharacterSpecial>();

        BirthsignId birthsign = BirthsignId.None;
        ClassDefinition preset;
        BackgroundDefinition background;
        string characterName = "Wanderer";

        Step step = Step.Name;
        RectTransform page;
        Text heading;
        Text summary;
        Text nextLabel;

        int SpentAttributes { get { int t = 0; foreach (int v in spend) t += v; return t; } }
        int SpentSpecials { get { int t = 0; foreach (CharacterSpecial s in chosen) t += Specials.Cost(s); return t; } }

        bool IsCustom => preset == null;

        int Count(Assignment kind)
        {
            int total = 0;
            foreach (Assignment a in assignments)
                if (a == kind) total++;
            return total;
        }

        void Awake()
        {
            BuildFrame();
            Rebuild();
        }

        void OnEnable() => Suspend(true);

        public void Show()
        {
            step = Step.Name;
            root.gameObject.SetActive(true);
            enabled = true;
            Suspend(true);
            Rebuild();
        }

        public void Hide()
        {
            root.gameObject.SetActive(false);
            Suspend(false);
        }

        void Suspend(bool suspended)
        {
            bool inWorld = suspendWhileOpen != null && suspendWhileOpen.Length > 0;

            if (inWorld)
            {
                foreach (Behaviour behaviour in suspendWhileOpen)
                    if (behaviour != null) behaviour.enabled = !suspended;
            }

            if (!suspended && !inWorld)
                return;

            Cursor.lockState = suspended ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = suspended;
        }

        void BuildFrame()
        {
            heading = MenuWidgets.Label(root, new Vector2(40f, -30f), 900f, string.Empty, 24);
            heading.color = new Color(0.95f, 0.85f, 0.5f);

            page = MenuWidgets.Panel(root, "Page", new Vector2(40f, -80f), new Vector2(1200f, 470f));

            summary = MenuWidgets.Label(root, new Vector2(40f, -572f), 1100f, string.Empty, 13);
            summary.color = new Color(0.78f, 0.76f, 0.7f);

            MenuWidgets.Button(root, new Vector2(40f, -604f), new Vector2(150f, 32f), Back).text = "Back";
            nextLabel = MenuWidgets.Button(root, new Vector2(200f, -604f), new Vector2(200f, 32f), Next);
        }

        void Rebuild()
        {
            for (int i = page.childCount - 1; i >= 0; i--)
                Destroy(page.GetChild(i).gameObject);

            heading.text = Title(step);

            switch (step)
            {
                case Step.Name: BuildName(); break;
                case Step.Class: BuildClass(); break;
                case Step.Attributes: BuildAttributes(); break;
                case Step.Skills: BuildSkills(); break;
                case Step.Specials: BuildSpecials(); break;
                case Step.Birthsign: BuildBirthsign(); break;
                case Step.Background: BuildBackground(); break;
                default: BuildReview(); break;
            }

            RefreshFooter();
        }

        static string Title(Step which)
        {
            switch (which)
            {
                case Step.Name: return "WHO ARE YOU";
                case Step.Class: return "WHAT ARE YOU";
                case Step.Attributes: return "ATTRIBUTES";
                case Step.Skills: return "SKILLS";
                case Step.Specials: return "ADVANTAGES AND DISADVANTAGES";
                case Step.Birthsign: return "BIRTHSIGN";
                case Step.Background: return "WHERE YOU CAME FROM";
                default: return "READY";
            }
        }

        // The three steps between Class and Birthsign only exist to build a custom class, so a named
        // one steps straight over them rather than showing rows that cannot be changed
        bool Skipped(Step which) =>
            !IsCustom && (which == Step.Attributes || which == Step.Skills || which == Step.Specials);

        void Next()
        {
            if (step == Step.Review)
            {
                Begin();
                return;
            }

            if (!Ready())
                return;

            do step++;
            while (step < Step.Review && Skipped(step));

            Rebuild();
        }

        void Back()
        {
            if (step == Step.Name)
                return;

            do step--;
            while (step > Step.Name && Skipped(step));

            Rebuild();
        }

        bool Ready()
        {
            if (step == Step.Name)
                return !string.IsNullOrWhiteSpace(characterName);

            if (step == Step.Skills)
                return Count(Assignment.Primary) >= PrimaryCount && Count(Assignment.Major) >= MajorCount;

            return true;
        }

        void RefreshFooter()
        {
            bool ready = Ready();

            nextLabel.text = step == Step.Review ? "BEGIN" : ready ? "Next" : Blocker();
            nextLabel.color = ready ? Color.white : new Color(0.7f, 0.6f, 0.6f);

            summary.text = Summary();
        }

        string Blocker()
        {
            if (step == Step.Name)
                return "Name yourself";

            return $"Pick {PrimaryCount} primary and {MajorCount} major";
        }

        string Summary()
        {
            switch (step)
            {
                case Step.Attributes:
                    return $"{attributePool - SpentAttributes} attribute points left.";
                case Step.Skills:
                    return $"Primary {Count(Assignment.Primary)}/{PrimaryCount}   " +
                           $"Major {Count(Assignment.Major)}/{MajorCount}   " +
                           $"Minor {Count(Assignment.Minor)}/{MinorCount}: click a skill to cycle it.";
                case Step.Specials:
                    return $"{specialPool - SpentSpecials} points left. " +
                           $"A harder class levels more slowly: difficulty x{Difficulty():F2}.";
                default:
                    return string.Empty;
            }
        }

        float Difficulty()
        {
            ClassDefinition definition = preset != null ? preset : BuildCustomClass();
            return definition != null ? definition.Difficulty : 1f;
        }

        void BuildName()
        {
            MenuWidgets.Label(page, new Vector2(0f, -10f), 900f,
                "The empire does not ask. The temple that named you wrote it down, and everyone else " +
                "reads it off the sign you were born under.");

            GameObject field = new GameObject("Name", typeof(RectTransform));
            field.transform.SetParent(page, false);

            Image back = field.AddComponent<Image>();
            back.color = new Color(0.14f, 0.14f, 0.18f, 0.95f);
            MenuWidgets.Anchor(back.rectTransform, new Vector2(0f, -56f), new Vector2(420f, 34f));

            Text text = MenuWidgets.Label(back.rectTransform, new Vector2(8f, 0f), 400f, characterName, 16);
            text.raycastTarget = false;

            InputField input = field.AddComponent<InputField>();
            input.textComponent = text;
            input.text = characterName;
            input.characterLimit = 24;
            input.onValueChanged.AddListener(value => { characterName = value; RefreshFooter(); });
        }

        void BuildClass()
        {
            float y = -10f;

            y = ClassRow(y, null, "Custom", "Build one out of points, skills and what you are willing to give up.");

            IReadOnlyList<ClassDefinition> classes = database != null ? database.Classes : null;

            if (classes == null)
                return;

            foreach (ClassDefinition definition in classes)
            {
                if (definition != null)
                    y = ClassRow(y, definition, definition.DisplayName, definition.Description);
            }
        }

        float ClassRow(float y, ClassDefinition definition, string name, string description)
        {
            bool on = preset == definition;

            Text row = MenuWidgets.Button(page, new Vector2(0f, y), new Vector2(760f, 46f),
                () => { preset = definition; Rebuild(); },
                on ? new Color(0.20f, 0.20f, 0.14f, 0.95f) : new Color(0.12f, 0.12f, 0.16f, 0.9f));

            row.text = (on ? "[x] " : "[ ] ") + name;
            row.fontSize = 15;
            row.alignment = TextAnchor.UpperLeft;

            Text note = MenuWidgets.Label(row.rectTransform, new Vector2(0f, -22f), 740f, description, 11);
            note.color = new Color(0.72f, 0.70f, 0.64f);

            return y - 52f;
        }

        void BuildAttributes()
        {
            float y = -10f;

            foreach (CharacterAttribute attribute in Enum.GetValues(typeof(CharacterAttribute)))
            {
                CharacterAttribute captured = attribute;

                Text label = MenuWidgets.Label(page, new Vector2(0f, y), 220f, string.Empty, 15);
                label.text = $"{attribute,-14}{50 + spend[(int)attribute],3}";

                MenuWidgets.Button(page, new Vector2(230f, y), new Vector2(28f, 22f), () =>
                {
                    spend[(int)captured] = Mathf.Max(0, spend[(int)captured] - 1);
                    Rebuild();
                }).text = "-";

                MenuWidgets.Button(page, new Vector2(264f, y), new Vector2(28f, 22f), () =>
                {
                    if (SpentAttributes < attributePool) spend[(int)captured]++;
                    Rebuild();
                }).text = "+";

                y -= 30f;
            }
        }

        void BuildSkills()
        {
            float y = -10f;
            float x = 0f;

            foreach (SkillId skill in Enum.GetValues(typeof(SkillId)))
            {
                SkillId captured = skill;
                Assignment assignment = assignments[(int)skill];
                string tag = assignment == Assignment.None ? string.Empty : assignment.ToString().ToUpperInvariant();

                Text row = MenuWidgets.Button(page, new Vector2(x, y), new Vector2(360f, 24f),
                    () => { Cycle(captured); Rebuild(); },
                    assignment == Assignment.None
                        ? new Color(0.11f, 0.11f, 0.14f, 0.9f)
                        : new Color(0.18f, 0.18f, 0.13f, 0.95f));

                row.text = $"{skill,-16} {Skills.Group(skill),-8} {tag}";

                y -= 28f;

                if (y < -290f)
                {
                    y = -10f;
                    x += 380f;
                }
            }
        }

        void Cycle(SkillId skill)
        {
            int i = (int)skill;

            switch (assignments[i])
            {
                case Assignment.None:
                    assignments[i] = Count(Assignment.Primary) < PrimaryCount ? Assignment.Primary
                        : Count(Assignment.Major) < MajorCount ? Assignment.Major
                        : Count(Assignment.Minor) < MinorCount ? Assignment.Minor : Assignment.None;
                    break;
                case Assignment.Primary:
                    assignments[i] = Count(Assignment.Major) < MajorCount ? Assignment.Major : Assignment.None;
                    break;
                case Assignment.Major:
                    assignments[i] = Count(Assignment.Minor) < MinorCount ? Assignment.Minor : Assignment.None;
                    break;
                default:
                    assignments[i] = Assignment.None;
                    break;
            }
        }

        void BuildSpecials()
        {
            float y = -10f;
            float x = 0f;

            foreach (CharacterSpecial special in Enum.GetValues(typeof(CharacterSpecial)))
            {
                if (special == CharacterSpecial.None)
                    continue;

                CharacterSpecial captured = special;
                int cost = Specials.Cost(special);
                bool on = chosen.Contains(special);

                Text row = MenuWidgets.Button(page, new Vector2(x, y), new Vector2(560f, 24f),
                    () => { Toggle(captured); Rebuild(); },
                    on ? new Color(0.17f, 0.19f, 0.15f, 0.95f) : new Color(0.11f, 0.11f, 0.14f, 0.9f));

                row.text = $"{(on ? "[x] " : "[ ] ")}{Specials.Describe(special)}  ({cost:+0;-0})";
                row.color = on
                    ? (cost > 0 ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 0.65f, 0.6f))
                    : new Color(0.78f, 0.78f, 0.78f);

                y -= 28f;

                if (y < -290f)
                {
                    y = -10f;
                    x += 580f;
                }
            }
        }

        void Toggle(CharacterSpecial special)
        {
            if (chosen.Contains(special))
            {
                chosen.Remove(special);
                return;
            }

            // Advantages are paid for out of the pool, disadvantages put points back into it
            if (Specials.Cost(special) > 0 && SpentSpecials + Specials.Cost(special) > specialPool)
                return;

            chosen.Add(special);
        }

        void BuildBirthsign()
        {
            float y = -10f;

            foreach (BirthsignId sign in Enum.GetValues(typeof(BirthsignId)))
            {
                BirthsignId captured = sign;
                bool on = birthsign == sign;

                Text row = MenuWidgets.Button(page, new Vector2(0f, y), new Vector2(760f, 26f),
                    () => { birthsign = captured; Rebuild(); },
                    on ? new Color(0.17f, 0.15f, 0.22f, 0.95f) : new Color(0.11f, 0.11f, 0.14f, 0.9f));

                row.text = (on ? "[x] " : "[ ] ") + Birthsigns.Describe(sign);
                y -= 30f;
            }

            // The sign says which twin was far, and the far twin's temple did the naming. Nobody in
            // the empire is told this, they read it off you
            Text note = MenuWidgets.Label(page, new Vector2(0f, y - 10f), 760f,
                birthsign == BirthsignId.None
                    ? "Unnamed. No temple has a record of you."
                    : $"Named in the house of {Birthsigns.NamedIn(birthsign)}.");

            note.color = new Color(0.62f, 0.6f, 0.7f);
        }

        void BuildBackground()
        {
            IReadOnlyList<BackgroundDefinition> backgrounds = database != null ? database.Backgrounds : null;

            if (backgrounds == null || backgrounds.Count == 0)
            {
                MenuWidgets.Label(page, new Vector2(0f, -10f), 900f,
                    "No backgrounds in the content. Rebuild the greybox content to get them.");
                return;
            }

            float y = -10f;

            foreach (BackgroundDefinition option in backgrounds)
            {
                if (option == null)
                    continue;

                BackgroundDefinition captured = option;
                bool on = background == option;

                Text row = MenuWidgets.Button(page, new Vector2(0f, y), new Vector2(880f, 46f),
                    () => { background = captured; Rebuild(); },
                    on ? new Color(0.20f, 0.18f, 0.14f, 0.95f) : new Color(0.12f, 0.12f, 0.16f, 0.9f));

                row.text = $"{(on ? "[x] " : "[ ] ")}{option.DisplayName} : {option.StartingGold} gold";
                row.fontSize = 15;
                row.alignment = TextAnchor.UpperLeft;

                Text note = MenuWidgets.Label(row.rectTransform, new Vector2(0f, -22f), 860f, option.Description, 11);
                note.color = new Color(0.72f, 0.70f, 0.64f);

                y -= 52f;
            }
        }

        void BuildReview()
        {
            ClassDefinition definition = preset != null ? preset : BuildCustomClass();

            string skills = IsCustom
                ? $"Primary: {Join(Collect(Assignment.Primary))}\nMajor:   {Join(Collect(Assignment.Major))}\nMinor:   {Join(Collect(Assignment.Minor))}"
                : $"Primary: {Join(definition.PrimarySkills)}\nMajor:   {Join(definition.MajorSkills)}\nMinor:   {Join(definition.MinorSkills)}";

            Text body = MenuWidgets.Label(page, new Vector2(0f, -10f), 1000f, string.Empty, 15);
            body.alignment = TextAnchor.UpperLeft;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.rectTransform.sizeDelta = new Vector2(1000f, 400f);

            body.text =
                $"{characterName}, {definition.DisplayName}\n" +
                $"{(birthsign == BirthsignId.None ? "Unnamed by any temple" : Birthsigns.Describe(birthsign))}\n" +
                $"{(background != null ? background.DisplayName : "No background")}\n\n" +
                $"{skills}\n\n" +
                $"Levels at x{definition.Difficulty:F2} the usual cost.";
        }

        static string Join(SkillId[] set)
        {
            if (set == null || set.Length == 0)
                return "none";

            return string.Join(", ", set);
        }

        SkillId[] Collect(Assignment kind)
        {
            List<SkillId> list = new List<SkillId>();

            for (int i = 0; i < assignments.Length; i++)
                if (assignments[i] == kind) list.Add((SkillId)i);

            return list.ToArray();
        }

        ClassDefinition BuildCustomClass()
        {
            ClassDefinition definition = ScriptableObject.CreateInstance<ClassDefinition>();
            definition.name = "Custom";
            definition.Configure("Custom", Collect(Assignment.Primary), Collect(Assignment.Major),
                Collect(Assignment.Minor), spend, chosen.ToArray());
            return definition;
        }

        CharacterBlueprint Blueprint() => new CharacterBlueprint
        {
            Name = characterName,
            ClassId = preset != null ? preset.Id : null,
            ClassName = preset != null ? preset.DisplayName : "Custom",
            BackgroundId = background != null ? background.Id : null,
            AttributeSpend = (int[])spend.Clone(),
            Primary = Collect(Assignment.Primary),
            Major = Collect(Assignment.Major),
            Minor = Collect(Assignment.Minor),
            Specials = chosen.ToArray(),
            Birthsign = birthsign
        };

        void Begin()
        {
            if (IsCustom && (Count(Assignment.Primary) < PrimaryCount || Count(Assignment.Major) < MajorCount))
                return;

            CharacterBlueprint blueprint = Blueprint();

            // Announced whether or not there is a player in this scene, the creator never writes to
            // one directly, because in the boot scene there is not one to write to
            GameStart.NewCharacter(blueprint);
            Finished?.Invoke(blueprint);

            Hide();
            enabled = false;
        }
    }
}
