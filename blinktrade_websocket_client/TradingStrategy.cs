using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

namespace Blinktrade
{
    public class TradingStrategy
    {
        private string _strategySellOrderClorid = null;
        private string _strategyBuyOrderClorid = null;
        private char _strategySide = default(char); // default: run both SELL AND BUY 
        private const ulong _minTradeSize = (ulong)(0.0001 * 1e8); // 10,000 Satoshi
        private ulong _maxTradeSize = 0;
		private ulong _buyTargetPrice = 0;
        private ulong _sellTargetPrice = 0;

		private double _startTime;
        private long _cloridSeqNum = 0;
        private ITradeClientService _tradeclient;

		// ** temporary workaround to support pegged order strategy without plugins **
		public enum PriceType { FIXED, PEGGED }
		private PriceType _priceType;
		private ulong _pegOffsetValue = 0;
        
		public event LogStatusDelegate LogStatusEvent;

        public ITradeClientService tradeclient { set { _tradeclient = value; } }

		public TradingStrategy(ulong max_trade_size, ulong buy_target_price, ulong sell_target_price, char side, PriceType priceType)
        {
            _maxTradeSize = max_trade_size;
            _buyTargetPrice = buy_target_price;
            _sellTargetPrice = sell_target_price;
            _strategySide = side;
			_priceType = priceType;

			if (priceType == PriceType.PEGGED && side == OrderSide.SELL) 
			{
				_pegOffsetValue = sell_target_price;
			}
					
            _startTime = Util.ConvertToUnixTimestamp(DateTime.Now);
        }

        private string MakeClOrdId()
        {
            return "BLKTRD-" + _startTime.ToString() + "-" + Interlocked.Increment(ref this._cloridSeqNum).ToString();
        }

        private void LogStatus(LogStatusType type, string message)
        {
            if (LogStatusEvent != null)
                LogStatusEvent(type, message);
        }

        public void runStrategy(IWebSocketClientConnection webSocketConnection, string symbol)
        {
            // Run the strategy to try to have an order on at least one side of the book according to fixed price range 
			// but never executing as a taker
            
			// ** temporary workaround to support market pegged sell order strategy without plugins **
			if (_priceType == PriceType.PEGGED && _strategySide == OrderSide.SELL) 
			{
				// make the price float according to the MID Price Peg Or Last Price Peg (the highest)
				SecurityStatus status = _tradeclient.GetSecurityStatus ("BLINK", symbol);
				if (status != null) {
					ulong midprice = (ulong)((status.BestAsk + status.BestBid + status.LastPx) / 3);
					Debug.Assert (_pegOffsetValue > 0);
					//_sellTargetPrice = (midprice > status.LastPx ? midprice : status.LastPx) + _pegOffsetValue;
					_sellTargetPrice = midprice + _pegOffsetValue;
					// TODO: MUST Provide a FLOOR for selling in the final strategy
				} 
				else 
				{
					LogStatus(
						LogStatusType.WARN,
						String.Format(
							"Expecting Security Status BLINK:{0} to run Pegged strategy",
							symbol)
					);
					return;
				}
			}

			// run the strategy

			if (_maxTradeSize > 0)
            {
                webSocketConnection.EnableTestRequest = false;
                if (_strategySide == OrderSide.BUY || _strategySide == default(char)) // buy or both
                {
                    runBuyStrategy(webSocketConnection, symbol);
                }

                if (_strategySide == OrderSide.SELL || _strategySide == default(char)) // sell or both
                {
                    runSellStrategy(webSocketConnection, symbol);
                }
                webSocketConnection.EnableTestRequest = true;
            }
        }

