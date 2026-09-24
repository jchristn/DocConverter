namespace DocConverter.Internal
{
    using System;
    using DocConverter.Model;

    /// <summary>
    /// Decides whether a writer renders a section's title. A slide section read from PPTX carries its title both as the
    /// section title and as a leading heading; rendering both would duplicate it.
    /// </summary>
    internal static class SectionTitles
    {
        internal static bool ShouldRender(SectionBlock section)
        {
            if (string.IsNullOrEmpty(section.Title)) return false;
            if (section.Blocks.Count == 0) return true;
            if (section.Blocks[0] is HeadingBlock heading)
            {
                string text = ModelText.Inlines(heading.Inlines).Trim();
                if (string.Equals(text, section.Title!.Trim(), StringComparison.Ordinal)) return false;
            }

            return true;
        }
    }
}
