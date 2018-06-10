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
        private const ulong _minOrderSize = (ulong)(0.0001 * 1e8); // 10,000 Satoshi
		private const ulong _maxAmountToSell = (ulong)(10 * 1e8); // TODO: make it an optional parameter
		private ulong _maxOrderSize = 0;

		private ulong _buyTargetPrice = 0;
        private ulong _sellTargetPrice = 0;

		private double _startTime;
        private long _cloridSeqNum = 0;
        private ITradeClientService _tradeclient;

		private volatile bool _enabled = true;

		// ** temporary workaround to support pegged order strategy without plugins **
		public enum PriceType { FIXED, PEGGED, STOP, MARKET_AS_MAKER, EXPLORE_BOOK_DEPTH }
		private PriceType _priceType;
		private ulong _pegOffsetValue = 0;

		// stop exclusive attributes
		private ulong _stop_price = 0;

		// another workaround for the float sell strategy
		private ulong _sell_floor = 0;

		private ulong _minBookDepth = 0;
		private ulong _maxBookDepth = 0;

        
		public event LogStatusDelegate LogStatusEvent;

        public ITradeClientService tradeclient 
		{
			set { _tradeclient = value; } 
			get { return _tradeclient; }
		}

		public bool Enabled 
		{
			get { return _enabled;}
			set { _enabled = value; }
		}

		public ulong MinOrderSize 
		{
			get { return _minOrderSize; }
		}

		public TradingStrategy(ulong max_trade_size, ulong buy_target_price, ulong sell_target_price, char side, PriceType priceType)
        {
            _maxOrderSize = max_trade_size;
            _buyTargetPrice = buy_target_price;
            _sellTargetPrice = sell_target_price;
            _strategySide = side;
			_priceType = priceType;

			if (priceType == PriceType.PEGGED && side == OrderSide.SELL) 
			{
				_pegOffsetValue = sell_target_price;
			}

			// ugly workaround to explore book depth strategy without a hard refactory
			if (priceType == PriceType.EXPLORE_BOOK_DEPTH) 
			{
				_minBookDepth = _buyTargetPrice;
				_maxBookDepth = _sellTargetPrice;
			}
					
            _startTime = Util.ConvertToUnixTimestamp(DateTime.Now);
        }

		public TradingStrategy(char side, ulong order_size, ulong stoppx, ulong limit_price = 0)
		{
			_priceType = PriceType.STOP;
			_maxOrderSize = order_size;
			_stop_price = stoppx;
			_buyTargetPrice  = (side == OrderSide.BUY  ? limit_price : 0);
			_sellTargetPrice = (side == OrderSide.SELL ? limit_price : 0);
			_strategySide = side;
			_startTime = Util.ConvertToUnixTimestamp(DateTime.Now);
		}

		public void Reset()
		{
			_strategySellOrderClorid = null;
			_strategyBuyOrderClorid = null;
			_cloridSeqNum = 0;
			_startTime = Util.ConvertToUnixTimestamp(DateTime.Now);
		}

		/*
		private Object connLock = new Object();  
		private IWebSocketClientConnection _connection = null; // might change to a list of connections in the future

		public IWebSocketClientConnection activeConnection
		{
			get 
			{ 
				lock (connLock) 
				{
					return _connection;
				} 
			}
		}

		public void OnStart(IWebSocketClientConnection connection)
		{
			lock (connLock) 
			{ 
				_connection = connection; 
			}

			// a partir daqui se torna "seguro" acessar os dados do tradeclient e ate mesmo enviar ordens de compra e venda usando o connection
			Console.WriteLine("Func = {0} : ThreadID={1}" , "OnStart", System.Threading.Thread.CurrentThread.ManagedThreadId);
			var saldoBTC = tradeclient.GetBalance("BTC");
			Console.WriteLine("BTC = {0}", saldoBTC);

			var saldoFiat = tradeclient.GetBalance(tradeclient.GetTradingSymbol().Substring(3));
			Console.WriteLine("FIAT = {0}", saldoFiat);

			var bookBestBid = tradeclient.GetOrderBook(tradeclient.GetTradingSymbol()).BestBid.Price;
			Console.WriteLine("BID = {0}", bookBestBid);

			var bookBestOffer = tradeclient.GetOrderBook(tradeclient.GetTradingSymbol()).BestOffer.Price;
			Console.WriteLine("OFFER = {0}", bookBestOffer);

			var vwap = tradeclient.CalculateVWAP();
			Console.WriteLine("VWAP = {0}", vwap);
			Console.WriteLine("IsConnected : {0}", connection.IsConnected);
			Console.WriteLine("-------------------------------------------------------------------");
		}

		public void OnClose(IWebSocketClientConnection connection)
		{
			lock (connLock) 
			{
				_connection = null;
			}

		}
		*/

		public void OnDepositRefresh(string deposit_id, string currency, ulong amount, int status, string state)
		{
			Console.WriteLine ("Received Depoist {0} {1} [{2}][{3}]", amount, currency, status, state);
			if (this._sell_floor == 0 && currency == "BTC")
			{
				// *** workaround ***
				SecurityStatus btcusd_quote = _tradeclient.GetSecurityStatus ("BITSTAMP", "BTCUSD");
				if (btcusd_quote == null || btcusd_quote.LastPx == 0) {
					LogStatus (LogStatusType.WARN, "BITSTAMP:BTCUSD not available");
					return;
				}
				this._sell_floor = (ulong)(btcusd_quote.LastPx * 4);
				/*
				Console.WriteLine ("DEBUG Calculated[0] Sell Floor {0}", this._sell_floor );
				if (amount > 0)
				{
					this._sell_floor = (ulong)(5000 / amount * 1e8);
					Console.WriteLine ("DEBUG Calculated[1] Sell Floor {0}", this._sell_floor );
				}
				this._sell_floor = 0;
				*/
				Console.WriteLine ("DEBUG Calculated[2] Sell Floor {0}", this._sell_floor );
			}
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

		public void OnExecutionReport(IWebSocketClientConnection webSocketConnection, MiniOMS.IOrder order)
		{
			if (_priceType == PriceType.PEGGED && _strategySide == OrderSide.SELL)
			{
				if (order.OrdStatus == OrdStatus.FILLED || order.OrdStatus == OrdStatus.PARTIALLY_FILLED) 
				{
					ulong theSoldAmount = _tradeclient.GetSoldAmount();
					if (theSoldAmount >= _maxAmountToSell) 
					{
						LogStatus (LogStatusType.WARN, String.Format ("[OnExecutionReport] Cannot exceed the allowed max amount to sell : {0} {1}", theSoldAmount, _maxAmountToSell));
						_tradeclient.CancelOrderByClOrdID (webSocketConnection, _strategySellOrderClorid);
					}
				}
			}

		}

		/*abstract*/ public void runStrategy(IWebSocketClientConnection webSocketConnection, string symbol)
        {
            // Run the strategy to try to have an order on at least one side of the book according to fixed price range 
			// but never executing as a taker
            
			if (!_enabled) { // strategy cannot run when disabled
				LogStatus(LogStatusType.WARN,"Strategy is disabled and will not run");
				return;
			}

			// ** temporary workaround to support market pegged sell order strategy without plugins**
			if (_priceType == PriceType.PEGGED && _strategySide == OrderSide.SELL) 
			{
				// make the price float according to the MID Price 
				/*
				// requires the Security List for the trading symbol
				SecurityStatus status = _tradeclient.GetSecurityStatus ("BLINK", symbol);
				if (status == null)
				{
					LogStatus(
						LogStatusType.WARN,
						String.Format(
							"Waiting Security Status BLINK:{0} to run Pegged strategy",
							symbol)
					);
					return;
				}
				*/

				// check the remaining qty that can still be sold
				ulong theSoldAmount = _tradeclient.GetSoldAmount();
				if (theSoldAmount < _maxAmountToSell) 
				{
					ulong uAllowedAmountToSell = _maxAmountToSell - theSoldAmount;
					_maxOrderSize = _maxOrderSize < uAllowedAmountToSell ? _maxOrderSize : uAllowedAmountToSell;
					_maxOrderSize = _maxOrderSize > _minOrderSize ? _maxOrderSize : _minOrderSize;
				} 
				else 
				{
					LogStatus(LogStatusType.WARN, String.Format ("[runStrategy] Cannot exceed the allowed max amount to sell : {0} {1}", theSoldAmount, _maxAmountToSell));
					_tradeclient.CancelOrderByClOrdID(webSocketConnection, _strategySellOrderClorid);
					return;
				}

				// gather the data to calculate the midprice
				OrderBook orderBook = _tradeclient.GetOrderBook(symbol);

				// instead of bestAsk let's use the Price reached if one decides to buy X BTC
				ulong maxPriceToBuyXBTC = orderBook.MaxPriceForAmountWithoutSelfOrders(
														OrderBook.OrdSide.SELL,
														(ulong)(5 * 1e8), // TODO: make it a parameter
														_tradeclient.UserId);
				
			
				// let's use the VWAP as the market price (short period tick based i.e last 30 min.)
				ulong marketPrice = _tradeclient.CalculateVWAP();

				// calculate the mid price					
				ulong midprice = (ulong)((orderBook.BestBid.Price + maxPriceToBuyXBTC + marketPrice) / 3);
				_sellTargetPrice = midprice + _pegOffsetValue;

				// get the dollar price
				SecurityStatus usd_official_quote = _tradeclient.GetSecurityStatus ("UOL", "USDBRT"); // use USDBRT for the turism quote
				if (usd_official_quote == null || usd_official_quote.BestAsk == 0) {
					LogStatus (LogStatusType.WARN, "UOL:USDBRT not available");
				}
				// get the BTC Price in dollar
				SecurityStatus btcusd_quote = _tradeclient.GetSecurityStatus ("BITSTAMP", "BTCUSD");
				if (btcusd_quote == null || btcusd_quote.LastPx == 0) {
					LogStatus (LogStatusType.WARN, "BITSTAMP:BTCUSD not available");
				}
				// calculate the selling floor must be at least the price of the BTC in USD
				ulong floor = 0;
				if ( this._sell_floor > 0 ) {
					floor = this._sell_floor;
				}
				else
				{
					//floor = (ulong)(1.025 * btcusd_quote.LastPx * (float)(usd_official_quote.BestAsk / 1e8));
					// floor = (ulong)(30000*1e8);
					//floor = 0;
				}

				// check the selling FLOOR
				if ( _sellTargetPrice < floor ) {
					_sellTargetPrice = floor;
				}
			}

			// another workaround for sending a single stop order and disable the strategy
			if (_priceType == PriceType.STOP) {
				if (_strategySide == OrderSide.BUY) {
					char ordType = (_buyTargetPrice == 0 ? OrdType.STOP_MARKET : OrdType.STOP_LIMIT);
					ulong ref_price = (_buyTargetPrice > _stop_price ? _buyTargetPrice : _stop_price);
					ulong qty = calculateOrderQty (symbol, _strategySide, ref_price);
					sendOrder(webSocketConnection, symbol, OrderSide.BUY, qty, _buyTargetPrice, ordType, _stop_price, default(char));
				}

				if (_strategySide == OrderSide.SELL) {
					char ordType = (_sellTargetPrice == 0 ? OrdType.STOP_MARKET : OrdType.STOP_LIMIT);
					ulong qty = calculateOrderQty (symbol, _strategySide);
					sendOrder(webSocketConnection, symbol, OrderSide.SELL, qty, _sellTargetPrice, ordType, _stop_price, default(char));
				}
				// disable strategy after sending the stop order...
				this._enabled = false;
				return;
			}

			// run the strategy
			if (_maxOrderSize > 0)
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
			OrderBook.IOrder bestOffer = _tradeclient.GetOrderBook(symbol).BestOffer;
			if (_priceType == PriceType.MARKET_AS_MAKER) {
				ulong buyPrice = 0;
				if (bestOffer != null) {
					buyPrice = bestOffer.Price - (ulong)(0.01 * 1e8);
				} else if (bestBid != null) {
					buyPrice = bestBid.Price;
				}

				if (buyPrice > 0) {
					replaceOrder(webSocketConnection, symbol, OrderSide.BUY, buyPrice);
				}
				return;
			}

			if (_priceType == PriceType.EXPLORE_BOOK_DEPTH) 
			{
				// set the price based on the depth (this is a lot inefficent but I don't care)
				OrderBook orderBook = _tradeclient.GetOrderBook(symbol);
				ulong max_price = orderBook.MaxPriceForAmountWithoutSelfOrders(OrderBook.OrdSide.BUY, _minBookDepth, _tradeclient.UserId);
				ulong min_price = orderBook.MaxPriceForAmountWithoutSelfOrders(OrderBook.OrdSide.BUY, _maxBookDepth, _tradeclient.UserId);
				max_price = max_price < ulong.MaxValue ? max_price : min_price;

				if (max_price < ulong.MaxValue) {
					max_price += (ulong)(0.01 * 1e8); // increment in 1 cent because min and max might be the same in a tight book range
					var myOrder = _tradeclient.miniOMS.GetOrderByClOrdID (this._strategyBuyOrderClorid);
					if ((myOrder == null) || (myOrder.Price > max_price || myOrder.Price < min_price)) {
						LogStatus (LogStatusType.WARN, String.Format ("[DT] must change order price not in acceptable position {0} {1} {2}", myOrder != null ? myOrder.Price : 0, max_price, min_price));
						_buyTargetPrice = min_price + (ulong)(0.01 * 1e8);
					} else {
						return; // don't change the order because it is still in an acceptable position
					}
				} else {
					ulong market_price = _tradeclient.CalculateVWAP();
					ulong lastPrice = _tradeclient.GetLastPrice();
					ulong off_sale_price = ulong.MaxValue;
					SecurityStatus usd_official_quote = _tradeclient.GetSecurityStatus ("UOL", "USDBRL"); // use USDBRT for the turism quote
					SecurityStatus btcusd_quote = _tradeclient.GetSecurityStatus ("BITSTAMP", "BTCUSD");
					if (usd_official_quote != null && usd_official_quote.BestBid > 0 && btcusd_quote != null && btcusd_quote.LastPx > 0) {
						off_sale_price = (ulong)(usd_official_quote.BestBid / 1e8 * btcusd_quote.BestBid / 1e8 * 0.5 * 1e8);	
					}
					_buyTargetPrice = Math.Min(Math.Min(market_price, lastPrice), off_sale_price);
				}
			}

            if (bestBid != null)
            {
                if (bestBid.UserId != _tradeclient.UserId)
                {
					// buy @ 1 cent above the best price (TODO: parameter for price increment)
                    ulong buyPrice = bestBid.Price + (ulong)(0.01 * 1e8);
                    if (buyPrice <= this._buyTargetPrice) 
                    {
						//OrderBook.IOrder bestOffer = _tradeclient.GetOrderBook(symbol).BestOffer;
						if (buyPrice < bestOffer.Price) 
						{
							replaceOrder (webSocketConnection, symbol, OrderSide.BUY, buyPrice);
						}
						else 
						{
							// avoid being a taker or receiving a reject when using ExecInst=6 but stay in the book with max price
							ulong max_buy_price = bestOffer.Price - (ulong)(0.01 * 1e8);
							var own_order = _tradeclient.miniOMS.GetOrderByClOrdID( _strategyBuyOrderClorid );
							ulong availableQty = calculateOrderQty(symbol, OrderSide.BUY, max_buy_price);
							if (own_order == null || own_order.Price != max_buy_price || availableQty > own_order.OrderQty)
								replaceOrder(webSocketConnection, symbol, OrderSide.BUY, max_buy_price);
						}
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
                        
						if (this._priceType != PriceType.EXPLORE_BOOK_DEPTH) {
							// verificar se a profundidade vale a pena: (TODO: parameters for max_pos_depth and max_amount_depth)
							if (position > 5 + 1 && orderBook.DoesAmountExceedsLimit (
								    OrderBook.OrdSide.BUY,
								    position - 1, (ulong)(20 * 1e8))) {
								_tradeclient.CancelOrderByClOrdID (webSocketConnection, _strategyBuyOrderClorid);
								return;
							}
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
			OrderBook.IOrder bestBid = _tradeclient.GetOrderBook(symbol).BestBid;
			OrderBook.IOrder bestOffer = _tradeclient.GetOrderBook(symbol).BestOffer;
			if (_priceType == PriceType.MARKET_AS_MAKER) {
				ulong sellPrice = 0;
				if (bestBid != null) {
					sellPrice = bestBid.Price + (ulong)(0.01 * 1e8);
				} else if (bestOffer != null) {
					sellPrice = bestOffer.Price;
				}
				if (sellPrice > 0) {
					replaceOrder(webSocketConnection, symbol, OrderSide.SELL, sellPrice);
				}
				return;
			}

			//OrderBook.IOrder bestOffer = _tradeclient.GetOrderBook(symbol).BestOffer;
            if (bestOffer != null)
            {
                if (bestOffer.UserId != _tradeclient.UserId)
                {
					// sell @ 1 cent bellow the best price (TODO: parameter for price increment)
                    ulong sellPrice = bestOffer.Price - (ulong)(0.01 * 1e8);
                    if (sellPrice >= _sellTargetPrice)
                    {
						//OrderBook.IOrder bestBid = _tradeclient.GetOrderBook(symbol).BestBid;
						if (sellPrice > bestBid.Price) {
							replaceOrder (webSocketConnection, symbol, OrderSide.SELL, sellPrice);
						}
						else 
						{
							// avoid being a taker or receiving a reject when using ExecInst=6 but stay in the book with max price
							ulong max_sell_price = bestBid.Price + (ulong)(0.01 * 1e8);
							var own_order = _tradeclient.miniOMS.GetOrderByClOrdID( _strategySellOrderClorid );
							ulong availableQty = calculateOrderQty(symbol, OrderSide.SELL);
							if (own_order == null || own_order.Price != max_sell_price || availableQty > own_order.OrderQty)
								replaceOrder(webSocketConnection, symbol, OrderSide.SELL, max_sell_price);
						}
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
                        
						// verificar se a profundidade vale a pena: (TODO: parameters for max_pos_depth and max_amount_depth)
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
            
			if (qty == 0)
			{
				qty = calculateOrderQty(symbol, side, price);
			}

			if (qty < _minOrderSize) {
				return; // order is too small to send
			}

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
                        return; // wait the confirmation of the NEW or CANCEL
					case OrdStatus.NEW:
					case OrdStatus.PARTIALLY_FILLED:
	                    // cancel the order to replace it
						
						if (orderToReplace.Price == price && orderToReplace.LeavesQty == qty)
							return; // order is essencially the same and should not be replaced
					
						_tradeclient.CancelOrderByClOrdID (webSocketConnection, orderToReplace.ClOrdID);
						break;

	                default:
	                break;
                }
            }
            
			// send a new order
            sendOrder(webSocketConnection, symbol, side, qty, price);
        }
        
		private void sendOrder(IWebSocketClientConnection webSocketConnection, string symbol, char side, ulong qty, ulong price, char orderType = OrdType.LIMIT, ulong stop_price = 0, char exec_inst = ExecInst.PARTICIPATE_DONT_INITIATE)
        {
            Debug.Assert(side == OrderSide.BUY || side == OrderSide.SELL);

			if (side != OrderSide.BUY && side != OrderSide.SELL)
				throw new ArgumentException();

			if (qty >= _minOrderSize)
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
										orderType,
										stop_price,
                                        exec_inst
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
                        result = _maxOrderSize < max_allowed_qty ? _maxOrderSize : max_allowed_qty;
                    }
                    break;
                case OrderSide.SELL:
                    string cryptoCurrency = symbol.Substring(0, 3);
                    Debug.Assert(cryptoCurrency == "BTC");
                    ulong crytoBalance = _tradeclient.GetBalance(cryptoCurrency);
                    result = _maxOrderSize < crytoBalance ? _maxOrderSize : crytoBalance;
                    break;
                default:
                    break;
            }
            return result;
        }
    }
}