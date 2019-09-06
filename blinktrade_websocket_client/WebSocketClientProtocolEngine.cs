using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Blinktrade
{
    public class WebSocketClientProtocolEngine : IWebSocketClientProtocolEngine
    {
        public event EventHandler<SystemEventArgs> SystemEvent;

        public event LogStatusDelegate LogStatusEvent;

		public List<IWebSocketClientConnection> _connections = new List<IWebSocketClientConnection>();

        protected virtual void DispatchEvent(
								SystemEventType evtType, 
								IWebSocketClientConnection connection, 
								JObject json = null)
        {
            if (SystemEvent != null)
            {
                SystemEventArgs args = new SystemEventArgs();
                args.evtType = evtType;
                args.json = json;
                SystemEvent(connection, args);
            }
        }

		public List<IWebSocketClientConnection> GetConnections()
		{
			return _connections;
		}

		public void OnLogEvent(LogStatusType type, string message)
        {
            if (LogStatusEvent != null)
                LogStatusEvent(type, message);
        }

        public void OnOpen(IWebSocketClientConnection connection)
        {
            Debug.Assert(connection.IsConnected);
            Debug.Assert(!connection.IsLoggedOn);

            OnLogEvent(LogStatusType.INFO, "Connection Succeeded");

			_connections.Add(connection);

            // dispatch the connection opened
            DispatchEvent(SystemEventType.OPENED, connection);

            // build the json Login Request Message
            JObject login_request = new JObject();
            login_request["MsgType"] = "BE";
            login_request["ClientID"] = "1";
            login_request["UserReqID"] = connection.NextOutgoingSeqNum();
            login_request["UserReqTyp"] = "1";
            login_request["Username"] = connection.UserAccount.Username;
            login_request["Password"] = connection.UserAccount.Password;
			login_request["BrokerID"] = connection.UserAccount.BrokerId;
			login_request["CancelOnDisconnect"] = connection.CancelOnDisconnectFlag.ToString();
			if (connection.UserAccount.SecondFactor != null && connection.UserAccount.SecondFactor != string.Empty)
            {
                login_request["SecondFactor"] = connection.UserAccount.SecondFactor;
            }
            login_request["UserAgent"] = "Mozilla/5.0 (Linux; Android 6.0; Nexus 5 Build/MRA58N) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.76 Mobile Safari/537.36";
            login_request["UserAgentLanguage"] = "en-US";
            login_request["UserAgentTimezoneOffset"] = ":180,";
            login_request["UserAgentPlatform"] = "Linux x86_64";
            login_request["FingerPrint"] = connection.Device.FingerPrint;
            login_request["STUNTIP"] = connection.Device.Stuntip;

            // send the login request Message on wire
            connection.SendMessage(login_request.ToString());
        }

        public void OnMessage(string message, IWebSocketClientConnection connection)
        {
            JObject msg = JsonConvert.DeserializeObject<JObject>(message);
            string msgType = msg["MsgType"].Value<string>();

            switch (msgType)
            {
                case "BF": //Login response:
                    {

                        if (msg.GetValue("UserReqTyp") != null && msg.GetValue("UserReqTyp").Value<int>() == 3)
                        {
                            Debug.Assert(connection.IsLoggedOn);
                            DispatchEvent(SystemEventType.CHANGE_PASSWORD_RESPONSE, connection, msg);
                            break;
                        }

                        if (msg["UserStatus"].Value<int>() == 1)
                        {
                            Debug.Assert(!connection.IsLoggedOn);
                            connection.IsLoggedOn = true;
                            OnLogEvent(LogStatusType.INFO, "Received LOGIN_OK response");
                            DispatchEvent(SystemEventType.LOGIN_OK, connection, msg);

                        }
                        else
                        {
                            connection.IsLoggedOn = false;
                            OnLogEvent(LogStatusType.WARN, 
								"Received LOGIN_ERROR response : " + msg["UserStatusText"].Value<string>()
							);
                            DispatchEvent(SystemEventType.LOGIN_ERROR, connection, msg);
                            connection.Shutdown();
                        }
                        break;
                    }
                case "W":
                    Debug.Assert(connection.IsLoggedOn);
                    if (msg["MarketDepth"].Value<int>() != 1) // Has Market Depth 
                    {
                        DispatchEvent(SystemEventType.ORDER_BOOK_CLEAR, connection, msg);
                        DispatchEvent(SystemEventType.TRADE_CLEAR, connection, msg);

                        foreach (JObject entry in msg["MDFullGrp"])
                        {
                            entry["MDReqID"] = msg["MDReqID"];
                            switch (entry["MDEntryType"].Value<char>())
                            {
                                case '0': // Bid 
                                case '1': // Offer 
                                    entry["Symbol"] = msg["Symbol"];
                                    DispatchEvent(SystemEventType.ORDER_BOOK_NEW_ORDER, connection, entry);
                                    break;
                                case '2': // Trade 
                                    DispatchEvent(SystemEventType.TRADE, connection, entry);
                                    break;
                                case '4': // Trading Session Status
                                    DispatchEvent(SystemEventType.TRADING_SESSION_STATUS, connection, entry);
                                    break;
                            }
                        }
                    }

                    DispatchEvent(SystemEventType.MARKET_DATA_FULL_REFRESH, connection, msg);
                    break;

                case "X":
                    if (msg["MDBkTyp"].Value<int>() == 3) // Order Depth 
                    {
                        foreach (JObject entry in msg["MDIncGrp"])
                        {
                            entry["MDReqID"] = msg["MDReqID"];
                            switch (entry["MDEntryType"].Value<char>())
                            {
                                case '0': // Bid 
                                case '1': // Offer 
                                    switch (entry["MDUpdateAction"].Value<char>())
                                    {
                                        case '0':
                                            DispatchEvent(SystemEventType.ORDER_BOOK_NEW_ORDER, connection, entry);
                                            break;
                                        case '1':
                                            DispatchEvent(SystemEventType.ORDER_BOOK_UPDATE_ORDER, connection, entry);
                                            break;
                                        case '2':
                                            DispatchEvent(SystemEventType.ORDER_BOOK_DELETE_ORDER, connection, entry);
                                            break;
                                        case '3':
                                            DispatchEvent(SystemEventType.ORDER_BOOK_DELETE_ORDERS_THRU, connection, entry);
                                            break;
                                    }
                                    break;
                                case '2': // Trade 
                                    DispatchEvent(SystemEventType.TRADE, connection, entry);
                                    break;
                                case '4': // Trading Session Status 
                                    DispatchEvent(SystemEventType.TRADING_SESSION_STATUS, connection, entry);
                                    break;
                            }
                        }
                    }
                    else
                    {
                        // TODO:  Top of the book handling.
                    }
                    DispatchEvent(SystemEventType.MARKET_DATA_INCREMENTAL_REFRESH, connection, msg);
                    break;
                case "Y":
                    DispatchEvent(SystemEventType.MARKET_DATA_REQUEST_REJECT, connection, msg);
                    break;
                case "f":
                    DispatchEvent(SystemEventType.SECURITY_STATUS, connection, msg);
                    break;
                case "U3":
                    DispatchEvent(SystemEventType.BALANCE_RESPONSE, connection, msg);
                    break;
                case "U5":
                    DispatchEvent(SystemEventType.ORDER_LIST_RESPONSE, connection, msg);
                    break;
                case "8":  //Execution Report 
					if (msg.GetValue("Volume") == null || msg.GetValue("Volume").Type == JTokenType.Null)
                    {
						if (msg.GetValue("AvgPx") != null && msg.GetValue("AvgPx").Type != JTokenType.Null && msg.GetValue("AvgPx").Value<ulong>() > 0)
							msg["Volume"] = (ulong)(msg["CumQty"].Value<ulong>() * (float)(msg["AvgPx"].Value<ulong>() / 1e8));
                        else
                            msg["Volume"] = 0;
                    }
                    DispatchEvent(SystemEventType.EXECUTION_REPORT, connection, msg);
                    break;
				case "U33": // Trade History Response
					DispatchEvent(SystemEventType.TRADE_HISTORY_RESPONSE, connection, msg);
					break;
			case "U35": // Ledger List_Response
					DispatchEvent(SystemEventType.LEDGER_LIST_RESPONSE, connection, msg);
					break;
				case "U23":
					DispatchEvent(SystemEventType.DEPOSIT_REFRESH, connection, msg);
					break;
				case "0":
                    DispatchEvent(SystemEventType.HEARTBEAT, connection, msg);
                    break;
                case "ERROR":
                    OnLogEvent(LogStatusType.ERROR, msg.ToString());
                    DispatchEvent(SystemEventType.ERROR, connection, msg);
                    connection.Shutdown();
                    break;
                default:
                    {
                        Debug.Assert(connection.IsLoggedOn);
                        OnLogEvent(LogStatusType.WARN, "Unhandled message type : " + msgType);
                        break;
                    }
            }
        }

        public void OnError(string ErrorMessage, IWebSocketClientConnection webSocketConn)
        {
            OnLogEvent(LogStatusType.ERROR, ErrorMessage);
            DispatchEvent(SystemEventType.ERROR, webSocketConn);
        }

        public void OnClose(IWebSocketClientConnection webSocketConn)
        {
            Debug.Assert(!webSocketConn.IsConnected);
            webSocketConn.IsLoggedOn = false;
            OnLogEvent(LogStatusType.ERROR, "WebSocket closed.");
            DispatchEvent(SystemEventType.CLOSED, webSocketConn);
			bool bRetVal = _connections.Remove(webSocketConn);
			if (bRetVal) {
				OnLogEvent (LogStatusType.INFO, "Removed connection : " + webSocketConn.ToString());
			}
        }

        public void SendTestRequest(IWebSocketClientConnection connection)
        {
            JObject test_request = new JObject();
            test_request["MsgType"] = "1";
            test_request["FingerPrint"] = connection.Device.FingerPrint;
            test_request["STUNTIP"] = connection.Device.Stuntip;
            test_request["TestReqID"] = connection.NextOutgoingSeqNum();
            test_request["SendTime"] = (ulong) Util.ConvertToUnixTimestamp(DateTime.Now);
            string test_request_msg = test_request.ToString();
            connection.SendMessage(test_request_msg);
        }
    }
}
