using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
namespace NetworkCommunicationsFramework
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Network Communications Framework");
            Console.WriteLine("Copyright Dracon Interactive, 2020"); ;

            Console.WriteLine(" ");

            string lower = GetConstrainedInput("UDP or Sockets?", "Please enter 'udp' or 'sockets': ", new string[] { "udp", "sockets" });

            if (lower == "udp")
            {
                Console.WriteLine("Starting UDP Listener on port 11000");
                UDPListener.Start();
                bool continueListening = true;
                while (continueListening)
                {
                    string m = "'all' to print all received data, 'last' to print last packet and 'close' to close stream";
                    lower = GetConstrainedInput(m, m, new string[] { "all", "last", "close" });
                    if (lower == "all")
                    {
                        Console.Write(UDPListener.allData + "\n");
                    } else if (lower == "last")
                    {
                        Console.Write(UDPListener.lastData + "\n");
                    } else
                    {
                        Console.WriteLine("Closing Stream");
                        UDPListener.Stop();
                    }
                }
            } else
            {
                lower = GetConstrainedInput("Server or Client?", "Please enter 'server' or 'client': ", new string[] { "server", "client" });
                if (lower == "server")
                {
                    Console.WriteLine("Server launched");
                    SocketController.Execute(SocketController.ExecuteType.Server);
                } else if (lower == "client")
                {
                    Console.WriteLine("Client launched");
                    SocketController.Execute(SocketController.ExecuteType.Client);
                }
            }
        }

        static string GetConstrainedInput (string message, string errorMessage, string[] options)
        {
            Console.WriteLine(message);
            string lower = Console.ReadLine().ToLower();
            while (!options.Contains(lower))
            {
                Console.WriteLine(errorMessage);
                lower = Console.ReadLine().ToLower();
            }
            return lower;
        }
    }

    public class UDPListener
    {
        private const int listenPort = 11000;

        static bool listening;

        public static string allData;
        public static string lastData;
        public static void Start()
        {
            var t = new Thread(() => StartListener());
            t.Name = "Listener Thread";
            t.Priority = ThreadPriority.Normal;
            t.Start();
            Console.WriteLine("Listener Thread Started");
            t.IsBackground = true;
            listening = true;
        }

        public static void Stop ()
        {
            listening = false;
        }

        private static void StartListener()
        {
            UdpClient listener = new UdpClient(listenPort);
            IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, listenPort);

            try
            {
                using (var progress = new ProgressBar())
                {
                    while (listening)
                    {
                        byte[] bytes = listener.Receive(ref groupEP);
                        string output = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
                        allData += output;
                        lastData = output;
                        if (lastData.Contains("<EOF>"))
                        {
                            listening = false;
                            Console.WriteLine("End of file received from server. Closing stream");
                        }
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                listener.Close();
            }

            listener.Close();
        }
    }

    public class SocketController
    {
        public static string currentDataToSend = "0";
        public enum ExecuteType
        {
            Client,
            Server
        }

        public static void Execute(ExecuteType command)
        {
            if (command == ExecuteType.Client)
            {
                Console.WriteLine("Executing Client Thread");
                var t = new Thread(() => ExecuteClient());
                t.Name = "Socket Thread";
                t.Priority = ThreadPriority.Normal;
                t.Start();
                t.IsBackground = true;
            }
            else
            {
                Console.WriteLine("Executing Server Thread");
                var t = new Thread(() => ExecuteServer());
                t.Name = "Socket Thread";
                t.Priority = ThreadPriority.Normal;
                t.Start();
                t.IsBackground = true;
            }
        }

        public static void CloseClient()
        {
            currentDataToSend = "";
            Thread.Sleep(50);
            currentDataToSend = "0";
        }
        static void ExecuteClient()
        {
            try
            {
                IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipAddr = ipHost.AddressList[0];
                IPEndPoint localEndPoint = new IPEndPoint(ipAddr, 11111);
                Socket sender = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    sender.Connect(localEndPoint);
                    Console.WriteLine("Socket connected to -> {0} ", sender.RemoteEndPoint.ToString());

                    while (currentDataToSend != "")
                    {
                        byte[] messageSent = Encoding.ASCII.GetBytes(currentDataToSend);
                        int byteSent = sender.Send(messageSent);
                        Thread.Sleep(5);
                    }
                    byte[] lastMessage = Encoding.ASCII.GetBytes("Test Client<EOF>");
                    int lastByteSent = sender.Send(lastMessage);

                    byte[] messageRecieved = new byte[1024];
                    int byteRecv = sender.Receive(messageRecieved);
                    Console.WriteLine("Message from server -> {0}", Encoding.ASCII.GetString(messageRecieved, 0, byteRecv));

                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();
                }
                catch (ArgumentNullException ane)
                {
                    Console.WriteLine("ArgumentNullException: {0}", ane.ToString());
                }

                catch (SocketException se)
                {

                    Console.WriteLine("SocketException : {0}", se.ToString());
                }

                catch (Exception e)
                {
                    Console.WriteLine("Unexpected exception : {0}", e.ToString());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        static void ExecuteServer()
        {
            IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddr = ipHost.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddr, 11111);

            Socket listener = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(10);

                while (true)
                {
                    Console.WriteLine("Waiting for connection ... ");
                    Socket clientSocket = listener.Accept();

                    byte[] bytes = new byte[1024];
                    string data = null;

                    while (true)
                    {
                        int numByte = clientSocket.Receive(bytes);
                        //data += Encoding.ASCII.GetString(bytes, 0, numByte);
                        data = Encoding.ASCII.GetString(bytes, 0, numByte);
                        Console.WriteLine("Recieved -> {0} ", data);
                        if (data.IndexOf("<EOF>") > -1)
                        {
                            break;
                        }

                    }

                    //Console.WriteLine("Text received -> {0} ", data);
                    byte[] message = Encoding.ASCII.GetBytes("Test Server");
                    clientSocket.Send(message);
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }

    public class ProgressBar : IDisposable, IProgress<double>
    {
        private const int blockCount = 10;
        private readonly TimeSpan animationInterval = TimeSpan.FromSeconds(1.0 / 8);
        private const string animation = @"|/-\";

        private readonly Timer timer;

        private double currentProgress = 0;
        private string currentText = string.Empty;
        private bool disposed = false;
        private int animationIndex = 0;

        public ProgressBar()
        {
            timer = new Timer(TimerHandler);

            // A progress bar is only for temporary display in a console window.
            // If the console output is redirected to a file, draw nothing.
            // Otherwise, we'll end up with a lot of garbage in the target file.
            if (!Console.IsOutputRedirected)
            {
                ResetTimer();
            }
        }

        public void Report(double value)
        {
            // Make sure value is in [0..1] range
            value = Math.Max(0, Math.Min(1, value));
            Interlocked.Exchange(ref currentProgress, value);
        }

        private void TimerHandler(object state)
        {
            lock (timer)
            {
                if (disposed) return;

                int progressBlockCount = (int)(currentProgress * blockCount);
                int percent = (int)(currentProgress * 100);
                string text = string.Format("[{0}{1}] {2,3}% {3}",
                    new string('#', progressBlockCount), new string('-', blockCount - progressBlockCount),
                    percent,
                    animation[animationIndex++ % animation.Length]);
                UpdateText(text);

                ResetTimer();
            }
        }

        private void UpdateText(string text)
        {
            // Get length of common portion
            int commonPrefixLength = 0;
            int commonLength = Math.Min(currentText.Length, text.Length);
            while (commonPrefixLength < commonLength && text[commonPrefixLength] == currentText[commonPrefixLength])
            {
                commonPrefixLength++;
            }

            // Backtrack to the first differing character
            StringBuilder outputBuilder = new StringBuilder();
            outputBuilder.Append('\b', currentText.Length - commonPrefixLength);

            // Output new suffix
            outputBuilder.Append(text.Substring(commonPrefixLength));

            // If the new text is shorter than the old one: delete overlapping characters
            int overlapCount = currentText.Length - text.Length;
            if (overlapCount > 0)
            {
                outputBuilder.Append(' ', overlapCount);
                outputBuilder.Append('\b', overlapCount);
            }

            Console.Write(outputBuilder);
            currentText = text;
        }

        private void ResetTimer()
        {
            timer.Change(animationInterval, TimeSpan.FromMilliseconds(-1));
        }

        public void Dispose()
        {
            lock (timer)
            {
                disposed = true;
                UpdateText(string.Empty);
            }
        }

    }
}
