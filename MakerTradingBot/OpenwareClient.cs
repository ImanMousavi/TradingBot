using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MakerTradingBot
{

    public class Balance
    {
        [JsonProperty("currency")]
        public string currency { get; set; }

        [JsonProperty("balance")]
        public string balance { get; set; }

        [JsonProperty("locked")]
        public string locked { get; set; }
    }

    public class Market
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("base_unit")]
        public string BaseUnit { get; set; }

        [JsonProperty("quote_unit")]
        public string QuoteUnit { get; set; }

        [JsonProperty("min_price")]
        public string MinPrice { get; set; }

        [JsonProperty("max_price")]
        public string MaxPrice { get; set; }

        [JsonProperty("min_amount")]
        public string MinAmount { get; set; }

        [JsonProperty("amount_precision")]
        public int AmountPrecision { get; set; }

        [JsonProperty("price_precision")]
        public int PricePrecision { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }
    }

    public class Order
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("side")]
        public string Side { get; set; }

        [JsonProperty("ord_type")]
        public string OrdType { get; set; }

        [JsonProperty("price")]
        public string Price { get; set; }

        [JsonProperty("avg_price")]
        public string AvgPrice { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("market")]
        public string Market { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("origin_volume")]
        public string OriginVolume { get; set; }

        [JsonProperty("remaining_volume")]
        public string RemainingVolume { get; set; }

        [JsonProperty("executed_volume")]
        public string ExecutedVolume { get; set; }

        [JsonProperty("trades_count")]
        public int TradesCount { get; set; }
    }

    public class MarketDepth
    {
        [JsonProperty("timestamp")]
        public int Timestamp { get; set; }

        [JsonProperty("asks")]
        public List<List<float>> Asks { get; set; }

        [JsonProperty("bids")]
        public List<List<float>> Bids { get; set; }
    }

    public class Ticker
    {
        [JsonProperty("low")]
        public string Low { get; set; }

        [JsonProperty("high")]
        public string High { get; set; }

        [JsonProperty("open")]
        public string Open { get; set; }

        [JsonProperty("last")]
        public string Last { get; set; }

        [JsonProperty("volume")]
        public string Volume { get; set; }

        [JsonProperty("amount")]
        public string Amount { get; set; }

        [JsonProperty("avg_price")]
        public string AvgPrice { get; set; }

        [JsonProperty("price_change_percent")]
        public string PriceChangePercent { get; set; }

        [JsonProperty("vol")]
        public string Vol { get; set; }
    }

    public enum OrderType
    {
        Sell,
        Buy
    }

    public class OpenwareClient
    {
        public string openwareApiUrl;
        public string openwareRangerUrl;
        public string apiKey;
        public string apiKeySecret;

        public string TradingPair = "wikieth"; // wikieth
        string userAgent = "openware/c#";

        List<Order> NewOrders = new List<Order>();
        List<Order> openOrders = new List<Order>();

        RNG rand = new RNG();

        public OpenwareClient(string openwareApiUrl, string openwareRangerUrl, string apiKey, string apiSecret)
        {
            this.openwareApiUrl = openwareApiUrl;
            this.openwareRangerUrl = openwareRangerUrl;
            this.apiKey = apiKey;
            this.apiKeySecret = apiSecret;
        }

        public async Task<string> GetServerTimestamp()
        {
            string url = openwareApiUrl + "/public/timestamp";

            string res = Get(url);

            return res;
        }

        public async Task<string> GetAccountBalances()
        {
            string url = openwareApiUrl + "/account/balances";

            string res = Get(url);

            return res;
        }

        public async Task<string> GetMarkets()
        {
            string url = openwareApiUrl + "/public/markets";

            string res = Get(url);

            return res;
        }

        public async Task<List<Order>> GetOrders()
        {
            string url = openwareApiUrl + "/market/orders";

            string orders = Get(url);

            var openOrders = JsonConvert.DeserializeObject<List<Order>>(orders);

            return openOrders;
        }

        public async Task<MarketDepth> GetSnapshot()
        {
            string url = openwareApiUrl + "/public/markets/" + TradingPair + "/depth";

            string marketDepthJSON = Get(url);

            MarketDepth marketDepth = JsonConvert.DeserializeObject<MarketDepth>(marketDepthJSON);

            return marketDepth;
        }

        public async Task<Ticker> GetTickers()
        {
            string url = openwareApiUrl + "/public/markets/tickers";

            string tickerJSON = Get(url);

            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(tickerJSON);

            string marketTricker = dict[TradingPair].ToString();

            var dict2 = JsonConvert.DeserializeObject<Dictionary<string, object>>(marketTricker);

            string tickerJSON2 = dict2["ticker"].ToString();

            Ticker ticker = JsonConvert.DeserializeObject<Ticker>(tickerJSON2);

            return ticker;
        }

        public Order ExecuteOrder(OrderType orderType, float orderVolume, float orderPrice)
        {
            string side = orderType == OrderType.Sell ? "sell" : "buy";
            string volume = string.Format("{0:0.00000}", orderVolume);
            string price = string.Format("{0:0.0000}", orderPrice);
            string ordType = "limit";

            string url = openwareApiUrl + "/market/orders";
            string data = "{\"market\": \"" + TradingPair + "\", \"volume\": \"" + volume + "\", \"price\": \"" + price + "\", \"side\": \"" + side + "\", \"ord_type\": \"" + ordType + "\"}";

            string resp = Post(url, data, "application/json", "POST");

            if (!string.IsNullOrEmpty(resp))
            {
                Order order = JsonConvert.DeserializeObject<Order>(resp);

                Console.WriteLine("Order Executed [" + orderType.ToString() + "] " + order.Market + " " + order.Id + " " + order.Uuid + " " + order.State + " price: " + string.Format("{0:0.0000}", order.Price) + " volume: " + string.Format("{0:0.00000}", order.OriginVolume));

                return order;
            }
            else
            {
                throw new Exception("Can not deserialise order");
            }
        }

        public void CancelOrder(int id)
        {
            string url = openwareApiUrl + "/market/orders/" + id.ToString() + "/cancel";

            string resp = Post(url, "", "application/json", "POST");

            if (!string.IsNullOrEmpty(resp))
            {
                Order order = JsonConvert.DeserializeObject<Order>(resp);

                if (NewOrders != null)
                {
                    NewOrders.Remove(NewOrders.First(o => o.Id == id));
                }


                Console.WriteLine("Order Canceled " + order.Market + " " + order.Id + " " + order.Uuid + " " + order.State + " price: " + order.Price + " volume: " + order.OriginVolume);
            }
            else
            {
                throw new Exception("Can not deserialise order");
            }
        }

        public void CancelAllOrders()
        {
            var task = Task.Run(async () => await GetOrders());

            var allOrders = task.Result;

            foreach (var order in allOrders)
            {
                CancelOrder(order.Id);
            }
        }

        public void CancelOrders()
        {
            for (int i = 0; i < NewOrders.Count; i++)
            {
                Order order = NewOrders[i];

                if (order.State == "pending" || order.State == "wait")
                {
                    //var task = Task.Run(async () => await CancelOrder(order.Id));

                    CancelOrder(order.Id);
                }
            }
        }

        public float stepSize = 0.01f;
        public float stepMode = 0.00f;
        public double ConvertToRadians(double angle)
        {
            return (Math.PI / 180) * angle;
        }

        public void ExecuteTakerBot()
        {
            Console.WriteLine("Taker Bot.");
            float minVolume = 0.0001f;
            float MAX_Volume = 1.0f;
            float MAX_SELL_Volume = 1.0f;
            int numTakerBots = 4;
            float amplitude = 100;

            if(stepMode >= 360)
            {
                stepMode = 0;

                stepSize = rand.Next(0.01f, 10.00f);
            }

            for (int k = 0; k < numTakerBots; k++)
            {
                var task = Task.Run(async () => await GetSnapshot());
                MarketDepth marketDepth = task.Result;

                var task2 = Task.Run(async () => await GetTickers());
                Ticker ticker = task2.Result;

                float lastPrice = float.Parse(ticker.Last);

                if (rand.Next(0, 5) >= 2)
                {
                    // BUY
                    if (marketDepth.Asks.Any())
                    {
                        // Get minimum buying price and max volume.
                        float minBuyPrice = float.MaxValue;
                        float maxVolume = minVolume;
                        bool found = false;

                        for (int i = 0; i < marketDepth.Asks.Count; i++)
                        {
                            var ask = marketDepth.Asks[i];

                            if (ask.First() < minBuyPrice && ask.First() >= lastPrice)
                            {
                                minBuyPrice = ask.First();
                                maxVolume = ask[1];
                                found = true;
                            }
                        }

                        float volume = rand.Next(minVolume, amplitude * (float)Math.Sin(ConvertToRadians(stepMode * (maxVolume % MAX_Volume))));
                        //float volume = rand.Next(minVolume, maxVolume % MAX_Volume);
                        float price = minBuyPrice;

                        if(price >= lastPrice && found)
                        {
                            try
                            {
                                Order or = ExecuteOrder(OrderType.Buy, volume, price);

                                stepMode += stepSize;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("ERROR couldn't execute BUY order. Price:" + price + " volume: " + volume);
                            }
                        }
                    }
                    else
                    {

                    }
                }
                else
                {
                    // SELL
                    if (marketDepth.Bids.Any())
                    {
                        // Get maximum selling price and max volume.
                        float maxSellPrice = float.MinValue;
                        float maxVolume = minVolume;
                        bool found = false;
                        for (int i = 0; i < marketDepth.Bids.Count; i++)
                        {
                            var bid = marketDepth.Bids[i];

                            if (bid.First() > maxSellPrice && bid.First() <= lastPrice)
                            {
                                maxSellPrice = bid.First();
                                maxVolume = bid[1];
                                found = true;
                            }
                        }

                        float volume = rand.Next(minVolume, amplitude * (float)Math.Sin(ConvertToRadians(stepMode * (maxVolume % MAX_SELL_Volume))));
                        //float volume = rand.Next(minVolume, maxVolume % MAX_SELL_Volume);
                        float price = maxSellPrice;

                        if (price <= lastPrice && found)
                        {
                            try
                            {
                                Order or = ExecuteOrder(OrderType.Sell, volume, price);

                                stepMode += stepSize;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("ERROR couldn't execute SELL order. Price:" + price + " volume: " + volume);
                            }
                        }
                    }
                    else
                    {

                    }
                }
            }

        }


        public void ExecuteMakerBot()
        {
            Console.WriteLine("Maker Bot.");
            CancelOrders();

            IssueNewProposal();
        }

        public void IssueNewProposal()
        {
            int numOrders = 10;
            float askSpread = 0.01f;
            float bidSpread = 0.01f;
            float offset = 0.0001f;
            float minVolume = 0.01f;
            float maxVolume = 11.57000f;

            var task = Task.Run(async () => await GetSnapshot());
            MarketDepth marketDepth = task.Result;

            var task2 = Task.Run(async () => await GetTickers());
            Ticker ticker = task2.Result;

            float lastPrice = float.Parse(ticker.Last);

            if (marketDepth.Asks.Any() && marketDepth.Bids.Count() < numOrders)
            {
                float minPriceSell = marketDepth.Asks.Min(ask => ask.First());


                for (int i = 0; i < numOrders; i++)
                {
                    float volume = rand.Next(minVolume, maxVolume);
                    //float price = rand.Next(minPriceSell - offset - (numOrders * bidSpread), minPriceSell - offset);
                    float price = rand.Next(((minPriceSell - offset) - (numOrders * bidSpread)), (minPriceSell - offset));

                    try
                    {
                        Order or = ExecuteOrder(OrderType.Buy, volume, price);

                        if (or != null)
                        {
                            NewOrders.Add(or);
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERROR couldn't execute BUY order. Price:" + price + " volume: " + volume);
                    }
                }
            }
            else if (!marketDepth.Bids.Any())
            {
                float volume = rand.Next(minVolume, maxVolume);
                float price = rand.Next(((lastPrice - offset) - (numOrders * bidSpread)), (lastPrice - offset));

                try
                {
                    Order or = ExecuteOrder(OrderType.Buy, volume, price);

                    if (or != null)
                    {
                        NewOrders.Add(or);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR couldn't execute BUY order. Price:" + price + " volume: " + volume);
                }

            }

            if (marketDepth.Bids.Any() && marketDepth.Asks.Count() < numOrders)
            {
                float maxPriceBuy = marketDepth.Bids.Max(bid => bid.First());

                for (int i = 0; i < numOrders; i++)
                {
                    float volume = rand.Next(minVolume, maxVolume);
                    float price = rand.Next(maxPriceBuy + offset, maxPriceBuy + offset + (numOrders * askSpread));

                    try
                    {
                        Order or = ExecuteOrder(OrderType.Sell, volume, price);

                        if (or != null)
                        {
                            NewOrders.Add(or);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERROR couldn't execute SELL order. Price:" + price + " volume: " + volume);
                    }
                }
            }
            else if (!marketDepth.Asks.Any())
            {
                float volume = rand.Next(minVolume, maxVolume);
                float price = rand.Next(lastPrice + offset, lastPrice + offset + (numOrders * askSpread));

                try
                {
                    Order or = ExecuteOrder(OrderType.Sell, volume, price);

                    if (or != null)
                    {
                        NewOrders.Add(or);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR couldn't execute SELL order. Price:" + price + " volume: " + volume);
                }
            }
        }

        public void PrintOrders()
        {
            var task = Task.Run(async () => await GetOrders());

            openOrders = task.Result;

            Console.WriteLine("Orders:");
            if (openOrders != null)
            {
                foreach (var order in openOrders)
                {
                    if (order.Market == TradingPair && order.OrdType == "limit")
                    {
                        if (order.State == "pending")
                        {
                            Console.WriteLine("\t" + order.Market + " " + order.Id
                                + " " + order.Side + " price: " + order.Price
                                + " volume: " + order.OriginVolume
                                + " " + order.State + " " + order.OrdType + " TradesCount: " + order.TradesCount);
                        }
                        else if (order.State == "wait")
                        {
                            Console.WriteLine("\t" + order.Market + " " + order.Id
                                + " " + order.Side + " price: " + order.Price
                                + " volume: " + order.OriginVolume
                                + " " + order.State + " " + order.OrdType + " TradesCount: " + order.TradesCount);
                        }
                        else if (order.State == "cancel")
                        {

                        }
                    }
                }
            }
        }

        public void PrintMarkets()
        {
            var task = Task.Run(async () => await GetMarkets());

            string markets = task.Result;

            Console.WriteLine("Markets:");
            List<Market> deserializedMarkets = JsonConvert.DeserializeObject<List<Market>>(markets);
            foreach (var market in deserializedMarkets)
            {
                Console.WriteLine("\t" + market.Name + " MinPrice: "
                    + market.MinPrice + " MaxPrice: " + market.MaxPrice + " MinAmount: " + market.MinAmount
                    + " PricePrecision: " + market.PricePrecision + " AmountPrecision: " + market.AmountPrecision);
            }
        }

        public void PrintBalances()
        {
            var task = Task.Run(async () => await GetAccountBalances());

            string balances = task.Result;

            if (string.IsNullOrEmpty(balances))
            {
                Console.WriteLine("Could not retrieve asset balances. Server returned malformed json.");
            }
            else
            {
                Console.WriteLine("Assets:");
                List<Balance> deserializedBalances = JsonConvert.DeserializeObject<List<Balance>>(balances);
                foreach (var balance in deserializedBalances)
                {
                    Console.WriteLine("\t" + balance.currency + " balance: " + balance.balance + " locked: " + balance.locked);
                }
            }

        }

        long GetTimestamp()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return now.ToUnixTimeMilliseconds();
        }

        private static byte[] StringEncode(string text)
        {
            var encoding = new UTF8Encoding();
            return encoding.GetBytes(text);
        }
        private static string HashEncode(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private static byte[] HashHMAC_SHA256(byte[] key, byte[] message)
        {
            var hash = new HMACSHA256(key);
            return hash.ComputeHash(message);
        }

        string GenerateSignature(string timestamp)
        {
            string query_string = timestamp + apiKey;


            byte[] hash = HashHMAC_SHA256(StringEncode(apiKeySecret), StringEncode(query_string));

            string hashResult = HashEncode(hash);

            return hashResult;
        }


        public string Get(string uri)
        {
            string timestamp = GetTimestamp().ToString();
            string signature = GenerateSignature(timestamp);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            //request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Accept = "application/json";
            request.UserAgent = userAgent;
            request.Headers["X-Auth-Apikey"] = apiKey;
            request.Headers["X-Auth-Nonce"] = timestamp;
            request.Headers["X-Auth-Signature"] = signature;

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        public async Task<string> PostAsync(string uri, string data, string contentType, string method = "POST")
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            string timestamp = GetTimestamp().ToString();
            string signature = GenerateSignature(timestamp);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.ContentLength = dataBytes.Length;
            request.ContentType = contentType;
            request.Method = method;
            request.Accept = "application/json";
            request.UserAgent = userAgent;
            request.Headers["X-Auth-Apikey"] = apiKey;
            request.Headers["X-Auth-Nonce"] = timestamp;
            request.Headers["X-Auth-Signature"] = signature;

            using (Stream requestBody = request.GetRequestStream())
            {
                await requestBody.WriteAsync(dataBytes, 0, dataBytes.Length);
            }

            using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync();
            }
        }


        public string Post(string uri, string data, string contentType, string method = "POST")
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            string timestamp = GetTimestamp().ToString();
            string signature = GenerateSignature(timestamp);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            //request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.ContentLength = dataBytes.Length;
            request.ContentType = contentType;
            request.Method = method;
            request.Accept = "application/json";
            request.UserAgent = userAgent;
            request.Headers["X-Auth-Apikey"] = apiKey;
            request.Headers["X-Auth-Nonce"] = timestamp;
            request.Headers["X-Auth-Signature"] = signature;

            using (Stream requestBody = request.GetRequestStream())
            {
                requestBody.Write(dataBytes, 0, dataBytes.Length);


                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (Stream stream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
                catch (Exception ex)
                {
                    return "";
                }
            }
        }
    }
}