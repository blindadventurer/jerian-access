using System.Collections.Generic;
using _Scripts.AMVCC.Views.ConsequenceButton;
using _Scripts.Helpers;
using _Scripts.Managers;

namespace JerianAccess.Module.Game
{
    /// <summary>
    /// Composes a choice's requirement text from the game's own typed Condition model, mirroring
    /// what ConditionGroupIconTooltipHandler builds into the hover tooltip - but as speech, and
    /// (for the unavailable announcement) only the FAILED conditions, so a blind player hears why
    /// a choice is locked instead of a bare "unavailable". Every name/value comes from the game's
    /// own localization (GetNameLocalised, GetTranslation); only the operator words and glue are
    /// mod strings (the game voices operators as bare symbols, which readers mishandle). Hidden
    /// conditions (Condition.IsHidden) are never spoken, matching the game. Read at speech time;
    /// nothing cached.
    /// </summary>
    public static class ChoiceConditions
    {
        /// <summary>Null when the choice is available; otherwise the failed requirements joined,
        /// or the plain unavailable word when the block is an opaque group (OR-tree only).</summary>
        public static string UnavailableReason(ConsequenceButtonController choice)
        {
            if (choice.GetIsButtonInteractable()) return null;
            var reqs = new List<string>();
            if (choice.HasConditionGroups)
                foreach (var group in choice.ConditionGroups)
                    foreach (var cond in group.Conditions)
                        if (!cond.IsHidden && !cond.IsSatisfied)
                        {
                            var d = Describe(cond);
                            if (d != null) reqs.Add(d);
                        }
            if (reqs.Count == 0) return Loc.T("state.unavailable");
            return Loc.T("choice.unavailable", new { req = string.Join(", ", reqs.ToArray()) });
        }

        /// <summary>Every non-hidden condition with a met / not-met marker (the Space tooltip):
        /// the full requirement picture on demand. Null when the choice carries no conditions.</summary>
        public static string Details(ConsequenceButtonController choice)
        {
            if (!choice.HasConditionGroups) return null;
            var rows = new List<string>();
            foreach (var group in choice.ConditionGroups)
                foreach (var cond in group.Conditions)
                {
                    if (cond.IsHidden) continue;
                    var d = Describe(cond);
                    if (d == null) continue;
                    rows.Add(Loc.T(cond.IsSatisfied ? "choice.cond.met" : "choice.cond.notmet",
                        new { row = d }));
                }
            return rows.Count == 0 ? null : string.Join(". ", rows.ToArray());
        }

        // One condition as "name op value" / "name: status" / bare entity name, using the game's
        // own localized pieces. Returns null for condition kinds with no player-facing text
        // (scene-group / unlocked-group references) so they never surface as noise.
        private static string Describe(Condition c)
        {
            switch (c.Type)
            {
                case ConditionType.Parameter:
                    return Loc.T("choice.req.param", new
                    {
                        name = c.Parameter.GetNameLocalised(),
                        op = OpWord(c.Operator),
                        value = c.Value,
                    });
                case ConditionType.CharacterRelation:
                    return Loc.T("choice.req.relation", new
                    {
                        name = c.Character.GetNameLocalised(),
                        op = OpWord(c.Operator),
                        value = c.Value,
                    });
                case ConditionType.CharacterStatus:
                {
                    var status = I2.Loc.LocalizationManager.GetTranslation(
                        ParametersManager.Instance.GetCharacterStatusKey(
                            c.Character, c.CharacterStatusGroup, c.CharacterStatus) ?? "");
                    var req = Loc.T("choice.req.status",
                        new { name = c.Character.GetNameLocalised(), status });
                    return c.Not ? Loc.T("choice.req.not", new { req }) : req;
                }
                case ConditionType.Objective:
                {
                    var req = c.Objective.GetNameLocalised();
                    return c.Not ? Loc.T("choice.req.not", new { req }) : req;
                }
                case ConditionType.Ending:
                {
                    var req = c.Ending.GetNameLocalised();
                    return c.Not ? Loc.T("choice.req.not", new { req }) : req;
                }
                case ConditionType.Article:
                {
                    var req = c.Article.GetNameLocalised() + ": "
                        + c.Article.GetValueLocalised(c.ArticleValue);
                    return c.Not ? Loc.T("choice.req.not", new { req }) : req;
                }
                case ConditionType.Estate:
                    return Loc.T("choice.req.param", new
                    {
                        name = EstatesSerializeHelper.Initiate.GetEstateParameterName(
                            c.Estate, c.EstateParameterKey),
                        op = OpWord(c.Operator),
                        value = c.Value,
                    });
                case ConditionType.BilateralParameter:
                    return Loc.T("choice.req.param", new
                    {
                        name = c.BilateralParameter.GetNameLocalised(c.BilateralParameterSide),
                        op = OpWord(c.Operator),
                        value = c.Value,
                    });
                default:
                    return null;
            }
        }

        // The comparison operator as a spoken word. Mod table first (more/less/at least/...);
        // the game's own translation of the enum name is the fallback for anything not mapped.
        private static string OpWord(System.Enum op)
        {
            var name = op.ToString();
            var word = Localization.LocalizationManager.GetOrDefault(
                "ui", "choice.op." + name.ToLowerInvariant(), null);
            return word ?? I2.Loc.LocalizationManager.GetTranslation(name) ?? name;
        }
    }
}
