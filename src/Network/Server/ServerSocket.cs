﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Configuration;
using WRPServer.Network.Client;

namespace WRPServer.Network.Server
{
    public class ServerSocket
    {
        // Thread signal  
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        /// <summary>
        /// Spustí pol
        /// </summary>
        /// <param name="serverCtx"></param>
        public static void StartListening(ServerContext serverCtx)
        {
            // Vytvorim lokalni interface - dvojici localhost:11001
            IPAddress ipAddress = GetLocalIPAddress();
            Int32 port = Convert.ToInt32(ConfigurationManager.AppSettings["serverPort"]);
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            // TCP Server socket
            Socket listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);
            log.Info("Inicializuji datove struktury socketu pro "+ ipAddress.ToString()+":"+port);
            // Nabinduju server socket na lokalni interface
            try
            {
                listener.Bind(localEndPoint);
                // Nejvyse 100 cekajicich spojeni
                listener.Listen(Convert.ToInt32(ConfigurationManager.AppSettings["maxWaitingConnections"]));

                log.Info("Vstupuji do smycky pro prijimani klientu.");
                while (true)
                {
                    // Novy socket
                    log.Info("Cekam na pripojeni klienta.");
                    ClientContext ctx = new ClientContext();
                    try
                    {
                        Socket clientSocket = listener.Accept();
                        log.Info("Pripojil se novy klient! Inicializuji ClientContext.");
                        // Inicializace kontextu klienta
                        ctx.socket = clientSocket;
                        ctx.clientId = serverCtx.CreateNewClientId();
                    }
                    catch (ObjectDisposedException ode)
                    {
                        log.Error("ServerSocket byl ukoncen. Bude proved pokus o jeho obnovu.", ode);
                        log.Info("Pred pokusem o opetovne pripojeni bude vyckano 10s.");
                        Thread.Sleep(10 * 1000);
                        log.Info("Pokousim se o opetovne spusteni ServerSocket.");
                        listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        listener.Bind(localEndPoint);
                        listener.Listen(Convert.ToInt32(ConfigurationManager.AppSettings["maxWaitingConnections"]));
                        log.Info("Pokus o opetovne pripojeni dokoncen.");
                        continue;
                    }
                    // Obsluha klienta v novem vlaknu
                    log.Info("Predavam obsluhu noveho klienta do dedikovaneho vlakna.");
                    ClientHandler clientHandler = new ClientHandler(serverCtx, ctx);
                    clientHandler.StartHandler();
                }
            }
            catch (Exception e)
            {
                log.Error("Pri inicializaci ServerSocket nastala chyba.", e);
            }
        }

    }
}
