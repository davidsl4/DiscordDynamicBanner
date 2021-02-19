using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DynamicBanner.DDBProtocol.Attributes;
using Newtonsoft.Json.Linq;

namespace DynamicBanner.DDBProtocol
{
    public class DdbProtocolService
    {
        private readonly List<Type> _hosts = new();
        
        public DdbProtocolService()
        {
            _hosts.AddRange(Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetCustomAttribute<HostAttribute>() != null));
        }

        public ExecuteResult GetRouteAsync(Uri uri, out MethodInfo routeMethod)
        {
            var host = _hosts.FirstOrDefault(h => h.GetCustomAttribute<HostAttribute>()?.Hostname == uri.Host);
            if (host == null)
            {
                routeMethod = null;
                return ExecuteResult.HostNotFound;
            }

            
            var uriRoutes = uri.Segments.Select(s => s.Trim('/')).Where(s => s.Length > 0).ToArray();

            MethodInfo FindRoute(int fromIndex, Type currentRoute)
            {
                if (currentRoute == null) return null;
                
                if (fromIndex >= uriRoutes.Length)
                {
                    return currentRoute.GetMethods().FirstOrDefault(m =>
                    {
                        if (!m.IsStatic) return false;
                        var attr = m.GetCustomAttribute<RouteAttribute>();
                        if (attr == null) return false;
                        return attr.Route == null;
                    });
                }

                if (fromIndex < uriRoutes.Length - 1)
                {
                    return FindRoute(fromIndex + 1, currentRoute.GetNestedTypes().FirstOrDefault(n =>
                    {
                        var attr = n.GetCustomAttribute<RouteAttribute>();
                        if (attr == null) return false;
                        return attr.Route == uriRoutes[fromIndex].ToLowerInvariant();
                    }));
                }

                return currentRoute.GetMethods().FirstOrDefault(m =>
                {
                    if (!m.IsStatic) return false;
                    var attr = m.GetCustomAttribute<RouteAttribute>();
                    if (attr == null) return false;
                    return attr.Route == uriRoutes[fromIndex].ToLowerInvariant();
                });
            }

            routeMethod = FindRoute(0, host);
            return routeMethod == null ? ExecuteResult.RouteNotFound : ExecuteResult.Success;
        }

        public static object[] GetRouteOrderedParams(MethodInfo routeMethod, params object[] unorderedParameters)
        {
            var parameters = routeMethod.GetParameters();
            var returnParameters = new object[parameters.Length];
            var passedParameters = 0;

            foreach (var parameter in parameters)
            {
                try
                {
                    returnParameters[passedParameters++] =
                        unorderedParameters.First(p =>
                        {
                            if (p == null) return false;
                             var pType = p.GetType();
                             return pType == parameter.ParameterType || parameter.ParameterType.IsAssignableFrom(pType);
                        });
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
            }

            return returnParameters;
        }
        
        public Task<(ExecuteResult result, JObject returnValue)> ExecuteRouteAsync(Uri uri, params object[] parameters)
        {
            var routeSearchResult = GetRouteAsync(uri, out var routeMethod);
            return routeSearchResult == ExecuteResult.Success ? ExecuteRouteAsync(routeMethod, parameters) : Task.FromResult((routeSearchResult, (JObject)null));
        }

        public async Task<(ExecuteResult result, JObject returnValue)> ExecuteRouteAsync(MethodInfo routeMethod,
            params object[] parameters)
        {
            if (routeMethod.ReturnType != typeof(JObject) && routeMethod.ReturnType != typeof(Task<JObject>))
                return (ExecuteResult.InvalidRouteSignature, (JObject) null);
            var orderedParams = GetRouteOrderedParams(routeMethod, parameters);
            if (orderedParams == null)
                return (ExecuteResult.InvalidRouteSignature, (JObject) null);
            try
            {
                var result = routeMethod.Invoke(null, parameters);
                if (result is Task<JObject> taskResult)
                {
                    result = await taskResult.ConfigureAwait(false);
                }

                return (ExecuteResult.Success, result as JObject);
            }
            catch
            {
                return (ExecuteResult.ExceptionInRoute, null);
            }
        }
    }
}