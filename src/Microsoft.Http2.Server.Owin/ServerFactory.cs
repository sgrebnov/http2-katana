using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using SocketServer;

namespace Microsoft.Http2.Server.Owin
{
    /// <summary>
    /// Implements Katana setup pattern for the OwinHttp20Listener server.
    /// </summary>
    public static class ServerFactory
    {
        public static void Initialize(IDictionary<string, object> properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }
            
            // TODO capabilities

            //var wrapper = new OwinHttp20Listener();
            //properties[typeof(OwinHttp20Listener).FullName] = wrapper;
            //properties[typeof(System.Net.HttpListener).FullName] = wrapper.Listener;

            
        }

        /// <summary>
        /// Creates an OwinHttp20Listener and starts listening on the given URL.
        /// </summary>
        /// <param name="app">The application entry point</param>
        /// <param name="properties">The address to listen on</param>
        /// <returns>The OwinHttp20Listener. Invoke Dispose() to shut down</returns>
        public static IDisposable Create(Func<IDictionary<string, object>, Task> app, IDictionary<string, object> properties)
        {
            if (app == null)
            {
                throw new ArgumentNullException("app");
            }

            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }

            bool useHandshake = ConfigurationManager.AppSettings["handshakeOptions"] != "no-handshake";
            bool usePriorities = ConfigurationManager.AppSettings["prioritiesOptions"] != "no-priorities";
            bool useFlowControl = ConfigurationManager.AppSettings["flowcontrolOptions"] != "no-flowcontrol";

            properties.Add("use-handshake", useHandshake);
            properties.Add("use-priorities", usePriorities);
            properties.Add("use-flowControl", useFlowControl);

            return new HttpSocketServer(app, properties);
        }

    }
}
