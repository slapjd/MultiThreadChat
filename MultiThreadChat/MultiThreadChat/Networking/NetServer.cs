using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using System.Diagnostics;

namespace MultiThreadChat.Networking
{
    delegate void ClientConnectedHandler(object sender);
    delegate void ServerShutdownHandler(object sender);

    abstract class NetServer<T> where T : NetClient
    {
        /// <summary>
        /// Listener object. Listens for new clients
        /// </summary>
        protected TcpListener _server;
        /// <summary>
        /// List of all connected clients
        /// </summary>
        protected List<T> _clients;
        /// <summary>
        /// Holds whether the server should be running, or if it's shutting down/shut down
        /// </summary>
        protected bool _serverRunning;

        public event ClientConnectedHandler ClientConnected;
        public event ServerShutdownHandler ServerShutdown;

        protected void _invokeClientConnected()
        {
            ClientConnected?.Invoke(this);
        }
        protected void _invokeServerShutdown()
        {
            ServerShutdown.Invoke(this);
        }

        protected abstract void _handleNewClient(TcpClient Client);

        /// <summary>
        /// Constantly listens for new clients until _serverRunning is false (server is shutting down)
        /// </summary>
        protected virtual async void _newClientLoop()
        {
            while (_serverRunning)
            {
                _server.Start();
                try
                {
                    TcpClient newTcpClient = await _server.AcceptTcpClientAsync();
                    _handleNewClient(newTcpClient);

                    ClientConnected?.Invoke(this);
                }
                catch (ObjectDisposedException) //Because the listener was stopped which threw this
                {
                    if (_serverRunning) //If this exception happens while the server is running, we have problems
                    {
                        throw;
                    }
                }//*/
            }
        }

        /// <summary>
        /// Runs when any client disconnects.
        /// Removes the client from _clients
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Arguments associated with disconnect event</param>
        protected virtual void _clientDisconnected(object sender, DisconnectArgs e)
        {
            var netSender = sender as T;
            _clients.Remove(netSender);
        }

        /// <summary>
        /// Sends a message to all currently connected clients
        /// </summary>
        /// <param name="Message">Message to send</param>
        protected virtual void Broadcast(byte[] Message)
        {
            foreach (var client in _clients)
            {
                client.SendAsync(Message);
            }
        }

        /// <summary>
        /// Runs server shutdown procedures (disconnects all clients)
        /// </summary>
        public virtual void Shutdown()
        {
            _serverRunning = false;
            _server.Stop();

            var _clientsCopy = new List<T>(_clients); //So I can run a foreach loop without stuff being removed in the middle
            foreach (var _client in _clientsCopy)
            {
                _client.Disconnect(new DisconnectArgs("Server shutdown"));
                //Removal of clients from _clients is handled by _clientDisconnected
            }

            ServerShutdown?.Invoke(this);
        }

        /// <summary>
        /// Starts up a TCP server (listener)
        /// </summary>
        /// <param name="Port">Port that the server listens on</param>
        /// </param>
        public NetServer(int Port)
        {
            _server = new TcpListener(IPAddress.Any, Port);
            _clients = new List<T>();

            _serverRunning = true;
            _newClientLoop();
        }
    }

    /// <summary>
    /// Example of how to handle new clients. In this case it was designed for a chat client (hence message forwarding)
    /// </summary>
    class ChatServer : NetServer<ChatClient>
    {
        public ChatServer(int Port)
            :base(Port)
        { }

        /// <summary>
        /// Sends a message to all clients except the client that sent the message.
        /// Mostly used when a message is received
        /// </summary>
        /// <param name="sender">Object that sent the message</param>
        /// <param name="e">Arguments associated with a message event</param>
        protected virtual void _forwardMessage(object sender, MsgEventArgs e)
        {
            foreach (var _client in _clients)
            {
                if (_client != sender)
                {
                    _client.SendAsync(e.Message);
                }
            }
        }

        /// <summary>
        /// Creates a new example client from a TcpClient
        /// </summary>
        /// <param name="Client">TcpClient to create from</param>
        protected override void _handleNewClient(TcpClient Client)
        {
            var newClient = new ChatClient(Client);
            _clients.Add(newClient);

            newClient.MessageReceived += _forwardMessage;
            newClient.Disconnected += _clientDisconnected;
        }
    }
}
