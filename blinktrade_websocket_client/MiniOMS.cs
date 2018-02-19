using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blinktrade
{
    public class MiniOMS
    {
        private Dictionary<string, MiniOMS.Order> m_orders = new Dictionary<string, Order>();
		//private ulong _lastBuyPrice = 0;
		//private ulong _lastSellPrice = 0;
		private ulong _maxBuyPrice = ulong.MinValue;
		private ulong _minSellPrice = ulong.MaxValue;

		public ulong MaxBuyPrice {get { return _maxBuyPrice;} }
		public ulong MinSellPrice {get { return _minSellPrice;} }

        public void AddOrder(Order order)
        {
            m_orders.Add(order.ClOrdID, order);
			if (order.CumQty > 0) // the order had a trade
			{
				// make sure it less than 24 hours ago
				DateTime minDateTime = DateTime.UtcNow - new TimeSpan(24, 0, 0);
				if (order.OrderDate > minDateTime) 
				{
					if (order.Side == OrderSide.BUY && order.AvgPx >_maxBuyPrice ) {
						_maxBuyPrice = order.AvgPx;
					} else if (order.Side == OrderSide.SELL && order.AvgPx <_minSellPrice ) {
						_minSellPrice = order.AvgPx;
					}	
				}
			}
        }

        public Order GetOrderByClOrdID(string clOrdID)
        {
            if (!string.IsNullOrEmpty(clOrdID))
            {
                Order order = null;
                if (m_orders.TryGetValue(clOrdID, out order))
                    return order;
            }
            return null;
        }


        public Order GetOrderByOrderID(ulong orderId)
        {
            throw new NotImplementedException();
        }


        public bool RemoveOrderByClOrdID(string clOrdID)
        {
            return m_orders.Remove(clOrdID);
        }

        public override string ToString()
        {
            StringBuilder sbuilder = new StringBuilder();
            sbuilder.AppendLine("ClOrdID;OrderID;OrdStatus;Symbol;Side;Price;OrderQty;LeavesQty;CumQty;AvgPx;OrderDate");
            foreach (KeyValuePair<string, Order> kvp in m_orders)
            {
                sbuilder.Append(kvp.Value.ClOrdID.ToString() + ';');
                sbuilder.Append(kvp.Value.OrderID.ToString() + ';');
                sbuilder.Append(kvp.Value.OrdStatus.ToString() + ';');
                sbuilder.Append(kvp.Value.Symbol.ToString() + ';');
                sbuilder.Append(kvp.Value.Side.ToString() + ';');
                sbuilder.Append(kvp.Value.Price.ToString() + ';');
                sbuilder.Append(kvp.Value.OrderQty.ToString() + ';');
                sbuilder.Append(kvp.Value.LeavesQty.ToString() + ';');
                sbuilder.Append(kvp.Value.CumQty.ToString() + ';');
                sbuilder.Append(kvp.Value.AvgPx.ToString() + ';');
                sbuilder.Append(kvp.Value.OrderDate.ToString() + ';');
                sbuilder.AppendLine();
            }
            return sbuilder.ToString();
        }

        public interface IOrder
        {
            string ClOrdID { get; }
            char OrdStatus { get; }
            char Side { get; }
            ulong OrderID { get; }
            ulong CumQty { get; }
            ulong LeavesQty { get; }
            ulong CxlQty { get; }
            ulong AvgPx { get; }
            ulong LastShares { get; }
            ulong LastPx { get; }
            string Symbol { get; }
            char OrdType { get; }
            ulong OrderQty { get; }
            ulong Price { get; }
            ulong Volume { get; }
            DateTime OrderDate { get; }
            char TimeInForce { get; }
			ulong StopPx { get; }
        }

        public class Order : IOrder
        {
            private string _clOrdID;

            public string ClOrdID
            {
                get { return _clOrdID; }
                set { _clOrdID = value; }
            }

            private ulong _orderID = 0;

            public ulong OrderID
            {
                get { return _orderID; }
                set { _orderID = value; }
            }

            private ulong _cumQty = 0;

            public ulong CumQty
            {
                get { return _cumQty; }
                set { _cumQty = value; }
            }

            private char _ordStatus = Blinktrade.OrdStatus.PENDING_NEW;

            public char OrdStatus
            {
                get { return _ordStatus; }
                set { _ordStatus = value; }
            }

            private ulong _leavesQty = 0;

            public ulong LeavesQty
            {
                get { return _leavesQty; }
                set { _leavesQty = value; }
            }

            private ulong _cxlQty = 0;

            public ulong CxlQty
            {
                get { return _cxlQty; }
                set { _cxlQty = value; }
            }

            private ulong _avgPx = 0;

            public ulong AvgPx
            {
                get { return _avgPx; }
                set { _avgPx = value; }
            }

            private ulong _lastShares = 0;

            public ulong LastShares
            {
                get { return _lastShares; }
                set { _lastShares = value; }
            }

            private ulong _lastPx = 0;

            public ulong LastPx
            {
                get { return _lastPx; }
                set { _lastPx = value; }
            }

            private string _symbol;

            public string Symbol
            {
                get { return _symbol; }
                set { _symbol = value; }
            }

            private char _side = default(char);

            public char Side
            {
                get { return _side; }
                set { _side = value; }
            }

            private char _ordType;

            public char OrdType
            {
                get { return _ordType; }
                set { _ordType = value; }
            }

            private ulong _orderQty = 0;

            public ulong OrderQty
            {
                get { return _orderQty; }
                set { _orderQty = value; }
            }

            private ulong _price = 0;

            public ulong Price
            {
                get { return _price; }
                set { _price = value; }
            }

            private ulong _volume = 0;

            public ulong Volume
            {
                get { return _volume; }
                set { _volume = value; }
            }

			private DateTime _orderDate;// = string.Empty;

            public DateTime OrderDate
            {
                get { return _orderDate; }
                set { _orderDate = value; }
            }

            private char _timeInForce = Blinktrade.TimeInForce.GOOD_TILL_CANCEL; // all blinktrade orders are GTC

            public char TimeInForce
            {
                get { return _timeInForce; }
                set { _timeInForce = value; }
            }

			private ulong _stopPx = 0;

			public ulong StopPx
			{
				get { return _stopPx; }
				set { _stopPx = value; }
			}
        }
    }
}