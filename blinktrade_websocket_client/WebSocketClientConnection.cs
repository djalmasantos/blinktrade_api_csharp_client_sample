using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json.Linq;


namespace Blinktrade
{
	class WebSocketClientConnection : WebSocketClientBase, IWebSocketClientConnection 
    {
        private ClientWebSocket _webSocket = null;
        private BufferBlock<string> _sendQueue = new BufferBlock<string>();
        private const int _sendChunkSize = 8192;
        private const int _receiveChunkSize = 8192;

        public bool IsConnected
        {
            get
            {
                return (_webSocket != null && _webSocket.State == WebSocketState.Open);
            }
        }

		WebSocketClientConnection(UserAccountCredentials account, 
									UserDevice device, 
									WebSocketClientProtocolEngine protocolEngine)
		: base (account, device, protocolEngine)
		{
        
		}

        public static async Task Start(string serverUri, 
										UserAccountCredentials account, 
										UserDevice device, 
										WebSocketClientProtocolEngine protocolEngine)
        {
            WebSocketClientConnection connectionInstance = new WebSocketClientConnection(account, device, protocolEngine);
            try
            {
                connectionInstance._webSocket = new ClientWebSocket();
                await connectionInstance._webSocket.ConnectAsync(new Uri(serverUri), CancellationToken.None);
                connectionInstance._protocolEngine.OnOpen(connectionInstance);
                await Task.WhenAll(Receive(connectionInstance), Send(connectionInstance), TestRequest(connectionInstance));
                connectionInstance._protocolEngine.OnClose(connectionInstance);
            }
            catch (Exception ex)
            {
                connectionInstance._protocolEngine.OnError((ex.InnerException != null ? ex.Message + ". " + ex.InnerException.Message : 
					ex.Message) + '\n' + 
					ex.StackTrace, connectionInstance);
            }
            finally
            {
                if (connectionInstance._webSocket != null)
                    connectionInstance._webSocket.Dispose();
            }
        }

        public void SendMessage(string message)
        {
            _sendQueue.Post(message);
        }

        public void SendTestRequest()
        {
            _protocolEngine.SendTestRequest(this);
        }

        public void Shutdown()
        {
            Debug.Assert(_webSocket != null);
            if (_webSocket != null)
            {
                _webSocket.Abort();
                SendMessage(""); // workaround to wake up and terminate the Send Task with an empty string
            }
        }

        private static async Task Send(WebSocketClientConnection connection)
        {
            ArraySegment<byte> bytesToSend = new ArraySegment<byte>(new byte[_sendChunkSize]);

            while (connection._webSocket.State == WebSocketState.Open)
            {
                string nextMessageToSend = await connection._sendQueue.ReceiveAsync();
                if (nextMessageToSend == null || nextMessageToSend.Length == 0)
                {
                    connection._protocolEngine.OnLogEvent(LogStatusType.WARN, "Received signal to terminate the Task");
                    return;
                }

				// Send the message (buffer is automagically resized when input is larger)
                bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(nextMessageToSend)); 
                await connection._webSocket.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None);

                // dispatch log event for the sent message
                connection._protocolEngine.OnLogEvent(LogStatusType.MSG_OUT, nextMessageToSend);
            }
        }

        private static async Task Receive(WebSocketClientConnection connection)
        {
            ArraySegment<byte> bytesReceived = new ArraySegment<byte>(new byte[_receiveChunkSize]);
            while (connection._webSocket.State == WebSocketState.Open)
            {
                // receive data from the the socket
                WebSocketReceiveResult result = null;
                using (var ms = new MemoryStream())
                {
                    do
                    {
                        result = await connection._webSocket.ReceiveAsync(bytesReceived, CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await connection._webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
																	string.Empty, 
																	CancellationToken.None);
                            return;
                        }
                        ms.Write(bytesReceived.Array, bytesReceived.Offset, result.Count);
                    }
                    while (!result.EndOfMessage);

                    // increment the number of received messages to help the keep-alive mechanism
                    Interlocked.Increment(ref connection._receiveMessageCounter);

                    // retrieve the whole message from the stream
                    ms.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(ms, Encoding.UTF8))
                    {
                        string all_received_data = reader.ReadToEnd();
                        // dispatch log event and message callback for the received message
                        connection._protocolEngine.OnLogEvent(LogStatusType.MSG_IN, all_received_data);
                        connection._protocolEngine.OnMessage(all_received_data, connection);
                    }
                }
            }
        }
    }
}
