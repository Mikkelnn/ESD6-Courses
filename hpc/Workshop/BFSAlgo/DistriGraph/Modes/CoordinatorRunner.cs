using BFSAlgo;
using BFSAlgo.Distributed;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DistriGraph.Modes
{
    public static class CoordinatorRunner
    {
        public static async Task RunAsync()
        {
            var (ipAddress, port) = await InitializeCoordinatorAsync();

            // Create and start the coordinator
            var coordinator = new Coordinator(ipAddress, port);
            _ = coordinator.StartAsync();

            // Print the listening address for the coordinator
            string GetAddresses() {
                var addressList = Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                .Select(ip => ip.ToString());

                return $"<{string.Join(",", addressList)}>";
            }
            var actualIp = ipAddress == IPAddress.Any ? GetAddresses() : coordinator.ListeningOn.Address.ToString();
            Console.WriteLine($"Coordinator started, listening on {actualIp}:{coordinator.ListeningOn.Port}");

            await DisplayWorkerConnectionStatusAsync(coordinator);

            var graph = LoadGraphFromUser();
            var startNode = PromptForStartNode();

            Stopwatch stopwatch = Stopwatch.StartNew();
            var visited = await coordinator.RunAsync(graph, startNode);
            stopwatch.Stop();

            Console.WriteLine($"\nTraversal completed in {stopwatch}. ");
            Console.WriteLine($"Visited {visited.CountSetBits()} of {graph.Length} nodes.");
        }

        public static async Task<(IPAddress ipAddress, int port)> InitializeCoordinatorAsync()
        {
            IPAddress ipAddress;
            // Prompt the user until valid IP address input
            while (true)
            {
                Console.Write("Enter Coordinator IP (default: All IP addresses): ");
                string ipInput = Console.ReadLine();

                if (string.IsNullOrEmpty(ipInput))
                {
                    ipAddress = IPAddress.Any; // Default to all IP addresses
                    break;
                }

                if (IPAddress.TryParse(ipInput, out ipAddress))
                {
                    break; // Valid IP, exit loop
                }
                else
                {
                    Console.WriteLine("Invalid IP address. Please enter a valid IP.");
                }
            }

            int port;
            // Prompt the user until valid port input
            while (true)
            {
                Console.Write("Port (leave empty for random): ");
                string portInput = Console.ReadLine();

                if (string.IsNullOrEmpty(portInput))
                {
                    port = 0; // Random port
                    break;
                }

                if (int.TryParse(portInput, out port) && port >= 1 && port <= 65535)
                {
                    break; // Valid port, exit loop
                }
                else
                {
                    Console.WriteLine("Invalid port. Please enter a valid port number between 1 and 65535.");
                }
            }

            return (ipAddress, port);
        }


        private static async Task DisplayWorkerConnectionStatusAsync(Coordinator coordinator)
        {
            Console.WriteLine("Waiting for workers...");
            int line = Console.CursorTop;

            while (true)
            {
                // Update the worker count and reset the cursor position to overwrite the previous output
                Console.SetCursorPosition(0, line);
                Console.Write($"Time: {DateTime.Now}: Connected workers: {coordinator.ConnectedWorkers}".PadRight(Console.WindowWidth - 1));

                // Display a message below the worker count
                Console.SetCursorPosition(0, line + 1);
                Console.WriteLine("Press [S] to start coordination, any other key to refresh.".PadRight(Console.WindowWidth - 1));

                // Wait up to 1 second or until a key is pressed
                for (int i = 0; i < 10; i++) // 10 * 100ms = 1s
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true).Key;
                        if (key == ConsoleKey.S)
                            return; // break the loop and return
                    }

                    await Task.Delay(100);
                }
            }
        }

        private static List<uint>[] LoadGraphFromUser()
        {
            while (true)
            {
                Console.Write("Path to .bin graph file: ");
                var path = Console.ReadLine();

                Console.WriteLine($"Loading graph...");
                try
                {
                    return GraphService.LoadGraph(path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading graph: {ex.Message}");
                }
            }
        }

        private static uint PromptForStartNode()
        {
            while (true)
            {
                Console.Write("Start node ID: ");
                var input = Console.ReadLine();
                if (uint.TryParse(input, out var id))
                    return id;

                Console.WriteLine("Invalid input. Must be a non-negative integer.");
            }
        }
    }
}
