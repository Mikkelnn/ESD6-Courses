using DistriGraph.Modes;
using System;
using System.Threading.Tasks;

namespace GraphPilot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "GraphPilot - Distributed Graph Traversal";

            while (true)
            {
                Console.WriteLine("=== GraphPilot ===");
                Console.WriteLine("1. Run as Coordinator");
                Console.WriteLine("2. Run as Worker");
                Console.WriteLine("3. Exit");
                Console.Write("Choose mode: ");

                var key = Console.ReadKey(intercept: true).Key;
                Console.WriteLine();

                try
                {
                    switch (key)
                    {
                        case ConsoleKey.D1:
                            await CoordinatorRunner.RunAsync();
                            break;
                        case ConsoleKey.D2:
                            await WorkerRunner.RunAsync();
                            break;
                        case ConsoleKey.D3:
                            Console.WriteLine("Exiting...");
                            return;
                        default:
                            Console.WriteLine("Invalid choice, try again.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();
                }

                Console.WriteLine("\nPress any key to return to main menu...");
                Console.ReadKey(true);
                Console.Clear();
            }
        }
    }
}
