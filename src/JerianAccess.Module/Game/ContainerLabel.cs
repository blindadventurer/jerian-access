using UnityEngine;
using _Scripts.AMVCC.Views.Windows.SceneConsequence.ParameterContainers;

namespace JerianAccess.Module.Game
{
    /// <summary>
    /// What an icon-only item IS, in words. Jerian draws much of its windows as pictures - a
    /// destiny objective, a chronicle entry, an article seal, an estate crest - with the words
    /// living in the hover tooltip. Every such item carries the game's own data containers
    /// (the ones the tooltip controllers read), so the label comes from there rather than from
    /// the picture: NAME synchronously on focus, the full tooltip on Space.
    /// </summary>
    public static class ContainerLabel
    {
        /// <summary>The item's name, or null when it carries no named data (a decorative
        /// picture - one that would only add an unlabeled stop).</summary>
        public static string Of(GameObject go)
        {
            if (go == null) return null;

            var numerical = go.GetComponentInParent<NumericalParameterContainer>();
            if (numerical != null && numerical.IsInitialised
                && !string.IsNullOrEmpty(numerical.NameLocalised))
                return Loc.T("hud.pair", new
                {
                    label = numerical.NameLocalised,
                    value = numerical.CurrentValue,
                });

            return NameOf(go);
        }

        /// <summary>Just what the item IS, without the container's own figure - for a stop that
        /// already shows the figure on screen (an estate's power, a summary counter: the game
        /// draws the number and leaves the word to the hover tooltip). The rendered number is the
        /// one to speak; a container's own CurrentValue can be a different reading of the same
        /// item (an estate's container answers with its loyalty, not the power beside it).</summary>
        public static string NameOf(GameObject go)
        {
            if (go == null) return null;

            var numerical = go.GetComponentInParent<NumericalParameterContainer>();
            if (numerical != null && numerical.IsInitialised
                && !string.IsNullOrEmpty(numerical.NameLocalised))
                return numerical.NameLocalised;

            var text = go.GetComponentInParent<TextParameterContainer>();
            if (text != null && text.IsInitialised && !string.IsNullOrEmpty(text.NameLocalised))
                return text.NameLocalised;

            var generic = go.GetComponentInParent<GenericTranslationKeyContainer>();
            if (generic != null && !string.IsNullOrEmpty(generic.TitleTranslationKey))
                return I2.Loc.LocalizationManager.GetTranslation(generic.TitleTranslationKey);

            // A province on the Empire map: the window is a picture with fourteen hover zones and
            // not one word of text, so the province's own name key is all there is to read.
            var mapElement = go.GetComponentInParent<MapElementView>();
            if (mapElement != null && !string.IsNullOrEmpty(mapElement.MapElementNameKey))
                return I2.Loc.LocalizationManager.GetTranslation(mapElement.MapElementNameKey);

            return null;
        }
    }
}
