#if __MonoCS__
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace Blinktrade
{
	public class WebSocketSharpClientConnection : WebSocketClientBase, IWebSocketClientConnection 
	{
		private WebSocket _webSocket = null;

		WebSocketSharpClientConnection(
			UserAccountCredentials account, 
			UserDevice device, 
			WebSocketClientProtocolEngine protocolEngine
		) : base (account, device, protocolEngine)
		{
			
		}

		public bool IsConnected
		{
			get
			{
				return (_webSocket != null && _webSocket.ReadyState == WebSocketState.Open);
			}
		}

		public static async Task Start(
			string serverUri, 
			UserAccountCredentials account, 
			UserDevice device, 
			WebSocketClientProtocolEngine protocolEngine)
		{

			WebSocketSharpClientConnection connectionInstance = new WebSocketSharpClientConnection(account, device, protocolEngine);
			try
			{
				WebSocket ws  = new WebSocket(serverUri);
				connectionInstance._webSocket = ws;

				ws.OnOpen += (sender, e) => 
				{ 
					connectionInstance._protocolEngine.OnOpen(connectionInstance);
				};

				ws.OnMessage += (sender, e) => 
				{
					if ( !e.IsPing ) 
					{
						// increment the number of received messages to help the keep-alive mechanism
						Interlocked.Increment(ref connectionInstance._receiveMessageCounter);
						// invoke the callbacks
						connectionInstance._protocolEngine.OnLogEvent(LogStatusType.MSG_IN, e.Data);
						connectionInstance._protocolEngine.OnMessage(e.Data, connectionInstance);
					}
				};


				ws.OnError += (sender, e) => 
				{
					connectionInstance._protocolEngine.OnError(e.Message, connectionInstance);
				};

				ws.OnClose += (sender, e) => 
				{
					connectionInstance._protocolEngine.OnClose(connectionInstance);
					connectionInstance._protocolEngine.OnLogEvent(LogStatusType.ERROR, 
						String.Format("WebSocket Close ({0} {1})", e.Code, e.Reason)
					);
				};

				connectionInstance._webSocket.Connect();
				await Task.WhenAll( TestRequest(connectionInstance));
			}
			catch (Exception ex)
			{
				string msg = (ex.InnerException != null ? ex.Message + ". " + ex.InnerException.Message : ex.Message) + '\n' + ex.StackTrace;
				connectionInstance._protocolEngine.OnError(msg, connectionInstance);
			}
			finally
			{
				if (connectionInstance._webSocket != null)
					((IDisposable) connectionInstance._webSocket).Dispose();
			}
		}

		public void SendMessage(string message)
		{
			_webSocket.Send(message);
			_protocolEngine.OnLogEvent(LogStatusType.MSG_OUT, message);
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
				_webSocket.Close();
			}
		}
	}
}
#endif