        private void runBuyStrategy(IWebSocketClientConnection webSocketConnection, string symbol)
        {
            OrderBook.IOrder bestBid = _tradeclient.GetOrderBook(symbol).BestBid;
            if (bestBid != null)
            {
                if (bestBid.UserId != _tradeclient.UserId)
                {
                    // buy @ 1 cent above the best price
                    ulong buyPrice = bestBid.Price + (ulong)(0.01 * 1e8);
                    if (buyPrice <= this._buyTargetPrice)
                    {
                        replaceOrder(webSocketConnection, symbol, OrderSide.BUY, buyPrice);
                    }
                    else
                    {
                        // cannot fight for the first position thus try to find a visible position in the book
                        OrderBook orderBook = _tradeclient.GetOrderBook(symbol);
                        List<OrderBook.Order> buyside = orderBook.GetBidOrders();
                        int i = buyside.BinarySearch(
                            new OrderBook.Order(OrderBook.OrdSide.BUY, _buyTargetPrice - (ulong)(0.01 * 1e8)),
                            new OrderBook.ReverseOrderPriceComparer()
                        );
                        int position = (i < 0 ? ~i : i);
						Debug.Assert (position > 0);
                        
						// verificar se a profundidade vale a pena
						if (position > 5+1 && orderBook.DoesAmountExceedsLimit (
							    					OrderBook.OrdSide.BUY,
							    					position - 1, (ulong)(10 * 1e8))) 
						{
							_tradeclient.CancelOrderByClOrdID(webSocketConnection, _strategyBuyOrderClorid);
							return;
						}

						var pivotOrder = buyside[position];
                        if (pivotOrder.UserId == _tradeclient.UserId)
                        {
                            // ordem ja e minha : pega + recursos disponiveis e cola no preco no vizinho se já nao estiver
							ulong price_delta = buyside.Count > position + 2 ? pivotOrder.Price - buyside[position+1].Price : 0;
                            ulong newBuyPrice = (price_delta > (ulong)(0.01 * 1e8) ?
                                            pivotOrder.Price - price_delta + (ulong)(0.01 * 1e8) :
                                            pivotOrder.Price);
                            ulong availableQty = calculateOrderQty(symbol, OrderSide.BUY, newBuyPrice);
                            if (newBuyPrice < pivotOrder.Price || availableQty > pivotOrder.Qty)
                            {
                                replaceOrder(webSocketConnection, symbol, OrderSide.BUY, newBuyPrice, availableQty);
                            }
                        }
                        else
                        {
							// estabelece preco de venda 1 centavo maior do que nesta posicao
							ulong newbuyPrice = pivotOrder.Price + (ulong)(0.01 * 1e8);
							replaceOrder(webSocketConnection, symbol, OrderSide.BUY, newbuyPrice);
                        }
                    }
                }
                else 
                {
                    // check and replace order to get closer to the order in the second position
                    List<OrderBook.Order> buyside = _tradeclient.GetOrderBook(symbol).GetBidOrders();
                    ulong price_delta = buyside.Count > 1 ? buyside[0].Price - buyside[1].Price : 0;
                    ulong newBuyPrice = ( price_delta > (ulong)(0.01 * 1e8) ? 
											bestBid.Price - price_delta + (ulong)(0.01 * 1e8) : 
											bestBid.Price );
                    ulong availableQty = calculateOrderQty(symbol, OrderSide.BUY, newBuyPrice);
                    if (newBuyPrice < bestBid.Price || availableQty > bestBid.Qty)
                    {
                        replaceOrder(webSocketConnection, symbol, OrderSide.BUY, newBuyPrice, availableQty);

                    }
                }
            }
            else
            {
                // TODO: empty book scenario
            }
        }

        private void runSellStrategy(IWebSocketClientConnection webSocketConnection, string symbol)
        {
            OrderBook.IOrder bestOffer = _tradeclient.GetOrderBook(symbol).BestOffer;
            if (bestOffer != null)
            {
                if (bestOffer.UserId != _tradeclient.UserId)
                {
                    // sell @ 1 cent bellow the best price
                    ulong sellPrice = bestOffer.Price - (ulong)(0.01 * 1e8);
                    if (sellPrice >= _sellTargetPrice)
                    {
                        replaceOrder(webSocketConnection, symbol, OrderSide.SELL, sellPrice);
                    }
                    else
                    { 
						// cannot fight for the first position thus try to find a visible position in the book
                        OrderBook orderBook = _tradeclient.GetOrderBook(symbol);
                        List<OrderBook.Order> sellside = orderBook.GetOfferOrders();
                        int i = sellside.BinarySearch( 
                            new OrderBook.Order( OrderBook.OrdSide.SELL, _sellTargetPrice + (ulong)(0.01 * 1e8)), 
                            new OrderBook.OrderPriceComparer()
                        );
                        int position = (i < 0 ? ~i : i);
						Debug.Assert (position > 0);
                        
						// verificar se a profundidade vale a pena
						if (position > 5+1 && orderBook.DoesAmountExceedsLimit (
							OrderBook.OrdSide.SELL,
							position - 1, (ulong)(10 * 1e8))) 
						{
							_tradeclient.CancelOrderByClOrdID(webSocketConnection, _strategySellOrderClorid);
							return;
						}

						var pivotOrder = sellside[position];
                        if (pivotOrder.UserId == _tradeclient.UserId)
                        {
							// ordem ja e minha : pega + recursos disponiveis e cola no preco no vizinho se já nao estiver
                            ulong price_delta = sellside[position+1].Price - pivotOrder.Price;
                            ulong newSellPrice = (price_delta > (ulong)(0.01 * 1e8) ?
                                            pivotOrder.Price + price_delta - (ulong)(0.01 * 1e8) :
                                            pivotOrder.Price);
                            ulong availableQty = calculateOrderQty(symbol, OrderSide.SELL);
                            if (newSellPrice > pivotOrder.Price || availableQty > pivotOrder.Qty)
                            {
                                replaceOrder(webSocketConnection, symbol, OrderSide.SELL, newSellPrice, availableQty);
                            }
                        }
                        else 
                        {
							// estabelece preco de venda 1 centavo menor do que nesta posicao
							ulong newSellPrice = pivotOrder.Price - (ulong)(0.01 * 1e8);
							replaceOrder(webSocketConnection, symbol, OrderSide.SELL, newSellPrice);
                        }
                    }
                }
                else 
                {
                    // check and replace the order to get closer to the order in the second position and gather more available funds
                    List<OrderBook.Order> sellside = _tradeclient.GetOrderBook(symbol).GetOfferOrders();
                    ulong price_delta = sellside.Count > 1 ? sellside[1].Price - sellside[0].Price : 0;
                    ulong newSellPrice = ( price_delta > (ulong)(0.01 * 1e8) ? 
											bestOffer.Price + price_delta - (ulong)(0.01 * 1e8) : 
											bestOffer.Price );
                    ulong availableQty = calculateOrderQty(symbol, OrderSide.SELL);
                    if (newSellPrice > bestOffer.Price || availableQty > bestOffer.Qty)
                    {
                        replaceOrder(webSocketConnection, symbol, OrderSide.SELL, newSellPrice, availableQty);
                    }
                }
            }
            else
            {
                // TODO: empty book scenario
            }
        }

