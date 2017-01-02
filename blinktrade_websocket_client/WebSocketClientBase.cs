using System;
using System.Threading;

namespace Blinktrade
{
	public class WebSocketClientBase : TestRequestDispatcher
	{
		public WebSocketClientBase (UserAccountCredentials account, 
									UserDevice device, 
									WebSocketClientProtocolEngine protocolEngine)
		{
			_account = account;
			_device = device;
			_protocolEngine = protocolEngine;
		}

		protected IWebSocketClientProtocolEngine _protocolEngine = null;
		private UserAccountCredentials _account = null;
		private UserDevice _device = null;
		private int _seqnum = 0;
		protected long _receiveMessageCounter = 0;
		private bool _loggedOn = false;

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