using System.Collections.Generic;
using UnityEngine;
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
        /// <summary>Null when the choice can be taken as it stands; the unlock offer when its
        /// requirements are unmet but payable with a resource (and not paid yet); otherwise the
        /// failed requirements joined, or the plain unavailable word when the block is an opaque
        /// group (OR-tree only).</summary>
        public static string UnavailableReason(ConsequenceButtonController choice)
        {
            if (AllGroupsSatisfied(choice)) return null;
            var offer = UnlockOffer(choice);
            if (offer != null) return offer;
            var reqs = new List<string>();
            foreach (var group in choice.ConditionGroups)
                foreach (var cond in group.Conditions)
                    if (!cond.IsHidden && !Satisfied(cond, choice))
                    {
                        var d = Describe(cond);
                        if (d != null) reqs.Add(d);
                    }
            if (reqs.Count == 0) return Loc.T("state.unavailable");
            return Loc.T("choice.unavailable", new { req = string.Join(", ", reqs.ToArray()) });
        }

        // The choice's requirements as the GAME evaluates them, live. Not GetIsButtonInteractable:
        // that flag is computed once, in the button's own Start, so on the frame the choice panel
        // goes up (the frame the choice announcement fires) it still reads its default, false, and
        // every choice would announce as unavailable. The per-group check IS the flag's condition.
        private static bool AllGroupsSatisfied(ConsequenceButtonController choice)
        {
            if (!choice.HasConditionGroups) return true;
            for (int i = 0; i < choice.ConditionGroups.Length; i++)
                if (!choice.IsConditionGroupSatisfied(i)) return false;
            return true;
        }

        /// <summary>The resource locks a choice with unmet requirements offers instead: the game
        /// lets the player BUY the choice open (Sanity / Tempest Legacy), and only then does its
        /// click go through. Null when the choice has no payable lock, or when every lock on it is
        /// already paid - then the choice is takeable and announces nothing.</summary>
        private static string UnlockOffer(ConsequenceButtonController choice)
        {
            var locks = Locks(choice);
            if (locks.Count == 0) return null;
            var pending = new List<string>();
            foreach (var view in locks)
            {
                if (view.IsUnlocked) continue;
                if (!view.IsUnlockable) return null;   // a lock nothing can open: the reason is the requirement
                pending.Add(Loc.T("choice.unlock", new
                {
                    cost = UnlockCost(view),
                    name = UnlockParameterName(view),
                }));
            }
            return pending.Count == 0 ? null : string.Join(", ", pending.ToArray());
        }

        /// <summary>The choice's live resource locks - the ones the game armed (a lock stays
        /// disabled when its group carries no conditions).</summary>
        public static List<ConditionLockView> Locks(ConsequenceButtonController choice)
        {
            var live = new List<ConditionLockView>();
            foreach (var view in choice.GetComponentsInChildren<ConditionLockView>(true))
                if (view.IsEnabled) live.Add(view);
            return live;
        }

        /// <summary>Every lock on the choice is paid (or it has none) - the game's own click gate
        /// (AreAllConditionLocksUnlocked).</summary>
        public static bool LocksPaid(ConsequenceButtonController choice)
        {
            foreach (var view in Locks(choice))
                if (!view.IsUnlocked) return false;
            return true;
        }

        /// <summary>What paying (or un-paying) this lock does, as speech: the cost and the resource
        /// it comes out of, plus the state it is in now.</summary>
        public static string LockState(ConditionLockView view)
            => Loc.T(view.IsUnlocked ? "choice.unlock.paid" : "choice.unlock", new
            {
                cost = UnlockCost(view),
                name = UnlockParameterName(view),
            });

        // The lock's price: the game sums the cost of every group it covers that is not already
        // satisfied (ConditionLockView.GetUnlockCost). The lock's own object is the parent the
        // condition model resolves scene-group references through.
        private static int UnlockCost(ConditionLockView view)
        {
            int cost = 0;
            foreach (var group in view.ConditionGroups)
                if (!group.AreAllChecksSatisfied(view.gameObject))
                    cost += Mathf.Abs(group.UnlockCost);
            return cost;
        }

        private static string UnlockParameterName(ConditionLockView view)
        {
            foreach (var group in view.ConditionGroups)
                if (group.UnlockParameter != null) return group.UnlockParameter.GetNameLocalised();
            return null;
        }

        /// <summary>Every non-hidden condition with a met / not-met marker (the Space tooltip):
        /// the full requirement picture on demand. Null when the choice carries no conditions.</summary>
        public static string Details(ConsequenceButtonController choice)
        {
            if (!choice.HasConditionGroups) return null;
            var rows = new List<string>();
            foreach (var group in choice.ConditionGroups)
                Rows(group, choice.gameObject, rows);
            return rows.Count == 0 ? null : string.Join(". ", rows.ToArray());
        }

        /// <summary>One condition group as met / not-met rows - the trigger-scene page's readout
        /// (that page IS a condition group, rendered as rows the game dims rather than words).
        /// Null when the group carries nothing a player may see.</summary>
        public static string GroupDetails(ConditionGroup group, GameObject parent)
        {
            var rows = new List<string>();
            Rows(group, parent, rows);
            return rows.Count == 0 ? null : string.Join(". ", rows.ToArray());
        }

        private static void Rows(ConditionGroup group, GameObject parent, List<string> rows)
        {
            foreach (var cond in group.Conditions)
            {
                if (cond.IsHidden) continue;
                var d = Describe(cond);
                if (d == null) continue;
                rows.Add(Loc.T(cond.IsConditionSatisfied(parent)
                    ? "choice.cond.met" : "choice.cond.notmet", new { row = d }));
            }
            foreach (var list in group.OrConditionLists)
            {
                var any = new List<string>();
                foreach (var cond in list.Conditions)
                {
                    if (cond.IsHidden) continue;
                    var d = Describe(cond);
                    if (d == null) continue;
                    any.Add(Loc.T(cond.IsConditionSatisfied(parent)
                        ? "choice.cond.met" : "choice.cond.notmet", new { row = d }));
                }
                if (any.Count > 0)
                    rows.Add(Loc.T("choice.cond.any", new { rows = string.Join(", ", any.ToArray()) }));
            }
        }

        // The condition as the game evaluates it right now. Condition.IsSatisfied is a field the
        // game refreshes only in the button's Start, so a lock paid mid-panel would still read its
        // startup value; the evaluation itself is public and cheap.
        private static bool Satisfied(Condition cond, ConsequenceButtonController choice)
            => cond.IsConditionSatisfied(choice.gameObject);

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
                    // The game draws this row as the character's icon, their name and the status
                    // word. A status the game has no word for (the ".None" key of a group nobody
                    // has entered yet) would leave the row as a bare name - "требуется Тест",
                    // which names no requirement at all - so the group is named instead, and a
                    // condition that can be named neither way says nothing rather than nonsense.
                    var key = ParametersManager.Instance.GetCharacterStatusKey(
                        c.Character, c.CharacterStatusGroup, c.CharacterStatus);
                    var status = I2.Loc.LocalizationManager.GetTranslation(key ?? "");
                    if (string.IsNullOrEmpty(status))
                    {
                        status = I2.Loc.LocalizationManager.GetTranslation(
                            ParametersManager.Instance.GetCharacterStatusGroupKey(
                                c.Character, c.CharacterStatusGroup) ?? "");
                        Mod.Log("character-status condition: no status text for key '" + key
                                + "', naming the group instead ('" + status + "')");
                    }
                    if (string.IsNullOrEmpty(status)) return null;
                    // The game draws the negation between the name and the status ("X ≠ Y"), and
                    // that is where it belongs in speech too: "не X Y" reads as a different person.
                    if (c.Not) status = Loc.T("choice.req.not", new { req = status });
                    return Loc.T("choice.req.status",
                        new { name = c.Character.GetNameLocalised(), status });
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
                    var value = c.Article.GetValueLocalised(c.ArticleValue);
                    if (c.Not) value = Loc.T("choice.req.not", new { req = value });
                    return c.Article.GetNameLocalised() + ": " + value;
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
