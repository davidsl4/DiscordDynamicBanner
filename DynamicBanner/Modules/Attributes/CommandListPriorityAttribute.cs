using System;

namespace DynamicBanner.Modules.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class CommandListPriorityAttribute : Attribute
    {
        public int Priority { get; }

        public CommandListPriorityAttribute(int priority)
        {
            Priority = priority;
        }
    }
}