using System;

namespace EasyPortAnalyzer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            try
            {
                Console.WriteLine("Welcome to the Easy Port Analyzer!");
                Console.WriteLine("Created by Joaquin Navarro for joaquinlab.com");
                Console.Write("Enter target IP or hostname: ");
                string target = Console.ReadLine();

                int startPort = 0, endPort = 0;
                List<int> specificPorts = null;
                int choice = DisplayMenu();

                switch (choice)
                {
                    case 1:
                        startPort = 0;
                        endPort = 1023;
                        break;
                    case 2:
                        startPort = 1024;
                        endPort = 49151;
                        break;
                    case 3:
                        startPort = 49152;
                        endPort = 65535;
                        break;
                    case 4:
                        Console.Write("Enter starting port: ");
                        startPort = int.Parse(Console.ReadLine() ?? "0");

                        Console.Write("Enter ending port: ");
                        endPort = int.Parse(Console.ReadLine() ?? "0");
                        break;
                    case 5:
                        Console.Write("Enter specific ports (comma-separated): ");
                        specificPorts = Console.ReadLine()?.Split(',').Select(int.Parse).ToList();
                        break;
                    default:
                        Console.WriteLine("Invalid choice. Defaulting to Well-Known Ports.");
                        startPort = 0;
                        endPort = 1023;
                        break;
                }

                Console.WriteLine("\nScanning ports...");
                List<PortScanResult> results;
                if (specificPorts != null)
                {
                    results = await PortScanner.ScanSpecificPortsAsync(target, specificPorts);
                }
                else
                {
                    results = await PortScanner.ScanAsync(target, startPort, endPort);
                }

                Console.WriteLine("\nScan complete!");
                PrintResults(results, target);


            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        static int DisplayMenu()
        {
            string[] options = {
                    "Well-Known Ports (0–1023)",
                    "Registered Ports (1024–49151)",
                    "Dynamic/Private Ports (49152–65535)",
                    "Custom Range",
                    "Specific Ports"
                };

            int selectedIndex = 0;

            ConsoleKey key;
            do
            {
                Console.Clear();
                Console.WriteLine("Select port range to scan:");
                for (int i = 0; i < options.Length; i++)
                {
                    if (i == selectedIndex)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"> {options[i]}");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"  {options[i]}");
                    }
                }

                key = Console.ReadKey(true).Key;

                if (key == ConsoleKey.UpArrow)
                {
                    selectedIndex = (selectedIndex == 0) ? options.Length - 1 : selectedIndex - 1;
                }
                else if (key == ConsoleKey.DownArrow)
                {
                    selectedIndex = (selectedIndex == options.Length - 1) ? 0 : selectedIndex + 1;
                }
            } while (key != ConsoleKey.Enter);

            return selectedIndex + 1;
        }

        static void PrintResults(List<PortScanResult> results, string ip)
        {
            int currentLine = 0;
            int pageSize = (Console.WindowHeight - 6); // Number of lines that can be displayed at once per column
            bool showAll = false;

            while (true)
            {
                Console.Clear();
                PrintTableHeader(ip);

                var filteredResults = showAll ? results : results.FindAll(r => r.IsTcpOpen || r.IsUdpOpen);

                for (int i = 0; i < pageSize; i++)
                {
                    int index1 = currentLine + i;
                    int index2 = currentLine + i + pageSize;
                    int index3 = currentLine + i + 2 * pageSize;

                    if (index1 < filteredResults.Count)
                    {
                        var result1 = filteredResults[index1];
                        PrintResult(result1);
                        Console.Write("\t| ");
                    }
                    else
                    {
                        Console.Write("\t\t\t| ");
                    }

                    if (index2 < filteredResults.Count)
                    {
                        var result2 = filteredResults[index2];
                        PrintResult(result2);
                        Console.Write("\t| ");
                    }
                    else
                    {
                        Console.Write("\t\t\t| ");
                    }

                    if (index3 < filteredResults.Count)
                    {
                        var result3 = filteredResults[index3];
                        PrintResult(result3);
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine();
                    }
                }

                Console.WriteLine("\nUse Up/Down arrows to scroll line by line, Left/Right arrows to scroll page by page, Space to toggle view, Esc to exit.");

                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.DownArrow && currentLine + pageSize < filteredResults.Count)
                {
                    currentLine++;
                }
                else if (key == ConsoleKey.UpArrow && currentLine > 0)
                {
                    currentLine--;
                }
                else if (key == ConsoleKey.RightArrow && currentLine + 3 * pageSize < filteredResults.Count)
                {
                    currentLine = Math.Min(currentLine + 3 * pageSize, filteredResults.Count - 3 * pageSize);
                }
                else if (key == ConsoleKey.LeftArrow && currentLine > 0)
                {
                    currentLine = Math.Max(currentLine - 3 * pageSize, 0);
                }
                else if (key == ConsoleKey.Spacebar)
                {
                    showAll = !showAll;
                    currentLine = 0; // Reset to the top of the list
                }
                else if (key == ConsoleKey.Escape)
                {
                    break;
                }
                else if (key == ConsoleKey.S)
                {
                    ExportResultsToCsv(results, ip);
                }
            }
        }

        static void PrintResult(PortScanResult result)
        {
            Console.Write($"{result.Port}\t");
            Console.ForegroundColor = result.IsTcpOpen ? ConsoleColor.Green : ConsoleColor.Red;
            Console.Write($"{(result.IsTcpOpen ? "Open" : "Closed")}\t");
            Console.ResetColor();
            Console.ForegroundColor = result.IsUdpOpen ? ConsoleColor.Green : ConsoleColor.Red;
            Console.Write($"{(result.IsUdpOpen ? "Open" : "Closed")}");
            Console.ResetColor();
        }

        static void PrintTableHeader(string ip)
        {
            Console.WriteLine();
            Console.WriteLine($"Results for {ip}: \t\t\tPress 's' to save the results to a CSV file");
            Console.WriteLine("Port\tTCP\tUDP\t| Port\tTCP\tUDP\t| Port\tTCP\tUDP");
            Console.WriteLine("------------------------------------------------------------------------");
        }

        static void ExportResultsToCsv(List<PortScanResult> results, string ip)
        {
            string fileName = $"PortsTo{ip.Replace('.', '-')}.csv";
            string directory = Directory.GetCurrentDirectory();
            string fullPath = Path.Combine(directory, fileName);

            using (var writer = new StreamWriter(fullPath))
            {
                writer.WriteLine("Port,TCP,UDP");
                foreach (var result in results)
                {
                    writer.WriteLine($"{result.Port},{(result.IsTcpOpen ? "Open" : "Closed")},{(result.IsUdpOpen ? "Open" : "Closed")}");
                }
            }
            Console.Clear();
            Console.WriteLine($"\nResults exported to {fullPath}");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true); // Wait for the user to press any key
        }

        static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine($"An unhandled exception occurred: {((Exception)e.ExceptionObject).Message}");
            Environment.Exit(1);
        }
    }
}
