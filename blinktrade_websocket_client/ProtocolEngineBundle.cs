using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Blinktrade
{
    // interface of the protocol engine to provide access to callback events
    public interface IWebSocketClientProtocolEngine
    {
        void OnOpen(IWebSocketClientConnection connection);
        void OnClose(IWebSocketClientConnection connection);
        void OnMessage(string message, IWebSocketClientConnection connection);
        void OnError(string errorMessage, IWebSocketClientConnection connection);
        void OnLogEvent(LogStatusType logType, string message);
        void SendTestRequest(IWebSocketClientConnection connection);
        event EventHandler<SystemEventArgs> SystemEvent;
        event LogStatusDelegate LogStatusEvent;
    }

    // interface of the websocket connection endpoint
    public interface IWebSocketClientConnection
    {
        void SendMessage(string message);
        void SendTestRequest();
        void Shutdown();
        int NextOutgoingSeqNum();
        void OnLogEvent(LogStatusType logType, string message);
        bool IsConnected { get; }
        bool IsLoggedOn { get; set; }
        bool EnableTestRequest { get; set; }
        long receivedMessageCounter { get; }
        UserDevice Device { get; }
        UserAccountCredentials UserAccount { get; }
    }

    // event arguments definition
    public class SystemEventArgs : EventArgs
    {
        public SystemEventType evtType { get; set; }
        public JObject json { get; set; }
    }
    
    // define a delegate to handle the Log activity
    public delegate void LogStatusDelegate(LogStatusType logtype, string message);

    // define log activity types
    public enum LogStatusType { MSG_IN, MSG_OUT, INFO, WARN, ERROR };

    // events that the IWebSocketClientProtocolEngine might dispatch
    public enum SystemEventType
    {
        CLOSED,
        ERROR,
        OPENED,

        RAW_MESSAGE,
        SENT_RAW_MESSAGE,
        ERROR_MESSAGE,

        LOGIN_OK,
        LOGIN_ERROR,
        CHANGE_PASSWORD_RESPONSE,

        NEWS,

        // Passwords
        TWO_FACTOR_SECRET,
        PASSWORD_CHANGED_OK,
        PASSWORD_CHANGED_ERROR,

        // Profile
        UPDATE_PROFILE_RESPONSE,
        PROFILE_REFRESH,

        // Deposits
        DEPOSIT_METHODS_RESPONSE,
        DEPOSIT_RESPONSE,
        DEPOSIT_REFRESH,
        PROCESS_DEPOSIT_RESPONSE,
        DEPOSIT_LIST_RESPONSE,

        // Withdraws
        WITHDRAW_RESPONSE,
        WITHDRAW_CONFIRMATION_RESPONSE,
        WITHDRAW_LIST_RESPONSE,
        WITHDRAW_REFRESH,
        PROCESS_WITHDRAW_RESPONSE,

        // Positions & balance
        POSITION_RESPONSE,
        BALANCE_RESPONSE,

        // Trading
        ORDER_LIST_RESPONSE,
        HEARTBEAT,
        EXECUTION_REPORT,

        // Securities
        SECURITY_LIST,
        SECURITY_STATUS,

        // Trade History
        TRADE_HISTORY,
        TRADE_HISTORY_RESPONSE,

        TRADERS_RANK_RESPONSE,
        LEDGER_LIST_RESPONSE,

        // API Key
        API_KEY_LIST_RESPONSE,
        API_KEY_REVOKE_RESPONSE,
        API_KEY_CREATE_RESPONSE,

        /* Card */
        CARD_LIST_RESPONSE,
        CARD_DISABLE_RESPONSE,
        CARD_CREATE_RESPONSE,

        // Brokers
        BROKER_LIST_RESPONSE,
        CUSTOMER_LIST_RESPONSE,
        CUSTOMER_DETAIL_RESPONSE,
        VERIFY_CUSTOMER_RESPONSE,
        VERIFY_CUSTOMER_UPDATE,

        // Market Data
        MARKET_DATA_FULL_REFRESH,
        MARKET_DATA_INCREMENTAL_REFRESH,
        MARKET_DATA_REQUEST_REJECT,

        LINE_OF_CREDIT_LIST_RESPONSE,
        GET_LINE_OF_CREDIT_RESPONSE,
        PAY_LINE_OF_CREDIT_RESPONSE,

        TRADING_SESSION_STATUS,
        TRADE,
        TRADE_CLEAR,
        ORDER_BOOK_CLEAR,
        ORDER_BOOK_DELETE_ORDERS_THRU,
        ORDER_BOOK_DELETE_ORDER,
        ORDER_BOOK_NEW_ORDER,
        ORDER_BOOK_UPDATE_ORDER
    };   
}
