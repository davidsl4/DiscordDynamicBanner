using System;

namespace DynamicBanner.DDBProtocol.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RouteAttribute : Attribute
    {
        public RouteAttribute()
        {
            Route = null;
        }
        public RouteAttribute(string route)
        {
            Route = route?.ToLowerInvariant();
        }

        public string Route { get; }
    }
}