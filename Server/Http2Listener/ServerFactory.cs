using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Owin.Server.Http20Listener
{
    using AppFunc = Func<IDictionary<string, object>, Task>;
    using LoggerFactoryFunc = Func<string, Func<TraceEventType, int, object, Exception, Func<object, Exception, string>, bool>>;
    using PropertiesType = IDictionary<string, object>;

    /// <summary>
    /// Implements Katana setup pattern for the OwinHttp20Listener server.
    /// </summary>
    public static class ServerFactory
    {
        public static void Initialize(PropertiesType properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }
            
            // TODO capabilities

            var wrapper = new OwinHttp20Listener();
            properties[typeof(OwinHttp20Listener).FullName] = wrapper;
            properties[typeof(System.Net.HttpListener).FullName] = wrapper.Listener;

            
        }

        /// <summary>
        /// Creates an OwinHttp20Listener and starts listening on the given URL.
        /// </summary>
        /// <param name="app">The application entry point</param>
        /// <param name="properties">The address to listen on</param>
        /// <returns>The OwinHttp20Listener. Invoke Dispose() to shut down</returns>
        public static IDisposable Create(AppFunc app, PropertiesType properties)
        {
            if (app == null)
            {
                throw new ArgumentNullException("app");
            }

            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }

            // Retrieve the instances created in Initialize
            OwinHttp20Listener wrapper = properties.Get<OwinHttp20Listener>(typeof(OwinHttp20Listener).FullName)
                ?? new OwinHttp20Listener();
            IList<IDictionary<string, object>> addresses = properties.Get<IList<IDictionary<string, object>>>("host.Addresses")
                ?? new List<IDictionary<string, object>>();
            IDictionary<string, object> capabilities =
                properties.Get<IDictionary<string, object>>("server.Capabilities")
                    ?? new Dictionary<string, object>();
            LoggerFactoryFunc loggerFactory = properties.Get<LoggerFactoryFunc>("server.LoggerFactory");

            wrapper.Start(app, addresses, capabilities, loggerFactory);
            return wrapper;
        }

    }
}