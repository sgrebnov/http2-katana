using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Threading.Tasks;

namespace Owin.Server.Http20Listener
{
    using System.Threading;
    using AppFunc = Func<IDictionary<string, object>, Task>;
    using LoggerFactoryFunc = Func<string, Func<TraceEventType, int, object, Exception, Func<object, Exception, string>, bool>>;
    using LoggerFunc = Func<TraceEventType, int, object, Exception, Func<object, Exception, string>, bool>;
    using ParametersType = IDictionary<string, object>;

    public sealed class OwinHttp20Listener : IDisposable
    {
        #region members definitions

        private System.Net.HttpListener _listener;
        private AppFunc _appFunc;
        private LoggerFactoryFunc _loggerFactory;
        private LoggerFunc _logger;
        private IList<string> _basePaths;
        private ParametersType _capabilities;
        private int _currentOutstandingAccepts;
        private int _currentOutstandingRequests;

        private static readonly Func<object, Exception, string> LogStateAndError =
            (state, error) => string.Format(CultureInfo.CurrentCulture, "{0}\r\n{1}", state, error);

        #endregion

        internal OwinHttp20Listener()
        {
            this._listener = new System.Net.HttpListener();
        }

        public System.Net.HttpListener Listener { get { return this._listener; } }

        public void Dispose()
        {
            if (this._listener.IsListening)
            {
                this._listener.Stop();
            }
            ((IDisposable)_listener).Dispose();
        }

        internal void Start(AppFunc appFunc, IList<IDictionary<string, object>> addresses,
            IDictionary<string, object> capabilities, LoggerFactoryFunc loggerFactory)
        {
            this.Start(this._listener, appFunc, addresses, capabilities, loggerFactory);
        }

        internal void Start(System.Net.HttpListener listener, AppFunc appFunc, IList<IDictionary<string, object>> addresses,
            IDictionary<string, object> capabilities, LoggerFactoryFunc loggerFactory)
        {
            Contract.Assert(this._appFunc == null); // Start should only be called once
            Contract.Assert(listener != null);
            Contract.Assert(appFunc != null);
            Contract.Assert(addresses != null);

            this._listener = listener;
            this._appFunc = appFunc;
            this._loggerFactory = loggerFactory;
            if (this._loggerFactory != null)
            {
                this._logger = this._loggerFactory(typeof(OwinHttp20Listener).FullName);
            }

            this._basePaths = new List<string>();

            foreach (var address in addresses)
            {
                // build url from parts
                string scheme = address.Get<string>("scheme") ?? Uri.UriSchemeHttp;
                string host = address.Get<string>("host") ?? "localhost";
                string port = address.Get<string>("port") ?? "5000";
                string path = address.Get<string>("path") ?? string.Empty;

                // if port is present, add delimiter to value before concatenation
                if (!string.IsNullOrWhiteSpace(port))
                {
                    port = ":" + port;
                }

                // Assume http(s)://+:9090/BasePath/, including the first path slash.  May be empty. Must end with a slash.
                if (!path.EndsWith("/", StringComparison.Ordinal))
                {
                    // Http.Sys requires that the URL end in a slash
                    path += "/";
                }
                this._basePaths.Add(path);

                // add a server for each url
                string url = scheme + "://" + host + port + path;
                this._listener.Prefixes.Add(url);
            }

            this._capabilities = capabilities;
            // TODO: add DisconnectHandler class functionality

            if (!this._listener.IsListening)
            {
                this._listener.Start();
            }

            this.OffloadStartNextRequest();
        }

        private void OffloadStartNextRequest()
        {
            if (_listener.IsListening && CanAcceptMoreRequests)
            {
                Task.Factory.StartNew(StartNextRequestAsync)
                    .Catch(errorInfo =>
                    {
                        // StartNextRequestAsync should handle it's own exceptions.
                        LogException("Unexpected exception", errorInfo.Exception);
                        Contract.Assert(false, "Un-expected exception path: " + errorInfo.Exception.ToString());
                        System.Diagnostics.Debugger.Break();
                        return errorInfo.Throw();
                    });
            }
        }

