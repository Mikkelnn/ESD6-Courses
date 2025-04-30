using BFSAlgo.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DistriGraph.Modes
{
    public static class WorkerRunner
    {
        public static async Task RunAsync()
        {
            var endpoint = await PromptForEndpointAsync();

            while (true)
            {
                Console.WriteLine($"Connecting to coordinator at {endpoint.Address}:{endpoint.Port}...");

                try
                {
                    var worker = new Worker(endpoint.Address, endpoint.Port);
                    await worker.Start();
                    Console.WriteLine("Worker session completed.");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();
                }

                var action = PromptForNextAction();
                switch (action)
                {
                    case WorkerAction.Reconnect:
                        continue;

                    case WorkerAction.ChangeCoordinator:
                        endpoint = await PromptForEndpointAsync();
                        continue;

                    case WorkerAction.Exit:
                    default:
                        Console.WriteLine("Exiting worker.");
                        return;
                }
            }
        }

        private static async Task<IPEndPoint> PromptForEndpointAsync()
        {
            while (true)
            {
                Console.Write("Coordinator IP: ");
                var ipInput = Console.ReadLine();
                if (!IPAddress.TryParse(ipInput, out var ip))
                {
                    Console.WriteLine("Invalid IP address. Try again.");
                    continue;
                }

                Console.Write("Coordinator Port: ");
                var portInput = Console.ReadLine();
                if (!int.TryParse(portInput, out var port) || port is <= 0 or > 65535)
                {
                    Console.WriteLine("Invalid port number. Try again.");
                    continue;
                }

                return new IPEndPoint(ip, port);
            }
        }

        private static WorkerAction PromptForNextAction()
        {
            Console.WriteLine("Select next step:");
            Console.WriteLine("  [1] Reconnect to same coordinator");
            Console.WriteLine("  [2] Exit");
            Console.WriteLine("  [3] Connect to a different coordinator");

            while (true)
            {
                var key = Console.ReadKey(intercept: true).Key;
                Console.WriteLine();

                return key switch
                {
                    ConsoleKey.D1 => WorkerAction.Reconnect,
                    ConsoleKey.D2 => WorkerAction.Exit,
                    ConsoleKey.D3 => WorkerAction.ChangeCoordinator,
                    _ => WorkerAction.Unknown
                };
            }
        }

        private enum WorkerAction
        {
            Reconnect,
            Exit,
            ChangeCoordinator,
            Unknown
        }
    }


}
