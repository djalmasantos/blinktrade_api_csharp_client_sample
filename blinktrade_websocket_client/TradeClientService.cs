using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blinktrade
{
    public interface ITradeClientService
    {
        MiniOMS miniOMS { get; }
        
		OrderBook GetOrderBook(string symbol);
		SecurityStatus GetSecurityStatus(string market, string symbol);

		string GetTradingSymbol();

		ulong UserId { get; }
        int BrokerId { get; }
        
		string SendOrder(IWebSocketClientConnection connection, 
			string symbol, ulong qty, ulong price, 
			char side, int broker_id, string client_order_id, 
			char order_type = OrdType.LIMIT, ulong stop_price = 0, 
            char execInst = ExecInst.DEFAULT,
            char time_in_force = TimeInForce.GOOD_TILL_CANCEL);
        
		bool CancelOrderByClOrdID(IWebSocketClientConnection connection, string clOrdID, bool force_unconfirmed_order_cancellation = false);
       // bool ForceCancelOrderByClOrdID(IWebSocketClientConnection connection, string clOrdID);

        ulong GetBalance(string currency);

		ulong GetSoldAmount(/*string symbol*/);
		ulong CalculateVWAP(/*string symbol*/);
		ulong GetLastPrice(/*string symbol*/);

		//ulong GetBoughtAmount()
		//ulong ResetSoldAmount();
		//ulong ResetBoughAmount()
    }
}