        private void replaceOrder(IWebSocketClientConnection webSocketConnection, string symbol, char side, ulong price, ulong qty = 0)
        {
            Debug.Assert(side == OrderSide.BUY || side == OrderSide.SELL);
            
            string existingClorId = side == OrderSide.BUY ? this._strategyBuyOrderClorid : this._strategySellOrderClorid;

            var orderToReplace = _tradeclient.miniOMS.GetOrderByClOrdID( existingClorId );
            // cancel the previous sent order since it is not possible to modify the order
            if (orderToReplace != null)
            {
                switch (orderToReplace.OrdStatus)
                {
                    case OrdStatus.PENDING_NEW: // client control - in the case no response was received
                    case OrdStatus.PENDING_CANCEL:
                        LogStatus(
                            LogStatusType.WARN,
                                String.Format(
                                    "WAITING ORDER STATE CHANGE : {0} CLORDID {1} SIDE {2}",
                                    orderToReplace.OrdStatus.ToString(),
                                    orderToReplace.ClOrdID,
                                    side)
                            );
                        return; // wait the confirmation
                    case OrdStatus.NEW:
                    case OrdStatus.PARTIALLY_FILLED:
                        // cancel the order to replace it
                        _tradeclient.CancelOrderByClOrdID(webSocketConnection, orderToReplace.ClOrdID);
                        break;
                    default:
                        break;
                }
            }

            if (qty == 0)
            {
                qty = calculateOrderQty(symbol, side, price);
            }

            // send a new buy order
            sendOrder(webSocketConnection, symbol, side, qty, price);
        }
        
        private void sendOrder(IWebSocketClientConnection webSocketConnection, string symbol, char side, ulong qty, ulong price)
        {
            Debug.Assert(side == OrderSide.BUY || side == OrderSide.SELL);

			if (side != OrderSide.BUY && side != OrderSide.SELL)
				throw new ArgumentException();

			if (qty >= _minTradeSize)
            {
                // send order (Participate don't initiate - aka book or cancel) and keep track of the ClOrdId
                string clorid = _tradeclient.SendOrder(
                                        webSocketConnection,
                                        symbol,
                                        qty,
                                        price,
                                        side,
                                        _tradeclient.BrokerId,
                                        MakeClOrdId(),
                                        OrdType.LIMIT,
                                        ExecInst.PARTICIPATE_DONT_INITIATE
				);
		
				if (side == OrderSide.BUY)
					_strategyBuyOrderClorid = clorid;
				else
					_strategySellOrderClorid = clorid;
            }
        }

        private ulong calculateOrderQty(string symbol, char side, ulong price = 0)
        {
            // check the updated balance to gather remaining qty
            // don't need to bother with locked balance because we keep only 1 alive order at each side of the book
            ulong result = 0;
            switch (side)
            {
                case OrderSide.BUY:
                    Debug.Assert(price > 0);
                    if (price > 0)
                    {
                        string fiatCurrency = symbol.Substring(3);
                        ulong fiatBalance = _tradeclient.GetBalance(fiatCurrency);
                        ulong max_allowed_qty = (ulong)((double)fiatBalance / price * 1e8);
                        result = _maxTradeSize < max_allowed_qty ? _maxTradeSize : max_allowed_qty;
                    }
                    break;
                case OrderSide.SELL:
                    string cryptoCurrency = symbol.Substring(0, 3);
                    Debug.Assert(cryptoCurrency == "BTC");
                    ulong crytoBalance = _tradeclient.GetBalance(cryptoCurrency);
                    result = _maxTradeSize < crytoBalance ? _maxTradeSize : crytoBalance;
                    break;
                default:
                    break;
            }
            return result;
        }
    }
}