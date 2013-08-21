﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Org.Mentalis.Security.Certificates;
using Org.Mentalis.Security.Ssl;
using Org.Mentalis.Security.Ssl.Shared.Extensions;
using Owin.Types;
using System.Configuration;
using SharedProtocol;
using SharedProtocol.Utils;

namespace SocketServer
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    /// <summary>
    /// Http2 socket server implementation that uses raw socket.
    /// </summary>
    public class HttpSocketServer : IDisposable
    {
        private static readonly string AssemblyName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8));
        private const string CertificateFilename = @"\certificate.pfx";
        private const string IndexFileName = @"\index.html";

        private readonly AppFunc _next;
        private readonly int _port;
        private readonly string _scheme;
        private readonly bool _useHandshake;
        private readonly bool _usePriorities;
        private readonly bool _useFlowControl;
        private bool _disposed;
        private readonly SecurityOptions _options;
        private readonly SecureTcpListener _server;
        private List<string> _listOfRootFiles = new List<string>();
        private readonly IDictionary<string, object> _properties;

        public HttpSocketServer(Func<IDictionary<string, object>, Task> next, IDictionary<string, object> properties)
        {
            _next = next;
            _properties = properties;
            var addresses = (IList<IDictionary<string, object>>)properties[OwinConstants.CommonKeys.Addresses];

            var address = addresses.First();
            _port = Int32.Parse(address.Get<string>("port"));
            _scheme = address.Get<string>("scheme");

            _useHandshake = (bool)properties["use-handshake"];
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
            _options.Certificate = Certificate.CreateFromCerFile(AssemblyName + CertificateFilename);
            _options.Flags = SecurityFlags.Default;
            _options.AllowedAlgorithms = SslAlgorithms.RSA_AES_256_SHA | SslAlgorithms.NULL_COMPRESSION;

            _server = new SecureTcpListener(_port, _options);

            ThreadPool.SetMaxThreads(30, 10);

            Listen();
        }

        private void InitializeRootFileList()
        {
            lock (IndexFileName)
            {
                using (var indexFile = new StreamWriter(AssemblyName + @"\Root" + IndexFileName))
                {
                    string dirPath = AssemblyName + @"\Root";
                    _listOfRootFiles =
                        Directory.EnumerateFiles(dirPath, "*", SearchOption.TopDirectoryOnly)
                                 .Select(Path.GetFileName)
                                 .ToList();
                    foreach (var fileName in _listOfRootFiles)
                    {
                        indexFile.Write(fileName + "<br>\n");
                    }
                }
            }
        }

        private void Listen()
        {
            InitializeRootFileList();
            Http2Logger.LogInfo("Started on port " + _port);
            _server.Start();
            while (!_disposed)
            {
                try
                {
                    var client = new HttpConnectingClient(_server, _options, _next, _useHandshake, _usePriorities, _useFlowControl, _listOfRootFiles, _properties);
                    client.Accept();
                }
                catch (Exception ex)
                {
                    Http2Logger.LogError("Unhandled exception was caught: " + ex.Message);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_server != null)
            {
                _server.Stop();
            }
        }
    }
}
