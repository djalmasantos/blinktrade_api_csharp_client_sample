This is an example of how to use the <a href="https://blinktrade.com/docs/#getting-started">Blinktrade Websocket API</a> in C#.

The application demonstrates the following use cases:

- Websocket connection
- Protocol engine basics (logon, test request, heartbeat and JSON/FIX messaging)
- Order Book Management
- Security List Request and Reply processing
- Order List Request and Reply processing
- Balance Request and Reply processing
- MiniOMS (in-memory updated information of the user's alive orders)
- Demo TradingStrategy (send/cancel/replace orders with the price incremented or decremented by 0.01 to fight for the best position on the book, but never initiating the execution and respecting the target price parameters to buy or sell)


<b>Setup:</b><br>
Windows 8 and above version or <a href="http://www.mono-project.com/">Mono</a><br>
Visual Studio with .NET 4.5 or MonoDevelop<br>

<b>Dependencies:</b><br>
Newtonsoft Json.NET<br>
<a href="https://github.com/sta/websocket-sharp">Websocket-Sharp</a> (required only when running with Mono because System.Net.WebSockets is buggy in Mono)<br>
Microsoft TPL Dataflow<br>

<b>Build and Run:</b><br>
After building the solution in Visual Studio or MonoDevelop, run blinktrade_websocket_client.exe without providing command line arguments to display help information. When building for Mono, make sure \_\_MonoCS\_\_ is defined, you must define it yourself if the compiler version you are using no longer do it, otherwise the program might build but it might not run properly with Mono (see Dependencies).<br>

<b>Is there a test environment to try out this sample app?</b><br>
Yes, <a href="https://testnet.blinktrade.com/">blinktrade tesnet exchange</a>.

<b>List of exchanges running the blinktrade platform</b><br>
- [bitcambio](https://bitcambio.com.br/)
- [chilebit](https://chilebit.net)
- [VBTC](https://vbtc.vn)
- [surbitcoin](https://surbitcoin.com) 
- [urdubit](https://urdubit.com)
