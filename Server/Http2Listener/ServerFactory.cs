using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Owin.Server.Http20Listener
{
    using System.Net;
    using AppFunc = Func<IDictionary<string, object>, Task>;
    using PropertiesType = IDictionary<string, object>;

    public static class ServerFactory
    {
        public static void Initialize(PropertiesType properties)
        {
            throw new NotImplementedException("Functionality not provided yet");
        }

        public static IDisposable Create(AppFunc app, PropertiesType properties)
        {
            return new HttpListener();
        }

    }
}