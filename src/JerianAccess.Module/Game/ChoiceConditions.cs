using System.Collections.Generic;
using UnityEngine;
using _Scripts.AMVCC.Models.Static;
using _Scripts.AMVCC.Views;
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
    ///
    /// Two condition shapes carry no words of their own and would leave a locked choice saying
    /// only "unavailable": an OR list (the group is satisfied by ANY of several alternatives) and
    /// a scene-group reference (the requirement lives in the scene's shared container). Both are
    /// expanded here into the conditions they actually stand for - the game itself renders the
    /// second as a developer string ("Глобальная группа выполняется: 3"), which names nothing.
    /// </summary>
    public static class ChoiceConditions
    {
        // How far a scene-group reference is followed before it is treated as opaque: a referenced
        // group may reference another, and a cycle would recurse forever.
        private const int MaxGroupDepth = 2;

        /// <summary>The object a condition is EVALUATED against. A scene-group reference resolves
        /// its group through the parent chain's ConditionGroupsContainer, and Jerian hangs the
        /// choice buttons outside it - so evaluating from the button logs "GroupConditionsContainer
        /// is not found!!" in the game's own log and answers false for every such condition. The
        /// scene carries exactly one container (Zenject injects that same instance into the
        /// buttons), so its own object is the parent that resolves.</summary>
        private static GameObject ConditionParent(GameObject owner)
        {
            if (owner != null && owner.GetComponentInParent<ConditionGroupsContainer>() != null)
                return owner;
            var container = Object.FindObjectOfType<ConditionGroupsContainer>();
            return container == null ? owner : container.gameObject;
        }

        /// <summary>Null when the choice can be taken as it stands; the unlock offer when its
        /// requirements are unmet but payable with a resource (and not paid yet); otherwise the
        /// failed requirements joined, or the resource it falls short of when the block is an
        /// opaque group.</summary>
        public static string UnavailableReason(ConsequenceButtonController choice)
        {
            // A choice the game lets the player BUY open is interactable whatever its conditions
            // say (ConsequenceButtonController.IsButtonInteractable returns true as soon as the
            // button can be unlocked): its locks are the gate. So the offer is the whole answer
            // while one is unpaid, and a choice whose locks are all paid is simply available -
            // reading its conditions there announced a bought choice as unavailable.
            var offer = UnlockOffer(choice);
            if (offer != null) return offer;
            if (Payable(choice)) return null;
            if (AllGroupsSatisfied(choice)) return null;
            var reqs = FailedRequirementText(choice.ConditionGroups, choice.gameObject);
            if (reqs != null) return Loc.T("choice.unavailable", new { req = reqs });
            // Nothing the player may see failed, so the block is a lock they cannot pay: name the
            // resource and the gap instead of a bare word that answers nothing.
            var shortfall = Shortfall(choice);
            if (shortfall != null) return shortfall;
            // A bare "unavailable" is the one answer a blind player cannot act on, so the state
            // that produced it is logged: a run of these lines IS the bug report.
            Mod.Log("choice " + choice.ButtonIndex + " announces a bare unavailable: "
                    + GroupTrace(choice));
            return Loc.T("state.unavailable");
        }

        // Debug-only: what each of the choice's groups and locks looked like at the moment its
        // announcement came out with nothing to say.
        private static string GroupTrace(ConsequenceButtonController choice)
        {
            var sb = new System.Text.StringBuilder();
            var parent = ConditionParent(choice.gameObject);
            sb.Append("parent=").Append(parent == null ? "null" : parent.name);
            for (int i = 0; i < choice.ConditionGroups.Length; i++)
            {
                var g = choice.ConditionGroups[i];
                sb.Append(" | group ").Append(i).Append(" '").Append(g.Name).Append("'")
                  .Append(" locked=").Append(g.IsLocked)
                  .Append(" cost=").Append(g.UnlockCost)
                  .Append(" param=").Append(g.UnlockParameter == null ? "-" : g.UnlockParameter.name)
                  .Append(" satisfied=").Append(g.AreAllChecksSatisfied(parent))
                  .Append(" conds=").Append(g.Conditions.Count)
                  .Append(" orLists=").Append(g.OrConditionLists.Count);
                foreach (var cond in g.Conditions)
                    sb.Append(" [").Append(cond.Type).Append(cond.IsHidden ? " hidden" : "")
                      .Append("]");
            }
            foreach (var view in choice.GetComponentsInChildren<ConditionLockView>(true))
                sb.Append(" | lock ").Append(view.name).Append(" enabled=").Append(view.IsEnabled)
                  .Append(" unlocked=").Append(view.IsUnlocked)
                  .Append(" unlockable=").Append(view.IsUnlockable);
            return sb.ToString();
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

        /// <summary>Every failed requirement the player may see, across a set of condition groups:
        /// the plain conditions of each unsatisfied group, plus - where a group's OR lists are the
        /// thing that failed - the alternatives any one of which would open it. Null when nothing
        /// the player may see is what closed them. Shared with the surfaces that gate on the same
        /// model without being choices (a year's projects).</summary>
        public static string FailedRequirementText(
            IEnumerable<ConditionGroup> groups, GameObject parent)
        {
            parent = ConditionParent(parent);
            var reqs = new List<string>();
            foreach (var group in groups)
            {
                if (group.AreAllChecksSatisfied(parent)) continue;
                foreach (var cond in group.Conditions)
                {
                    if (cond.IsHidden) continue;
                    // Describe FIRST: a condition with no player-facing words is not a reason, and
                    // evaluating a scene-group reference costs the game an error in its own log
                    // every time the mod reads a choice.
                    var d = Describe(cond, parent, 0);
                    if (d != null && !Satisfied(cond, parent)) reqs.Add(d);
                }
                var alternatives = FailedAlternatives(group, parent);
                if (alternatives != null) reqs.Add(alternatives);
            }
            return reqs.Count == 0 ? null : string.Join(", ", reqs.ToArray());
        }

        /// <summary>A group's OR lists when NONE of them is satisfied - which is what closes the
        /// group (ConditionGroup.HasUnsatisfiedChecks). Each list is an AND of its own, so a list
        /// reads whole and the lists read as alternatives. Null when the group has no OR lists, or
        /// when one of them is already met (then the OR side is not what failed).</summary>
        private static string FailedAlternatives(ConditionGroup group, GameObject parent)
        {
            if (group.OrConditionLists.Count == 0) return null;
            var alternatives = new List<string>();
            foreach (var list in group.OrConditionLists)
            {
                var rows = new List<string>();
                bool satisfied = true;
                foreach (var cond in list.Conditions)
                {
                    var d = Describe(cond, parent, 0);
                    if (!Satisfied(cond, parent)) satisfied = false;
                    if (cond.IsHidden || d == null) continue;
                    rows.Add(d);
                }
                if (satisfied) return null;
                if (rows.Count > 0) alternatives.Add(string.Join(", ", rows.ToArray()));
            }
            if (alternatives.Count == 0) return null;
            return Loc.T("choice.req.any", new { rows = string.Join("; ", alternatives.ToArray()) });
        }

        /// <summary>The resource locks a choice with unmet requirements offers instead: the game
        /// lets the player BUY the choice open (Sanity / Tempest Legacy), and only then does its
        /// click go through. Null when the choice has no payable lock, or when every lock on it is
        /// already paid - then the choice is takeable and announces nothing.</summary>
        private static string UnlockOffer(ConsequenceButtonController choice)
        {
            var locks = Locks(choice);
            // The lock VIEWS are armed in the button's own Start. A choice read on the frame its
            // panel goes up (a tutorial holds that frame on screen for as long as the player takes
            // to read it) has none yet, and its price - the one thing that choice is about - would
            // go unsaid. The groups the views are built from carry the same price.
            if (locks.Count == 0) return ModelOffer(choice);
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

        /// <summary>The unlock offer read from the choice's own condition groups rather than from
        /// its lock views - the same sums the views would show (one price per resource, over every
        /// unsatisfied group that resource opens). Null when no group is payable, or when one of
        /// them is beyond the player's purse: then the requirement, not a price, is the answer.</summary>
        private static string ModelOffer(ConsequenceButtonController choice)
        {
            if (!choice.HasConditionGroups) return null;
            var parent = ConditionParent(choice.gameObject);
            var resources = new List<Parameter>();
            var costs = new List<int>();
            foreach (var group in choice.ConditionGroups)
            {
                if (group.AreAllChecksSatisfied(parent)) continue;
                // A group the design holds shut (IsLocked) is never bought open - CanPayUnlockCost
                // refuses it outright.
                if (group.IsLocked || group.UnlockParameter == null) return null;
                int cost = Mathf.Abs(group.UnlockCost);
                if (cost == 0) continue;
                int at = resources.IndexOf(group.UnlockParameter);
                if (at < 0) { resources.Add(group.UnlockParameter); costs.Add(cost); }
                else costs[at] += cost;
            }
            var pending = new List<string>();
            for (int i = 0; i < resources.Count; i++)
            {
                if (ParametersManager.Instance.GetParameterValue(resources[i]) < costs[i]) return null;
                pending.Add(Loc.T("choice.unlock",
                    new { cost = costs[i], name = resources[i].GetNameLocalised() }));
            }
            return pending.Count == 0 ? null : string.Join(", ", pending.ToArray());
        }

        /// <summary>What the player is short of on a choice the game would sell them: the resource,
        /// the price and what they hold. Null when no group of the choice is priced at all.</summary>
        private static string Shortfall(ConsequenceButtonController choice)
        {
            if (!choice.HasConditionGroups) return null;
            var parent = ConditionParent(choice.gameObject);
            foreach (var group in choice.ConditionGroups)
            {
                if (group.IsLocked || group.UnlockParameter == null) continue;
                if (group.AreAllChecksSatisfied(parent)) continue;
                int cost = Mathf.Abs(group.UnlockCost);
                if (cost == 0) continue;
                int held = ParametersManager.Instance.GetParameterValue(group.UnlockParameter);
                if (held >= cost) continue;
                return Loc.T("choice.unlock.short", new
                {
                    name = group.UnlockParameter.GetNameLocalised(),
                    cost,
                    current = held,
                });
            }
            return null;
        }

        /// <summary>The game will let this choice be bought open: it carries armed locks and
        /// every one of them is either paid or payable. Read live - the button's own
        /// _canButtonBeUnlocked is computed once, in its Start.</summary>
        private static bool Payable(ConsequenceButtonController choice)
        {
            var locks = Locks(choice);
            if (locks.Count == 0) return false;
            foreach (var view in locks)
                if (!view.IsUnlocked && !view.IsUnlockable) return false;
            return true;
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
            var parent = ConditionParent(view.gameObject);
            foreach (var group in view.ConditionGroups)
                if (!group.AreAllChecksSatisfied(parent))
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
            {
                var orLists = new List<IEnumerable<Condition>>();
                foreach (var list in group.OrConditionLists) orLists.Add(list.Conditions);
                Rows(group.Conditions, orLists, ConditionParent(choice.gameObject), rows);
            }
            return rows.Count == 0 ? null : string.Join(". ", rows.ToArray());
        }

        /// <summary>One condition group as met / not-met rows - the trigger page's readout (that
        /// page IS a condition group, rendered as rows the game dims rather than words). Null when
        /// the group carries nothing a player may see.</summary>
        public static string GroupDetails(ConditionGroup group, GameObject parent)
        {
            parent = ConditionParent(parent);
            var rows = new List<string>();
            var orLists = new List<IEnumerable<Condition>>();
            foreach (var list in group.OrConditionLists) orLists.Add(list.Conditions);
            Rows(group.Conditions, orLists, parent, rows);
            return rows.Count == 0 ? null : string.Join(". ", rows.ToArray());
        }

        /// <summary>What an OBJECTIVE is waiting on, with a live met marker per row. The chapter
        /// book draws these as dimmed and lit rows behind a hover tooltip; the model behind them
        /// is the same typed Condition model a choice is gated on, in its own wrapper.</summary>
        public static string ObjectiveDetails(
            _Scripts.AMVCC.Views.Windows.Destiny.Objective objective, GameObject parent)
        {
            parent = ConditionParent(parent);
            var rows = new List<string>();
            foreach (var group in objective.ConditionGroups)
            {
                var conditions = new List<Condition>();
                foreach (var cond in group.Conditions) conditions.Add(cond);
                var orLists = new List<IEnumerable<Condition>>();
                foreach (var list in group.OrConditionLists)
                {
                    var alternatives = new List<Condition>();
                    foreach (var cond in list.Conditions) alternatives.Add(cond);
                    orLists.Add(alternatives);
                }
                Rows(conditions, orLists, parent, rows);
            }
            return rows.Count == 0 ? null : string.Join(". ", rows.ToArray());
        }

        private static void Rows(IEnumerable<Condition> conditions,
            IEnumerable<IEnumerable<Condition>> orLists, GameObject parent, List<string> rows)
        {
            foreach (var cond in conditions)
            {
                if (cond.IsHidden) continue;
                var d = Describe(cond, parent, 0);
                if (d == null) continue;
                rows.Add(Loc.T(Satisfied(cond, parent)
                    ? "choice.cond.met" : "choice.cond.notmet", new { row = d }));
            }
            foreach (var list in orLists)
            {
                var any = new List<string>();
                foreach (var cond in list)
                {
                    if (cond.IsHidden) continue;
                    var d = Describe(cond, parent, 0);
                    if (d == null) continue;
                    any.Add(Loc.T(Satisfied(cond, parent)
                        ? "choice.cond.met" : "choice.cond.notmet", new { row = d }));
                }
                if (any.Count > 0)
                    rows.Add(Loc.T("choice.cond.any", new { rows = string.Join(", ", any.ToArray()) }));
            }
        }

        // The condition as the game evaluates it right now. Condition.IsSatisfied is a field the
        // game refreshes only in the button's Start, so a lock paid mid-panel would still read its
        // startup value; the evaluation itself is public and cheap.
        private static bool Satisfied(Condition cond, GameObject parent)
            => cond.IsConditionSatisfied(parent);

        // One condition as "name op value" / "name: status" / bare entity name, using the game's
        // own localized pieces. Returns null for condition kinds with no player-facing text
        // (an unlocked-group reference - the other lock the player has to buy first, which the
        // choice's own offer already names) so they never surface as noise.
        private static string Describe(Condition c, GameObject parent, int depth)
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
                case ConditionType.SceneGroupReference:
                    return SceneGroupText(c, parent, depth);
                default:
                    return null;
            }
        }

        /// <summary>A reference to one of the scene's shared condition groups, spoken as the
        /// requirements that group holds. The game renders this row as a developer string with the
        /// group's INDEX, which tells a player nothing; the group behind it is ordinary conditions
        /// with ordinary names.</summary>
        private static string SceneGroupText(Condition c, GameObject parent, int depth)
        {
            if (parent == null || depth >= MaxGroupDepth) return null;
            var container = parent.GetComponentInParent<ConditionGroupsContainer>();
            if (container == null) return null;
            LockLessConditionGroup group = null;
            int index = 0;
            foreach (var candidate in container.ConditionGroups)
            {
                if (index++ != c.SceneConditionGroupIndex) continue;
                group = candidate as LockLessConditionGroup;
                break;
            }
            if (group == null) return null;
            var rows = new List<string>();
            foreach (var cond in group.Conditions)
            {
                if (cond.IsHidden) continue;
                var d = Describe(cond, parent, depth + 1);
                if (d != null) rows.Add(d);
            }
            foreach (var list in group.OrConditionLists)
            {
                var any = new List<string>();
                foreach (var cond in list.Conditions)
                {
                    if (cond.IsHidden) continue;
                    var d = Describe(cond, parent, depth + 1);
                    if (d != null) any.Add(d);
                }
                if (any.Count > 0)
                    rows.Add(Loc.T("choice.req.any", new { rows = string.Join(", ", any.ToArray()) }));
            }
            if (rows.Count == 0) return null;
            var text = string.Join(", ", rows.ToArray());
            return c.Not ? Loc.T("choice.req.not", new { req = text }) : text;
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
