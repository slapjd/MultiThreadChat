using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

using System.Diagnostics;

namespace MultiThreadChat.Networking
{
    delegate void MsgEventHandler(object sender, MsgEventArgs e);
    class MsgEventArgs : EventArgs
    {
        public byte[] Message;
        public MsgEventArgs(byte[] Message)
        {
            this.Message = Message;
        }
    }

    delegate void DisconnectHandler(object sender, DisconnectArgs e);
    class DisconnectArgs : EventArgs
    {
        public string Reason;
        public DisconnectArgs(string Reason)
        {
            this.Reason = Reason;
        }
    }

    class LocalDisconnectException : Exception
    {
        public LocalDisconnectException()
            : base()
        {
        }

        public LocalDisconnectException(string message)
            : base(message)
        {
        }

        public LocalDisconnectException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
    class RemoteDisconnectException : Exception
    {
        public RemoteDisconnectException()
            : base()
        {
        }

        public RemoteDisconnectException(string message)
            : base(message)
        {
        }

        public RemoteDisconnectException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Inherit from this and implement the methods for sending/receiving individual messages
    /// </summary>
    abstract class NetClient
    {
        /// <summary>
        /// Client object. Holds network socket, etc.
        /// </summary>
        protected TcpClient _client;
        /// <summary>
        /// Client stream object. Sends/receives data.
        /// </summary>
        protected NetworkStream _clientStream;

        public event MsgEventHandler MessageReceived;
        public event MsgEventHandler MessageSent;
        public event DisconnectHandler Disconnected;

        protected void _invokeMessageReceived(MsgEventArgs e) => MessageReceived?.Invoke(this, e);
        protected void _invokeMessageSent(MsgEventArgs e) => MessageSent?.Invoke(this, e);
        protected void _invokeDisconnected(DisconnectArgs e) => Disconnected.Invoke(this, e);

        /// <summary>
        /// Disconnects a client
        /// </summary>
        /// <param name="args">Arguments associated with disconnection events</param>
        public virtual void Disconnect(DisconnectArgs args)
        {
            _clientStream.Close();
            _clientStream.Dispose();
            _client.Close();
            _client.Dispose();

            Disconnected?.Invoke(this, args);
        }

        /// <summary>
        /// Send a byte[] message to the connected client
        /// </summary>
        /// <param name="Message">Message to send</param>
        public abstract void SendAsync(byte[] Message);

        /// <summary>
        /// Receives a single message and checks for disconnects of the client
        /// </summary>
        /// <returns>Message received in byte[] form</returns>
        protected abstract Task<byte[]> _recvMessageAsync();

        /// <summary>
        /// Loop to receive all incoming messages
        /// </summary>
        protected virtual async void _handleIncomingMessages()
        {
            while (_client.Connected)
            {
                try
                {
                    byte[] _message = await _recvMessageAsync();
                    MessageReceived?.Invoke(this, new MsgEventArgs(_message));
                }
                catch (Exception ex)
                {
                    if (ex is RemoteDisconnectException) //Disconnect procedure is not run yet if disconnect was remote
                    {
                        Disconnect(new DisconnectArgs("Remote host disconnected"));
                        break;
                    }
                    else if (ex is LocalDisconnectException)
                    {
                        break;
                    }

                    throw;
                }
            }
            //Debugger.Log(1, "DEBUG", "Client Recv Loop Ended" + Environment.NewLine);
        }

        /// <summary>
        /// Common initialisation steps for all constructor forms
        /// </summary>
        protected virtual void Init()
        {
            _clientStream = _client.GetStream();

            _handleIncomingMessages();
        }

        /// <summary>
        /// Initialises a client with a currently connected TcpClient
        /// </summary>
        /// <param name="Client">The TcpClient object to use as the internal client. Must be connected already.</param>
        public NetClient(TcpClient Client)
        {
            _client = Client;

            Init();
        }

        /// <summary>
        /// Initialises a client with an IP End point (IP address and port)
        /// </summary>
        /// <param name="ServerEndPoint">IP End point to connect to</param>
        public NetClient(IPEndPoint ServerEndPoint)
        {
            _client = new TcpClient();
            _client.Connect(ServerEndPoint);

            Init();
        }
    }

    /// <summary>
    /// Example of how to possibly implement sending/receiving of messages. Not necessarily the best way
    /// </summary>
    class ChatClient : NetClient
    {
        public ChatClient(TcpClient Client) : base(Client) { }
        public ChatClient(IPEndPoint ServerEndPoint) : base(ServerEndPoint) { }

        override public async void SendAsync(byte[] Message)
        {
            if (Message != null)
            {
                //Send the message length
                byte[] _messageLength = BitConverter.GetBytes(Message.Length);
                await _clientStream.WriteAsync(_messageLength, 0, _messageLength.Length);
                await _clientStream.FlushAsync();

                //Then send the message itself
                await _clientStream.WriteAsync(Message, 0, Message.Length);
                await _clientStream.FlushAsync();

                _invokeMessageSent(new MsgEventArgs(Message));
            }
        }

        override protected async Task<byte[]> _recvMessageAsync()
        {
            try
            {
                //Receive the message length (always a byte[4] because it's a converted integer)
                byte[] _lengthBuffer = new byte[4];
                await _clientStream.ReadAsync(_lengthBuffer, 0, _lengthBuffer.Length);
                int _messageLength = BitConverter.ToInt32(_lengthBuffer, 0);

                //Receive the message with a buffer matching the message length
                byte[] _message = new byte[_messageLength];
                await _clientStream.ReadAsync(_message, 0, _message.Length);

                if (_message.Length != 0)//When the other side disconnects, byte[0]s keep getting sent
                {
                    return _message;
                }
                else //If message is nothing, assume the client on the other side disconnected (by proper means)
                {
                    throw new RemoteDisconnectException("Remote host disconnected gracefully");
                }

            }
            catch (Exception ex)
            {
                if (ex is ObjectDisposedException && !_client.Connected) //Disconnect initiated locally, everything is fine already
                {
                    throw new LocalDisconnectException("Local client disconnected");
                }//*/
                else if (ex.InnerException is SocketException && !_client.Connected) //Disconnect happened remotely, but ungracefully when this happens
                {
                    throw new RemoteDisconnectException("Remote host disconnected ungracefully");
                }
                throw;
            }

        }
    }
}