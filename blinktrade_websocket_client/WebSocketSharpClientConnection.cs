#if __MonoCS__
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace Blinktrade
{
	public class WebSocketClientConnection : WebSocketClientBase, IWebSocketClientConnection 
	{
		private WebSocket _webSocket = null;

		WebSocketClientConnection(
			UserAccountCredentials account, 
			UserDevice device, 
			WebSocketClientProtocolEngine protocolEngine,
			int cancel_on_disconnect_flag
		) : base (account, device, protocolEngine, cancel_on_disconnect_flag)
		{
			
		}

		public bool IsConnected
		{
			get
			{
				return (_webSocket != null && _webSocket.ReadyState == WebSocketState.Open);
			}
		}

		public static async Task/*<int>*/ Start(
			string serverUri, 
			UserAccountCredentials account, 
			UserDevice device, 
			WebSocketClientProtocolEngine protocolEngine,
			COOFlag cancel_open_orders_flag)
		{

			int cancel_on_disconnect = (cancel_open_orders_flag & COOFlag.CANCEL_ON_DISCONNECT) != 0 ? 1 : 0;
			WebSocketClientConnection connectionInstance = new WebSocketClientConnection(account, device, protocolEngine, cancel_on_disconnect);
			try
			{
				WebSocket ws  = new WebSocket(serverUri);
                // ws.Origin = "http://blinktrade.com";
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
			//return 0;
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