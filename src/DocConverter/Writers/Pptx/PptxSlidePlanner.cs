namespace DocConverter.Writers.Pptx
{
    using System;
    using System.Collections.Generic;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;

    /// <summary>
    /// Splits the document into logical slides: one per slide section, and one at each heading whose level is at or
    /// below PptxOptions.SlideSplitHeadingLevel (the heading becomes the slide title).
    /// </summary>
    internal static class PptxSlidePlanner
    {
        internal static List<PptxSlidePlan> Plan(DocumentModel document, ConversionOptions options)
        {
            List<PptxSlidePlan> plans = new List<PptxSlidePlan>();
            PlanState state = new PlanState(plans, options.Pptx.SlideSplitHeadingLevel);
            Walk(document.Blocks, state, false);
            if (plans.Count == 0) plans.Add(new PptxSlidePlan(document.Metadata.Title));
            return plans;
        }

        private static void Walk(List<Block> blocks, PlanState state, bool insideSlide)
        {
            foreach (Block block in blocks)
            {
                if (block is SectionBlock section)
                {
                    if (section.Kind == SectionKindEnum.Slide && !insideSlide)
                    {
                        StartSlideSection(section, state);
                        continue;
                    }

                    if (section.Kind == SectionKindEnum.Sheet && !insideSlide && !string.IsNullOrEmpty(section.Title))
                    {
                        state.Start(section.Title);
                        Walk(section.Blocks, state, insideSlide);
                        continue;
                    }

                    Walk(section.Blocks, state, insideSlide);
                    continue;
                }

                if (block is HeadingBlock heading && !insideSlide && heading.Level <= state.SplitLevel)
                {
                    state.Start(ModelText.Inlines(heading.Inlines));
                    continue;
                }

                state.Current().Body.Add(block);
            }
        }

        private static void StartSlideSection(SectionBlock section, PlanState state)
        {
            List<Block> children = new List<Block>(section.Blocks);
            string? title = section.Title;
            if (children.Count > 0 && children[0] is HeadingBlock first && first.Level == 1)
            {
                string text = ModelText.Inlines(first.Inlines);
                if (title == null || string.Equals(title.Trim(), text.Trim(), StringComparison.Ordinal))
                {
                    title = text;
                    children.RemoveAt(0);
                }
            }

            PptxSlidePlan plan = state.Start(title);
            if (children.Count > 0 && children[0] is HeadingBlock second && second.Level == 2)
            {
                bool onlyNotesFollow = true;
                for (int i = 1; i < children.Count; i++)
                {
                    if (!(children[i] is SectionBlock s && s.Kind == SectionKindEnum.Generic && s.Title == "Notes"))
                    {
                        onlyNotesFollow = false;
                        break;
                    }
                }

                if (onlyNotesFollow)
                {
                    plan.Subtitle = ModelText.Inlines(second.Inlines);
                    children.RemoveAt(0);
                }
            }

            Walk(children, state, true);
        }
    }
}
