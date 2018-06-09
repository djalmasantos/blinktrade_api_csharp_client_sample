using System;
using System.Threading;

namespace Blinktrade
{
	public class WebSocketClientBase : TestRequestDispatcher
	{
		public WebSocketClientBase (UserAccountCredentials account, 
									UserDevice device, 
									WebSocketClientProtocolEngine protocolEngine,
									int cancel_on_disconnect_flag)
		{
			_account = account;
			_device = device;
			_protocolEngine = protocolEngine;
			_cancel_on_disconnect_flag = cancel_on_disconnect_flag;
		}

		protected IWebSocketClientProtocolEngine _protocolEngine = null;
		private UserAccountCredentials _account = null;
		private UserDevice _device = null;
		private int _seqnum = 0;
		protected long _receiveMessageCounter = 0;
		private bool _loggedOn = false;
		private int _cancel_on_disconnect_flag;

		public bool IsLoggedOn
		{
			get
			{
				return _loggedOn;
			}
			set
			{
				_loggedOn = value;
			}
		}

		public UserDevice Device
		{
			get
			{
				return _device;
			}
		}

		public UserAccountCredentials UserAccount
		{
			get
			{
				return _account;
			}
		}

		public int CancelOnDisconnectFlag 
		{
			get 
			{
				return _cancel_on_disconnect_flag;
			}
		}

		public long receivedMessageCounter 
		{
			get { return Interlocked.Read(ref _receiveMessageCounter); }
		}

		public int NextOutgoingSeqNum()
		{
			return Interlocked.Increment(ref _seqnum);
		}

		public void OnLogEvent(LogStatusType logType, string message)
		{
			_protocolEngine.OnLogEvent(logType, message);
		}
	}
}