using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Blinktrade
{
    public class OrderBook
    {
        private List<Order> _buyside = new List<Order>();
        private List<Order> _sellside = new List<Order>();
        private string _symbol;

        public interface IOrder
        {
            ulong Price { get; }
            ulong Qty { get; }
            ulong UserId { get; }
            string Broker { get; }
            ulong OrderId { get; }
            char Side { get; }
            string OrderTime { get; }
            string OrderDate { get; }
        }

        public class OrdSide
        {
            public const char BUY = '0';
            public const char SELL = '1';
        }


        public class Order : IOrder
        {
            private ulong _price = 0;
            private ulong _qty = 0;
            private ulong _user_id = 0;
            private string _broker = string.Empty;
            private ulong _order_id = 0;
            private char _side = default(char);
            private string _order_time = string.Empty;
            private string _order_date = string.Empty;

			public Order()
			{
			}

			public Order(char side, ulong price)
			{
				_side  = side;
				_price = price;
			}

			public ulong Price
            {
                get { return _price; }
                set { _price = value; }
            }

            public ulong Qty
            {
                get { return _qty; }
                set { _qty = value; }
            }

            public ulong UserId
            {
                get { return _user_id; }
                set { _user_id = value; }
            }

            public string Broker
            {
                get { return _broker; }
                set { _broker = value; }
            }

            public ulong OrderId
            {
                get { return _order_id; }
                set { _order_id = value; }
            }

            public char Side
            {
                get { return _side; }
                set { _side = value; }
            }

            public string OrderTime
            {
                get { return _order_time; }
                set { _order_time = value; }
            }

            public string OrderDate
            {
                get { return _order_date; }
                set { _order_date = value; }
            }
        }

		// helper class to binarySearch the Sell Book orders
		public class OrderPriceComparer: IComparer<OrderBook.Order>
		{
			public int Compare(OrderBook.Order order1, OrderBook.Order order2)
			{
				return CompareImpl (order1, order2);	
			}

			public static int CompareImpl(OrderBook.Order order1, OrderBook.Order order2)
			{
				if (order1 != null && order2 != null) 
				{
					Debug.Assert(order1.Side == order2.Side);

					if (order1.Price > order2.Price)
						return 1;
					else if (order1.Price < order2.Price)
						return -1;
					else
						return 0;
				}
				else 
				{
					if (order1 == null && order2 == null)
						return 0;
					else
						return (order1 != null ? 1 : -1);
				}
			}
		}

		// helper class to binarySearch the Buy Book orders
		public class ReverseOrderPriceComparer: IComparer<OrderBook.Order>
		{
			public int Compare(OrderBook.Order order1, OrderBook.Order order2)
			{
				// Compare order1 and order2 in reverse order.
				return OrderPriceComparer.CompareImpl(order2, order1);	
			}
		}

		// OrderBook class constructor
		public OrderBook(string symbol)
        {
            _symbol = symbol;
        }

        public string Symbol
        {
            get
            {
                return _symbol;
            }
        }

        public IOrder BestOffer
        {
            get { return _sellside.Count() > 0 ? _sellside[0] : null; }
        }

        public IOrder BestBid
        {
            get { return _buyside.Count() > 0 ? _buyside[0] : null; }
        }

        public void Clear()
        {
            _buyside.Clear();
            _sellside.Clear();
        }

		public List<Order> GetBidOrders()
		{
			return _buyside;
		}

		public List<Order> GetOfferOrders()
		{
			return _sellside;
		}

		public bool DoesAmountExceedsLimit(char side, int depth, ulong max_amount_limit)
		{
			List<Order> orderList = null;
			if (side == OrderBook.OrdSide.BUY)
				orderList = _buyside;
			else if (side == OrderBook.OrdSide.SELL)
				orderList = _sellside;
			else
				throw new System.ArgumentException("Invalid OrderBook Side : " + side);

			ulong amount = 0;
			for (int i = depth-1; i >= 0; --i) 
			{
				amount += orderList[i].Qty;
				if (amount > max_amount_limit)
					return true;
			}
			return false;
		}

		public ulong MaxPriceForAmountWithoutSelfOrders(char side, ulong target_amount, ulong user_id)
		{
			List<Order> orderList = null;
			if (side == OrderBook.OrdSide.BUY)
				orderList = _buyside;
			else if (side == OrderBook.OrdSide.SELL)
				orderList = _sellside;
			else
				throw new System.ArgumentException("Invalid OrderBook Side : " + side);
			
			ulong amount = 0;
			for (int i = 0; i < orderList.Count(); ++i) 
			{
				if (orderList [i].UserId != user_id) {
					amount += orderList[i].Qty;
					if (amount >= target_amount)
						return orderList[i].Price;
				}
			}
			return ulong.MaxValue;
		}


        private void AppendOrder(Order order)
		{
			if (order.Side == OrderBook.OrdSide.BUY)
				_buyside.Add(order);
			else if (order.Side == OrderBook.OrdSide.SELL)
				_sellside.Add(order);
			else
				throw new System.ArgumentException("Invalid OrderBook Side : " + order.Side);
		}


        public void AddOrder(JObject entry)
        {
            int index = entry.GetValue("MDEntryPositionNo").Value<int>() - 1;
            Order order = new Order();
            order.Price = entry.GetValue("MDEntryPx").Value<ulong>();
            order.Qty = entry.GetValue("MDEntrySize").Value<ulong>();
            order.UserId = entry.GetValue("UserID").Value<ulong>();
            order.Broker = entry.GetValue("Broker").Value<string>();
            order.OrderId = entry.GetValue("OrderID").Value<ulong>();
            order.Side = entry.GetValue("MDEntryType").Value<char>();
            order.OrderDate = entry.GetValue("MDEntryDate").Value<string>();
            order.OrderTime = entry.GetValue("MDEntryTime").Value<string>();

            if (order.Side == OrderBook.OrdSide.BUY)
                _buyside.Insert(index, order);
            else if (order.Side == OrderBook.OrdSide.SELL)
                _sellside.Insert(index, order);
            else
                throw new System.ArgumentException("Invalid OrderBook Side : " + order.Side);
        }

        public void UpdateOrder(JObject entry)
        {
            int index = entry.GetValue("MDEntryPositionNo").Value<int>() - 1;
            Order order = new Order();
            order.Price = entry.GetValue("MDEntryPx").Value<ulong>();
            order.Qty = entry.GetValue("MDEntrySize").Value<ulong>();
            order.UserId = entry.GetValue("UserID").Value<ulong>();
            order.Broker = entry.GetValue("Broker").Value<string>();
            order.OrderId = entry.GetValue("OrderID").Value<ulong>();
            order.Side = entry.GetValue("MDEntryType").Value<char>();
            order.OrderDate = entry.GetValue("MDEntryDate").Value<string>();
            order.OrderTime = entry.GetValue("MDEntryTime").Value<string>();

            if (order.Side == OrderBook.OrdSide.BUY)
                _buyside[index] = order;
            else if (order.Side == OrderBook.OrdSide.SELL)
                _sellside[index] = order;
            else
                throw new System.ArgumentException("Invalid OrderBook Side : " + order.Side);
        }

        public void DeleteOrder(JObject entry)
        {
            int index = entry.GetValue("MDEntryPositionNo").Value<int>() - 1;
            char side = entry.GetValue("MDEntryType").Value<char>();

            if (side == OrderBook.OrdSide.BUY)
                _buyside.RemoveAt(index);
            else if (side == OrderBook.OrdSide.SELL)
                _sellside.RemoveAt(index);
            else
                throw new System.ArgumentException("Invalid OrderBook Side : " + side);
        }

        public void DeleteOrdersThru(JObject entry)
        {
            int count = entry.GetValue("MDEntryPositionNo").Value<int>();
            char side = entry.GetValue("MDEntryType").Value<char>();

            if (side == OrderBook.OrdSide.BUY)
                _buyside.RemoveRange(0, count);
            else if (side == OrderBook.OrdSide.SELL)
                _sellside.RemoveRange(0, count);
            else
                throw new System.ArgumentException("Invalid OrderBook Side : " + side);
        }

        public override string ToString()
        {
            int max_count = (_buyside.Count() > _sellside.Count() ? _buyside.Count() : _sellside.Count());
            string result = "*** SYMBOL --> " + this._symbol + " ***\nBUYER;QUANTITY;PRICE;PRICE;QUANTITY;SELLER\n";
            for (int i = 0; i < max_count; i++)
            {
                string left = string.Empty;
                if (i < _buyside.Count())
                {
                    Order order = _buyside[i];
                    left = order.UserId.ToString() + ';' + order.Qty + ';' + order.Price + ';';
                }
                else
                {
                    left += ";;;";
                }

                string right = string.Empty;
                if (i < _sellside.Count())
                {
                    Order order = _sellside[i];
                    right = order.Price.ToString() + ';' + order.Qty.ToString() + ';' + order.UserId.ToString();
                }
                else
                {
                    right += ";;";
                }

                string line = left + right + '\n';
                result += line;
            }

            return result;
        }
    }
}
