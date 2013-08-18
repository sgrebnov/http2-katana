// -----------------------------------------------------------------------
// <copyright file="Http2Client.cs" company="Microsoft">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Org.Mentalis;
using Org.Mentalis.Security.Ssl;
using SharedProtocol;
using SharedProtocol.Exceptions;
using SharedProtocol.Extensions;
using SharedProtocol.Framing;
using SharedProtocol.Handshake;
using SharedProtocol.Http11;
using SharedProtocol.IO;
using SharedProtocol.Utils;

namespace SocketServer
{
    using AppFunc = Func<IDictionary<string, object>, Task>;
    /// <summary>
    /// This class handles incoming clients. It can accept them, make handshake and chose how to give a response.
    /// It encouraged to response with http11 or http20 
    /// </summary>
    internal sealed class HttpConnectingClient : IDisposable
    {
        private const string ClientSessionHeader = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n";
       
        private readonly SecureTcpListener _server;
        private readonly SecurityOptions _options;
        private readonly AppFunc _next;
        private Http2Session _session;
        //Remove file:// from Assembly.GetExecutingAssembly().CodeBase
        private readonly bool _useHandshake;
        private readonly bool _usePriorities;
        private readonly bool _useFlowControl;

        private IHttp2FrameHandler _frameHandler;
        

        internal HttpConnectingClient(SecureTcpListener server, SecurityOptions options,
                                     AppFunc next, bool useHandshake, bool usePriorities, 
                                     bool useFlowControl, IHttp2FrameHandler frameHandler)
        {
            _usePriorities = usePriorities;
            _useHandshake = useHandshake;
            _useFlowControl = useFlowControl;
            _server = server;
            _next = next;
            _options = options;
            _frameHandler = frameHandler;
        }

        private IDictionary<string, object> MakeHandshakeEnvironment(SecureSocket incomingClient)
        {
            var result = new Dictionary<string, object>
                {
                    {"securityOptions", _options},
                    {"secureSocket", incomingClient},
                    {"end", ConnectionEnd.Server}
                };

            return result;
        }
		
        /// <summary>
        /// Accepts client and deals handshake with it.
        /// </summary>
        internal void Accept()
        {
            SecureSocket incomingClient;
            using (var monitor = new ALPNExtensionMonitor())
            {
                incomingClient = _server.AcceptSocket(monitor);
            }
            Http2Logger.LogDebug("New connection accepted");
            Task.Run(() => HandleAcceptedClient(incomingClient));
        }

        private void HandleAcceptedClient(SecureSocket incomingClient)
        {
            bool backToHttp11 = false;
            string selectedProtocol = Protocols.Http2;
            var handshakeEnvironment = MakeHandshakeEnvironment(incomingClient);
            IDictionary<string, object> handshakeResult = null;

            //Think out smarter way to get handshake result.
            //DO NOT change Middleware function. If you will do so, server will not even launch. (It's owin's problem)
            Func<Task> handshakeAction = () =>
                {
                    var handshakeTask = new Task(() =>
                        {
                            handshakeResult = HandshakeManager.GetHandshakeAction(handshakeEnvironment).Invoke();
                        });
                    return handshakeTask;
                };
            
            if (_useHandshake)
            {
                var environment = new Dictionary<string, object>
                    {
                        //Sets the handshake action depends on port.
                        {"HandshakeAction", handshakeAction},
                    };

                try
                {
                    var handshakeTask = _next(environment);
                    
                    handshakeTask.Start();
                    if (!handshakeTask.Wait(6000))
                    {
                        incomingClient.Close();
                        Http2Logger.LogError("Handshake timeout. Connection dropped.");
                        return;
                    }
                    
                    selectedProtocol = incomingClient.SelectedProtocol;
                }
                catch (Http2HandshakeFailed ex)
                {
                    if (ex.Reason == HandshakeFailureReason.InternalError)
                    {
                        backToHttp11 = true;
                    }
                    else
                    {
                        incomingClient.Close();
                        Http2Logger.LogError("Handshake timeout. Client was disconnected.");
                        return;
                    }
                }
                catch (Exception e)
                {
                    Http2Logger.LogError("Exception occured. Closing client's socket. " + e.Message);
                    incomingClient.Close();
                    return;
                }
            }
            try
            {
                HandleRequest(incomingClient, selectedProtocol, backToHttp11, handshakeResult);
            }
            catch (Exception e)
            {
                Http2Logger.LogError("Exception occured. Closing client's socket. " + e.Message);
                incomingClient.Close();
            }
        }

        private void HandleRequest(SecureSocket incomingClient, string alpnSelectedProtocol, 
                                   bool backToHttp11, IDictionary<string, object> handshakeResult)
        {
            if (backToHttp11 || alpnSelectedProtocol == Protocols.Http1)
            {
                Http2Logger.LogDebug("Sending with http11");
                Http11Manager.Http11SendResponse(incomingClient);
                return;
            }

            if (GetSessionHeaderAndVerifyIt(incomingClient))
            {
                OpenHttp2Session(incomingClient, handshakeResult);
            }
            else
            {
                Http2Logger.LogError("Client has wrong session header. It was disconnected");
                incomingClient.Close();
            }
        }

        private bool GetSessionHeaderAndVerifyIt(SecureSocket incomingClient)
        {
            var sessionHeaderBuffer = new byte[ClientSessionHeader.Length];

            int received = incomingClient.Receive(sessionHeaderBuffer, 0,
                                                   sessionHeaderBuffer.Length, SocketFlags.None);


            var receivedHeader = Encoding.UTF8.GetString(sessionHeaderBuffer);

            return string.Equals(receivedHeader, ClientSessionHeader, StringComparison.OrdinalIgnoreCase);
        }

        private async void OpenHttp2Session(SecureSocket incomingClient, IDictionary<string, object> handshakeResult)
        {
            Http2Logger.LogDebug("Handshake successful");
            _session = new Http2Session(incomingClient, ConnectionEnd.Server, _usePriorities,_useFlowControl, handshakeResult);
            _session.OnFrameReceived += _frameHandler.ProcessFrame;

            try
            {
                await _session.Start();
            }
            catch (Exception)
            {
                Http2Logger.LogError("Client was disconnected");
            }
        }

       

        public void Dispose()
        {
            if (_session != null)
            {
                _session.Dispose();
                _session.OnFrameReceived -= _frameHandler.ProcessFrame;
            }
   
            _frameHandler.Dispose();
        }
    }
}
