using Org.Mentalis.Security.Ssl;
using Org.Mentalis.Security.Ssl.Shared.Extensions;
using SharedProtocol;
using SharedProtocol.EventArgs;
using SharedProtocol.Utils;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using System.Linq;
using Org.Mentalis.Security.Certificates;
using System.IO;
using System.Reflection;
using System.Threading;
using SharedProtocol.Handshake;

namespace SocketServer
{
    public abstract class Http2ServerBase : IDisposable
    {
        protected readonly int _port;
        protected readonly string _scheme;
        protected SecureTcpListener _server;
        protected bool _disposed;
        protected bool _usePriorities;
        protected bool _useFlowControl;

        private const string _certificateFilename = @"\certificate.pfx";
        protected SecurityOptions _options;

        protected Http2ServerBase(IDictionary<string, object> properties)
        {
            _port = int.Parse(((IList<IDictionary<string, object>>)properties["host.Addresses"]).First().Get<string>("port"));
            _scheme = ((IList<IDictionary<string, object>>)properties["host.Addresses"]).First().Get<string>("scheme");
            _disposed = false;
            UpgradeHandshakeEnabled = (bool)properties["use-handshake"];
            _usePriorities = (bool)properties["use-priorities"];
            _useFlowControl = (bool)properties["use-flowControl"];

            int securePort;

            try
            {
                securePort = int.Parse(ConfigurationManager.AppSettings["securePort"]);
            }
            catch (Exception)
            {
                Http2Logger.LogError("Incorrect port in the config file!" + ConfigurationManager.AppSettings["securePort"]);
                return;
            }

            if (_port == securePort && _scheme == Uri.UriSchemeHttp
                || _port != securePort && _scheme == Uri.UriSchemeHttps)
            {
                Http2Logger.LogError("Invalid scheme or port! Use https for secure port.");
                return;
            }

            var extensions = new[] { ExtensionType.Renegotiation, ExtensionType.ALPN };

            // protocols should be in order of their priority
            _options = _port == securePort ? new SecurityOptions(SecureProtocol.Tls1, extensions, new[] { Protocols.Http2, Protocols.Http1 }, ConnectionEnd.Server)
                                : new SecurityOptions(SecureProtocol.None, extensions, new[] { Protocols.Http2, Protocols.Http1 }, ConnectionEnd.Server);

            _options.VerificationType = CredentialVerification.None;
            _options.Flags = SecurityFlags.Default;
            _options.AllowedAlgorithms = SslAlgorithms.RSA_AES_256_SHA | SslAlgorithms.NULL_COMPRESSION;
            _options.Certificate = Certificate.CreateFromCerFile(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8)) + _certificateFilename);

            _server = new SecureTcpListener(_port, _options);

            ThreadPool.SetMaxThreads(30, 10);
        }

        public virtual void Dispose()
        {
            if (!_disposed)
            {
                _server.Stop();
                _disposed = true;
            }
        }

        public virtual void Listen()
        {
            Http2Logger.LogInfo("Started on port " + _port);
            _server.Start();
            while (!_disposed)
            {
                try
                {
                    var client = new HttpConnectingClient(this, _options, _usePriorities, _useFlowControl);
                    client.Accept();
                }
                catch (Exception ex)
                {
                    Http2Logger.LogError("Unhandled exception was caught: " + ex.Message);
                }
            }
        }

        public abstract void ProcessFrame(object sender, FrameReceivedEventArgs args);

        public bool UpgradeHandshakeEnabled { get; set; }

        public SecureTcpListener Listener { get { return _server; } }

        public virtual string HandleHandshake(IDictionary<string, object> handshakeEnvironment)
        {
            // TODO: get protocol returned by handshake action
            HandshakeManager.GetHandshakeAction(handshakeEnvironment).Invoke();
            return Protocols.Http2;
        }
    }
}
