using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Net.Sockets;

using MultiThreadChat.Networking;

namespace MultiThreadChat
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _txtSendDefaultText;
        private bool _txtSendWasModified = false;
        private ExampleClient _client;
        private NetServer<ExampleClient> _server;
        private bool _skipTextChangedEvent = true; //The event is loaded once at startup and the function needs to be skipped then

        /// <summary>
        /// Constructor.
        /// Attempts to automatically set the IP Address box to the local ip address
        /// and initialises the _txtSendDefaultText value to whatever is in txtSend by default
        /// </summary>
        public MainWindow()
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
            btnServerConnect.IsEnabled = true;
            btnServerDisconnect.IsEnabled = false;

            txtSend.Text = "";
            txtSend_ChangeTextWithKeyboardFocus(); //See _sendMessage for details
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
        /// Fired whenever a message is sent/received. Adds the message to txtLog
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Arguments associated with a message event</param>
        private void _logMessage(object sender, MsgEventArgs e)
        {
            string _message = Encoding.Unicode.GetString(e.Message);
            txtLog.AppendText(_message + Environment.NewLine);
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
                txtSend_ChangeTextWithKeyboardFocus();
            }
        }

        /// <summary>
        /// Changes the current value of txtSend based on keyboard focus on txtSend.
        /// Used to provide the disappearing "Enter Message" text
        /// </summary>
        private void txtSend_ChangeTextWithKeyboardFocus()
        {
            if (txtSend.IsKeyboardFocused == true)
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
        private void txtSend_IsKeyboardFocusedChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            txtSend_ChangeTextWithKeyboardFocus();
        }

        /// <summary>
        /// Fired whenever a key is pushed down and txtSend is focused
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void txtSend_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) //Send a message when enter is pushed
            {
                _sendMessage();
            }
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
        private void btnServerStart_Click(object sender, RoutedEventArgs e)
        {
            int _port = 0;
            if (int.TryParse(txtServerPort.Text, out _port))
            {
                _server = new NetServer<ExampleClient>(_port, t => new ExampleClient(t));
                _server.ServerShutdown += _serverShutdown;

                btnServerStart.IsEnabled = false;
                btnServerStop.IsEnabled = true;
            }
            else
            {
                MessageBox.Show("Please enter a valid port");
            }
        }

        /// <summary>
        /// Fired when btnServerConnect is clicked
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void btnServerConnect_Click(object sender, RoutedEventArgs e)
        {
            IPAddress _address;
            int _port = 0;
            if (IPAddress.TryParse(txtServerIP.Text, out _address))
            {
                if (int.TryParse(txtServerPort.Text, out _port) && 0 < _port && _port < 65536)
                {
                    _client = new ExampleClient(new IPEndPoint(IPAddress.Parse(txtServerIP.Text), Convert.ToInt32(txtServerPort.Text)));
                    btnServerConnect.IsEnabled = false;
                    btnServerStart.IsEnabled = false;
                    btnServerDisconnect.IsEnabled = true;

                    txtSend.IsEnabled = true;

                    _client.MessageReceived += _logMessage;
                    _client.MessageSent += _logMessage;
                    _client.Disconnected += _clientDisconnected;
                }
                else
                {
                    MessageBox.Show("Please enter a valid port");
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid IP address");
            }
        }

        /// <summary>
        /// Fired when btnServerStop is clicked
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void btnServerStop_Click(object sender, RoutedEventArgs e)
        {
            _server.Shutdown();
        }

        /// <summary>
        /// Fired when btnServerDisconnect is clicked
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void btnServerDisconnect_Click(object sender, RoutedEventArgs e)
        {
            _client.Disconnect(new DisconnectArgs("Local disconnect requested"));
        }

        /// <summary>
        /// Fired when btnSend is clicked
        /// </summary>
        /// <param name="sender">Object that fired the event</param>
        /// <param name="e">Event arguments</param>
        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            _sendMessage();
        }
    }
}
