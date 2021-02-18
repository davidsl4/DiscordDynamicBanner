using System;
using Discord.Commands;

namespace DynamicBanner.Modules.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class OrderedSummary : SummaryAttribute
    {
        private readonly int _order;
        public int Order => _order;

        public OrderedSummary(string summary, int order) : base(summary)
        {
            _order = order;
        }
    }
}