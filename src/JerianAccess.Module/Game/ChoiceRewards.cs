using System.Collections.Generic;
using _Scripts.AMVCC.Models.Character;
using _Scripts.AMVCC.Views.ConsequenceButton;
using _Scripts.Helpers;
using _Scripts.Managers;

namespace JerianAccess.Module.Game
{
    /// <summary>
    /// A choice's CONSEQUENCE SHEET as speech - what the game shows a sighted player on the right
    /// page while the cursor rests on a choice (ConsequenceSheetController: the choice's
    /// description line plus its reward rows). Composed from the same model the sheet renders from
    /// (ConsequenceButtonController.Rewards, the scene's own _ButtonDescription key), because the
    /// sheet's rows are generated prefabs that appear a frame late and vanish on the next hover -
    /// a sighted reader can wait, a spoken one cannot. Hidden-consequences mode is honored: the
    /// game hides the rows in that mode, so the mod says nothing either.
    /// </summary>
    public static class ChoiceRewards
    {
        /// <summary>The sheet for this choice - description, then the reward rows - or null when
        /// the game would show none (hidden-consequences mode, or a choice with no sheet).</summary>
        public static string Summary(ConsequenceButtonController choice)
        {
            if (GameManager.Instance.HiddenConsequencesMode) return null;
            var sb = new System.Text.StringBuilder();
            UI.PanelText.Append(sb, Description(choice));
            var rows = Rows(choice);
            if (rows != null) UI.PanelText.Append(sb, Loc.T("scene.rewards") + " " + rows);
            return sb.Length == 0 ? null : sb.ToString();
        }

        /// <summary>The choice's own explanation line ("{scene master key}_ButtonDescription{n}",
        /// the key ConsequenceSheetController.GetChoiceDescription builds). Empty for scenes that
        /// author none.</summary>
        private static string Description(ConsequenceButtonController choice)
        {
            var scene = GameManager.Instance.OpenedSceneConfiguration;
            if (scene == null) return null;
            return I2.Loc.LocalizationManager.GetTranslation(
                scene.MasterKey + "_ButtonDescription" + choice.ButtonIndex);
        }

        /// <summary>The reward rows the sheet would list, in the game's own order, skipping the
        /// ones it hides from the player. Null when the choice grants nothing readable.</summary>
        public static string Rows(ConsequenceButtonController choice)
        {
            var rows = new List<string>();
            foreach (var reward in choice.Rewards)
            {
                if (!reward.ShownToPlayer) continue;
                var text = Describe(reward);
                if (text != null) rows.Add(text);
            }
            return rows.Count == 0 ? null : string.Join(", ", rows.ToArray());
        }

        /// <summary>One reward row. Numeric rewards read as "name +2" (the sheet's own shape); the
        /// ones the sheet renders as a state change read as "name, value". Every word is the
        /// game's. Public because the interlude popup pages its own rewards one to a page, and
        /// they are the same model shown the same way.</summary>
        public static string Describe(Reward reward) => Describe(reward, withCurrent: false);

        /// <summary>The same row, optionally followed by where the figure now STANDS - what a
        /// sighted player reads off the slider the row animates. Used on the pages the game shows
        /// AFTER applying a change (the scene's consequence page, an interlude's reward pages), so
        /// the player hears the result without walking to the parameter window to look it up.</summary>
        public static string Describe(Reward reward, bool withCurrent)
        {
            switch (reward.RewardType)
            {
                case RewardType.None:
                    return null;
                case RewardType.Parameter:
                case RewardType.Estate:
                case RewardType.BilateralParameter:
                {
                    var row = Loc.T("choice.cons.param", new
                    {
                        name = Name(reward),
                        delta = Signed(reward.Value),
                    });
                    return withCurrent ? WithCurrent(row, reward) : row;
                }
                case RewardType.CharacterRelation:
                    return withCurrent ? WithCurrent(Character(reward), reward) : Character(reward);
                case RewardType.CreedEnding:
                    // The creed ending row has no name term of its own (the game paints it as a
                    // seal from the creed model); the ending sheet that carries it is read where
                    // it is shown, not from here.
                    return null;
                case RewardType.CharacterStatus:
                case RewardType.Character:
                    return Character(reward);
                case RewardType.Article:
                    return Loc.T("choice.cons.set", new
                    {
                        name = Name(reward),
                        value = reward.Article.GetValueLocalised(reward.ArticleValue),
                    });
                default:
                    // Objective / Ending / CreedEnding: the sheet shows the entity, no value.
                    return Name(reward);
            }
        }

        // A character row carries up to two changes, and the game decides which by the same two
        // tests: a status only when the reward names a status group (and is not a pure relation
        // change), a relation only when it moves the number (and is not a pure status change).
        private static string Character(Reward reward)
        {
            var text = Name(reward);
            if (reward.CharacterStatusGroup != null && reward.RewardType != RewardType.CharacterRelation)
            {
                // A status the game has no word for would leave the row as "name, " - the status
                // part is dropped instead, the way the requirement side does it.
                var status = I2.Loc.LocalizationManager.GetTranslation(
                    ParametersManager.Instance.GetCharacterStatusKey(
                        reward.Character, reward.CharacterStatusGroup,
                        reward.CharacterStatus) ?? "");
                if (!string.IsNullOrEmpty(status))
                    text = Loc.T("choice.cons.set", new { name = text, value = status });
            }
            if (reward.Value != 0 && reward.RewardType != RewardType.CharacterStatus)
                text = Loc.T("choice.cons.relation", new { name = text, delta = Signed(reward.Value) });
            return text;
        }

        /// <summary>The row plus where its figure now stands - with the parameter's ceiling where
        /// the game defines one (a parameter is drawn as a slider from its minimum to its maximum;
        /// an estate or a relation figure is not). The "now" word is the mod's own: this build
        /// ships no translation for the term its own tooltips ask for ("Now" resolves to an empty
        /// string in every language, which is why the game's own rows read "( 5)").
        /// The row alone when the reward moves nothing countable.</summary>
        private static string WithCurrent(string row, Reward reward)
        {
            if (string.IsNullOrEmpty(row)) return row;
            switch (reward.RewardType)
            {
                case RewardType.Parameter:
                    return Loc.T("choice.cons.now_max", new
                    {
                        row,
                        current = ParametersManager.Instance.GetParameterValue(reward.Parameter),
                        max = reward.Parameter.MaxValue,
                    });
                case RewardType.Estate:
                    return Now(row, ParametersManager.Instance.GetEstateParameter(
                        reward.Estate, reward.EstateParameterKey,
                        EstatesSerializeHelper.Initiate.EstateModels));
                case RewardType.BilateralParameter:
                    return Now(row, ParametersManager.Instance.GetBilateralParameter(
                        reward.BilateralParameter, reward.BilateralParameterSide,
                        BilateralParameterSerializeHelper.Initiate.BilateralParameterModels));
                case RewardType.CharacterRelation:
                    // A relation reward can also carry a status change; the figure is the part
                    // that moved on a scale, and it is only there when the value moved.
                    if (reward.Value == 0) return row;
                    return Now(row, Singleton<CharacterConfigCollector>.Instance
                        .GetCharacterRelation(reward.Character));
                default:
                    return row;
            }
        }

        private static string Now(string row, int current)
            => Loc.T("choice.cons.now", new { row, current });

        private static string Name(Reward reward)
            => I2.Loc.LocalizationManager.GetTranslation(reward.GetNameTerm() ?? "");

        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
    }
}
