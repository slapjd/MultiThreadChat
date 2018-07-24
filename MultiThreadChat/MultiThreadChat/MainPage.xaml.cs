using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

using MultiThreadChat.Networking;
using System.Net;
using System.Net.Sockets;

namespace MultiThreadChat
{
    public partial class MainPage : ContentPage
	{
        private string _txtSendDefaultText;
        private bool _txtSendWasModified = false;
        private ChatClient _client;
        private ChatServer _server;
        private bool _skipTextChangedEvent = true; //The event is loaded once at startup and the function needs to be skipped then

        public MainPage()
		{
			InitializeComponent();

            IPAddress[] ipAddresses = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress address in ipAddresses)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    txtServerIP.Text = address.ToString();
                }
            }

            _txtSendDefaultText = txtSend.Text;
        }

        /// <summary>
        /// Runs whenever the local client disconnects
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Arguments associated with client disconnects</param>
        private void _clientDisconnected(object sender, DisconnectArgs e)
        {
            _client = null;

            if (_server == null)
            {
                btnServerStart.IsEnabled = true;
            }
            btnConnect.IsEnabled = true;
            btnDisconnect.IsEnabled = false;

            txtSend.Text = "";
            txtSend_ChangeTextWithFocus(); //See _sendMessage for details
            txtSend.IsEnabled = false;
        }

        /// <summary>
        /// Runs whenever the local server is shut down
        /// </summary>
        /// <param name="sender">The object that fired the event</param>
        private void _serverShutdown(object sender)
        {
            _server = null;
            btnServerStart.IsEnabled = true;
            btnServerStop.IsEnabled = false;
        }

        /// <summary>
        /// Fired whenever a message is sent/received. Adds the message to lblLog
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Arguments associated with a message event</param>
        private void _logMessage(object sender, MsgEventArgs e)
        {
            string _message = Encoding.Unicode.GetString(e.Message);
            lblLog.Text = lblLog.Text + _message + Environment.NewLine;
        }

        /// <summary>
        /// Tells the local client to send a message from txtSend
        /// </summary>
        private void _sendMessage()
        {
            if (_txtSendWasModified)
            {
                string _message = "<" + txtUsername.Text + "> " + txtSend.Text;
                byte[] _messageBuffer = Encoding.Unicode.GetBytes(_message);

                _client.SendAsync(_messageBuffer);

                //Remember that the textchanged event shouldn't be skipped here (we're resetting the textbox)
                txtSend.Text = "";
                //Sometimes this is called before TextChanged so I need to run it again
                txtSend_ChangeTextWithFocus();
            }
        }

        /// <summary>
        /// Changes the current value of txtSend based on keyboard focus on txtSend.
        /// Used to provide the disappearing "Enter Message" text
        /// </summary>
        private void txtSend_ChangeTextWithFocus()
        {
            if (txtSend.IsFocused == true)
            {
                if (!_txtSendWasModified)
                {
                    _skipTextChangedEvent = true;
                    txtSend.Text = "";
                }
            }
            else if (txtSend.Text == "")
            {
                _skipTextChangedEvent = true;
                txtSend.Text = _txtSendDefaultText;
            }
        }

        /// <summary>
        /// Fired whenever the focus is changed on txtSend (focused or unfocused)
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void txtSend_FocusChanged(object sender, FocusEventArgs e)
        {
            txtSend_ChangeTextWithFocus();
        }

        /// <summary>
        /// Fired whenever the text changes in txSend.
        /// Some functions are skipped based on a flag in MainWindow
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void txtSend_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_skipTextChangedEvent)
            {
                if (txtSend.Text == "")
                {
                    _txtSendWasModified = false;
                    btnSend.IsEnabled = false;
                }
                else
                {
                    _txtSendWasModified = true;
                    btnSend.IsEnabled = true;
                }
            }
            else
            {
                _skipTextChangedEvent = false;
            }
        }

        /// <summary>
        /// Fired when btnServerStart is clicked
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void btnServerStart_Clicked(object sender, EventArgs e)
        {
            int _port = 0;
            if (int.TryParse(txtServerPort.Text, out _port))
            {
                _server = new ChatServer(_port);
                _server.ServerShutdown += _serverShutdown;

                btnServerStart.IsEnabled = false;
                btnServerStop.IsEnabled = true;
            }
            else
            {
                DisplayAlert("Invalid Port", "Please enter a valid port", "OK");
            }
        }

        /// <summary>
        /// Fired when btnConnect is clicked
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void btnConnect_Clicked(object sender, EventArgs e)
        {
            IPAddress _address;
            int _port = 0;
            if (IPAddress.TryParse(txtServerIP.Text, out _address))
            {
                if (int.TryParse(txtServerPort.Text, out _port) && 0 < _port && _port < 65536)
                {
                    _client = new ChatClient(new IPEndPoint(IPAddress.Parse(txtServerIP.Text), Convert.ToInt32(txtServerPort.Text)));
                    btnConnect.IsEnabled = false;
                    btnServerStart.IsEnabled = false;
                    btnDisconnect.IsEnabled = true;

                    txtSend.IsEnabled = true;

                    _client.MessageReceived += _logMessage;
                    _client.MessageSent += _logMessage;
                    _client.Disconnected += _clientDisconnected;
                }
                else
                {
                    DisplayAlert("Invalid Port", "Please enter a valid port", "OK");
                }
            }
            else
            {
                DisplayAlert("Invalid IP", "Please enter a valid IP address", "OK");
            }
        }

        /// <summary>
        /// Fired when btnServerStop is clicked
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void btnServerStop_Clicked(object sender, EventArgs e)
        {
            _server.Shutdown();
        }

        /// <summary>
        /// Fired when btnDisconnect is clicked
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void btnDisconnect_Clicked(object sender, EventArgs e)
        {
            _client.Disconnect(new DisconnectArgs("Local disconnect requested"));
        }

        /// <summary>
        /// Fired when btnSend is clicked
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void btnSend_Clicked(object sender, EventArgs e)
        {
            _sendMessage();
        }
    }
}
