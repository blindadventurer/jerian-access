using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using _Scripts.AMVCC.Views.ConsequenceButton;
using _Scripts.AMVCC.Views.Windows.SceneConsequence;

namespace JerianAccess.Module.Game
{
    /// <summary>
    /// The CONSEQUENCE page a scene turns into once a choice resolves: what the pick actually
    /// changed, one reward group to a page (SceneConsequenceController). The game paints each
    /// group as animated sliders, counters and crests - a skill row renders "Закон – 3" with the
    /// change living only in the movement, an estate crest carries no words at all - so the page
    /// is composed from the same reward model its rows are built from, the way the interlude's
    /// reward pages and a choice's own sheet are.
    ///
    /// With hidden-consequences mode on this announcement is the ONLY word a player gets about
    /// the change: the sheet that promises consequences before the pick is hidden in that mode,
    /// and the page's own parameter-change sound is all that marks it otherwise.
    /// </summary>
    public static class RewardPage
    {
        private static readonly FieldInfo GroupsField = typeof(SceneConsequenceController)
            .GetField("_rewardGroups", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo PageIndexField = typeof(SceneConsequenceController)
            .GetField("_currentPageIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo RowsContainerField = typeof(SceneConsequenceController)
            .GetField("pageRewardsContainer", BindingFlags.NonPublic | BindingFlags.Instance);

        // Delivery bookkeeping only - the page index and the words are re-read from the game
        // every frame. Static because the page belongs to the scene rather than to whichever
        // screen is on top of it: a story scene and an option scene raise the same page, and both
        // must announce it exactly once.
        private static string _spoken;

        /// <summary>Speak the changes the visible consequence page is showing, once per page.
        /// Called from the screens a consequence page can appear under.</summary>
        public static void Deliver()
        {
            var signature = Signature();
            if (signature == null) { _spoken = null; return; }
            if (signature == _spoken) return;
            _spoken = signature;
            var words = Words();
            if (words != null) Mod.Speech.Output(Loc.T("scene.rewards") + " " + words);
        }

        /// <summary>True for a label that belongs to a consequence page - the rows are announced
        /// from the model here, so a screen sweeping rendered text around one must leave them to
        /// this and read only the page's own words.</summary>
        public static bool OwnsLabel(GameObject go)
            => go.GetComponentInParent<SceneConsequenceController>() != null;

        // The page index leads the signature so two scenes granting the same single reward in a
        // row are still two deliveries, and so a player paging back and forth over the groups
        // hears each one again. The signature deliberately carries the changes WITHOUT their
        // resulting values: the game applies a change while its page is up, so a signature that
        // moved with the figures would deliver the same page twice, once per value.
        private static string Signature()
        {
            int index;
            var group = VisibleGroup(out index);
            if (group == null) return null;
            var rows = Rows(group, withCurrent: false);
            return rows == null ? null : index + "|" + rows;
        }

        /// <summary>The changes as they are spoken: each one followed by where its figure now
        /// stands, read live at speech time.</summary>
        private static string Words()
        {
            int index;
            var group = VisibleGroup(out index);
            return group == null ? null : Rows(group, withCurrent: true);
        }

        /// <summary>The reward group the game is SHOWING, or null when no consequence page is up.
        /// The group's rows are one child of the container, activated when the game shows the page
        /// and faded in with it: they are the game's own "this page is up now".</summary>
        private static RewardGrouping VisibleGroup(out int pageIndex)
        {
            pageIndex = -1;
            foreach (var controller in Object.FindObjectsOfType<SceneConsequenceController>())
            {
                var container = (Transform)RowsContainerField.GetValue(controller);
                if (container == null || container.childCount == 0
                    || !UiWidgets.Visible(container.GetChild(0).gameObject)) continue;
                int index = (int)PageIndexField.GetValue(controller);
                var groups = (List<RewardGrouping>)GroupsField.GetValue(controller);
                if (groups == null || index < 0 || index >= groups.Count) continue;
                pageIndex = index;
                return groups[index];
            }
            return null;
        }

        /// <summary>The group's changes in the game's own order and words. The rewards are the
        /// ones the choice was authored with (the inverted copies the rows animate from carry the
        /// same changes backwards), so a row reads the way the consequence sheet reads it. Rows
        /// follow the order the group controllers lay them out in - the reward's own Order, which
        /// is 0 for the character rewards the game leaves in model order.</summary>
        private static string Rows(RewardGrouping group, bool withCurrent)
        {
            var rows = new List<string>();
            foreach (var reward in group.Rewards.OrderBy(r => r.Order))
            {
                if (!reward.ShownToPlayer) continue;
                var text = ChoiceRewards.Describe(reward, withCurrent);
                if (!string.IsNullOrEmpty(text)) rows.Add(text);
            }
            return rows.Count == 0 ? null : string.Join(", ", rows.ToArray());
        }
    }
}
