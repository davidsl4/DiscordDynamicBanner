using System;

namespace DynamicBanner.DDBProtocol.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class HostAttribute : Attribute
    {
        public HostAttribute(string hostname)
        {
            Hostname = hostname?.ToLowerInvariant();
        }

        public string Hostname { get; }
    }
}