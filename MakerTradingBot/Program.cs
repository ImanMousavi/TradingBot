using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MakerTradingBot
{
    public class Program
    {
        static string openware_api_url = "https://www.example.com/api/v2/peatio";
        static string openware_ranger_url = "wss://www.example.com/api/v2/ranger";
        static string ApiKey = "xxxxxxxxxxxxxxxxxxxxxx";
        static string ApiKeySecret = "xxxxxxxxxxxxxxxxxxxxxx";

        static OpenwareClient client;

        private static bool _quitRequested = false;
        private static object _syncLock = new object();
        private static AutoResetEvent _waitHandle = new AutoResetEvent(false);

        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            client.CancelOrders();

            Console.WriteLine("exit");
        }
        private static void SetQuitRequested()
        {
            lock (_syncLock)
            {
                _quitRequested = true;
            }
        }

        private static void MessagePump()
        {
            int ticks = 0;

            client = new OpenwareClient(openware_api_url, openware_ranger_url, ApiKey, ApiKeySecret);

            //client.CancelAllOrders();
            client.PrintMarkets();
            client.PrintBalances();

            do
            {
                if (ticks == 0 || ticks % 5 == 0)
                {
                    client.PrintOrders();

                    client.ExecuteMakerBot();
                    client.ExecuteTakerBot();

                    client.PrintOrders();
                }


                //client.PrintBalances();

                var task = Task.Run(async () => await client.GetAccountBalances());
                string balancesJSON = task.Result;
                if (!string.IsNullOrEmpty(balancesJSON))
                {
                    List<Balance> balances = JsonConvert.DeserializeObject<List<Balance>>(balancesJSON);
                    foreach (var balance in balances)
                    {
                        File.AppendAllText("wallets-" + balance.currency + ".csv", balance.currency + "," + balance.balance + "," + balance.locked + "\n");
                    }
                }

                System.Threading.Thread.Sleep(1000);
                //System.Threading.Thread.Sleep(100);

                ticks++;

            } while (!_quitRequested);
            _waitHandle.Set();
        }

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
            System.Diagnostics.Process.GetCurrentProcess().EnableRaisingEvents = true;

            Thread msgThread = new Thread(MessagePump);
            msgThread.Start();

            string command = string.Empty;
            do
            {
                command = Console.ReadLine();
                command = command.ToLower();
            } while (!(command == "stop" || command == "exit" || command == "quit"));
            
            SetQuitRequested();
            _waitHandle.WaitOne();

            client.CancelOrders();
        }
    }
}
