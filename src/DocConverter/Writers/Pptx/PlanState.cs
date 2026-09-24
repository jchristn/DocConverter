namespace DocConverter.Writers.Pptx
{
    using System.Collections.Generic;

    /// <summary>
    /// Mutable state of the slide planner: the plans so far and the slide receiving body blocks.
    /// </summary>
    internal sealed class PlanState
    {
        private readonly List<PptxSlidePlan> _Plans;
        private PptxSlidePlan? _Current = null;

        internal int SplitLevel { get; }

        internal PlanState(List<PptxSlidePlan> plans, int splitLevel)
        {
            _Plans = plans;
            SplitLevel = splitLevel;
        }

        internal PptxSlidePlan Start(string? title)
        {
            _Current = new PptxSlidePlan(title);
            _Plans.Add(_Current);
            return _Current;
        }

        internal PptxSlidePlan Current()
        {
            if (_Current == null) Start(null);
            return _Current!;
        }
    }
}