        private void StartNextRequestAsync()
        {
            if (!_listener.IsListening || !CanAcceptMoreRequests)
            {
                return;
            }

            Interlocked.Increment(ref _currentOutstandingAccepts);

            try
            {
                this._listener.GetContextAsync()
                    .Then((Action<System.Net.HttpListenerContext>)StartProcessingRequest, runSynchronously: true)
                    .Catch(HandleAcceptError);
            }
            catch (ApplicationException ae)
            {
                // These come from the thread pool if HttpListener tries to call BindHandle after the listener has been disposed.
                HandleAcceptError(ae);
            }
            catch (System.Net.HttpListenerException hle)
            {
                // These happen if HttpListener has been disposed
                HandleAcceptError(hle);
            }
            catch (ObjectDisposedException ode)
            {
                // These happen if HttpListener has been disposed
                HandleAcceptError(ode);
            }
        }

        private void StartProcessingRequest(System.Net.HttpListenerContext context)
        {
            Interlocked.Decrement(ref _currentOutstandingAccepts);
            Interlocked.Increment(ref _currentOutstandingRequests);
            this.OffloadStartNextRequest();

            EndRequest(null, null);



            //OwinHttpListenerContext owinContext = null;

            try
            {
                //string pathBase, path, query;
                //GetPathAndQuery(context.Request.RawUrl, out pathBase, out path, out query);
                //owinContext = new OwinHttpListenerContext(context, pathBase, path, query, _disconnectHandler);
                //PopulateServerKeys(owinContext.Environment);
                //Contract.Assert(!owinContext.Environment.IsExtraDictionaryCreated,
                //    "All keys set by the server should have reserved slots.");

                //_appFunc(null)
                //    .Then((Func<Task>)owinContext.Response.CompleteResponseAsync, runSynchronously: true)
                //    .Then(() =>
                //    {
                //       EndRequest(owinContext, null);
                //    }, runSynchronously: true)
                //    .Catch(errorInfo =>
                //    {
                //        EndRequest(owinContext, errorInfo.Exception);
                //        return errorInfo.Handled();
                //    });
            }
            catch (Exception ex)
            {
                // TODO: Katana#5 - Don't catch everything, only catch what we think we can handle?  Otherwise crash the process.
                //EndRequest(owinContext, ex);
            }
        }

        private void HandleAcceptError(Exception e)
        {
            Interlocked.Decrement(ref _currentOutstandingAccepts);
            LogException("Accept", e);
        }

        private void EndRequest(Http20ListenerContext context, Exception ex)
        {
            Interlocked.Decrement(ref _currentOutstandingRequests);

            return;

            //if (ex != null)
            //{
            //    LogException("Request Processing", ex);
            //}

            //if (context != null)
            //{
            //    context.Dispose();
            //}

            //// Make sure we start the next request on a new thread, need to prevent stack overflows.
            //OffloadStartNextRequest();
        }

        private CatchInfoBase<Task>.CatchResult HandleAcceptError(CatchInfo errorInfo)
        {
            HandleAcceptError(errorInfo.Exception);
            return errorInfo.Handled();
        }

        internal void Stop()
        {
            try
            {
                if (this._listener.IsListening)
                {
                    this._listener.Stop();
                }
            }
            catch (ObjectDisposedException) { }
        }

        private void LogException(string location, Exception exception)
        {
            if (_logger == null)
            {
                System.Diagnostics.Debug.Write(exception);
            }
            else
            {
                _logger(TraceEventType.Error, 0, location, exception, LogStateAndError);
            }
        }

        private bool CanAcceptMoreRequests
        {
            get
            {
                // TODO: write correct getter
                return true;
            }
        }
    }
}
