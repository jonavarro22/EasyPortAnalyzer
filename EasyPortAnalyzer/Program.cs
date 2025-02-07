using System;
using System.Net;

namespace EasyPortAnalyzer
{
    class Program
    {
        static bool keepRunning = true;
        static string lastUsedIp = string.Empty;

        static async Task Main(string[] args)
        {
            // Handle unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            try
            {
                Console.WriteLine("Welcome to the Easy Port Analyzer!");
                Console.WriteLine("Created by Joaquin Navarro for joaquinlab.com");
                Console.WriteLine("Press Ctrl+C to exit at any time.\n");

                while (keepRunning)
                {
                    keepRunning = true;

                    string target = GetTargetIp();

                    int startPort = 0, endPort = 0;
                    List<int> specificPorts = null;

                    // Check if ports file exists and offer to read ports from it
                    if (File.Exists("Ports.txt") || File.Exists("Ports.csv"))
                    {
                        Console.WriteLine("A ports file (Ports.txt or Ports.csv) is available.");
                        Console.Write("Do you want to read ports from the file? (y/n): ");
                        if (Console.ReadLine()?.Trim().ToLower() == "y")
                        {
                            specificPorts = ReadPortsFromFile();
                        }
                    }

                    // If no specific ports were read from the file, display the menu
                    if (specificPorts == null)
                    {
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
                                startPort = GetPortInput("Enter starting port: ");
                                endPort = GetPortInput("Enter ending port: ");
                                break;
                            case 5:
                                Console.Write("Enter specific ports (comma-separated): ");
                                specificPorts = Console.ReadLine()?.Split(',').Select(int.Parse).ToList();
                                SavePortsToFile(specificPorts); // Save specific ports to file
                                break;
                            case 6:
                                Console.WriteLine("Warning: Scanning all ports (0-65535) might take a long time.");
                                Console.Write("Do you want to proceed? (y/n): ");
                                if (Console.ReadLine()?.Trim().ToLower() == "y")
                                {
                                    startPort = 0;
                                    endPort = 65535;
                                }
                                else
                                {
                                    Console.WriteLine("Operation cancelled.");
                                    return;
                                }
                                break;
                            default:
                                Console.WriteLine("Invalid choice. Defaulting to Well-Known Ports.");
                                startPort = 0;
                                endPort = 1023;
                                break;
                        }
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        static string GetTargetIp()
        {
            while (true)
            {
                if (!string.IsNullOrEmpty(lastUsedIp))
                {
                    Console.Write($"Use the last used IP ({lastUsedIp})? (y/n): ");
                    if (Console.ReadLine()?.Trim().ToLower() == "y")
                    {
                        return lastUsedIp;
                    }
                }

                Console.Write("Enter target IP or hostname: ");
                string input = Console.ReadLine();

                if (IsValidIpOrHostname(input))
                {
                    lastUsedIp = input;
                    return lastUsedIp;
                }
                else
                {
                    Console.WriteLine("Invalid IP address or hostname. Please try again.");
                }
            }
        }

        static bool IsValidIpOrHostname(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            // Check if input is a valid IP address
            if (IPAddress.TryParse(input, out _))
            {
                return true;
            }

            // Check if input is a valid hostname
            try
            {
                var hostEntry = Dns.GetHostEntry(input);
                return hostEntry != null;
            }
            catch
            {
                return false;
            }
        }

        static int GetPortInput(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                if (int.TryParse(Console.ReadLine(), out int port) && port >= 0 && port <= 65535)
                {
                    return port;
                }
                else
                {
                    Console.WriteLine("Invalid port number. Please enter a number between 0 and 65535.");
                }
            }
        }

        // Read ports from file
        static List<int> ReadPortsFromFile()
        {
            string filePath = File.Exists("Ports.txt") ? "Ports.txt" : "Ports.csv";
            string fileContent = File.ReadAllText(filePath);
            return fileContent.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(int.Parse)
                              .ToList();
        }

        // Save ports to file
        static void SavePortsToFile(List<int> ports)
        {
            try
            {
                File.WriteAllText("Ports.txt", string.Join(",", ports));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save ports to file: {ex.Message}");
            }
        }

        // Display menu and get user choice
        static int DisplayMenu()
        {
            string[] options = {
                                                "Well-Known Ports (0–1023)",
                                                "Registered Ports (1024–49151)",
                                                "Dynamic/Private Ports (49152–65535)",
                                                "Custom Range",
                                                "Specific Ports",
                                                "All Ports (0-65535)"
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

        // Print scan results
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

                Console.WriteLine("\nUse Up/Down arrows to scroll line by line, Left/Right arrows to scroll page by page, Space to toggle view, Enter to go back to the menu, Esc to exit.");

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
                else if (key == ConsoleKey.Enter)
                {
                    Console.Clear();
                    break;
                }
                else if (key == ConsoleKey.Escape)
                {
                    keepRunning = false;
                    break;
                }
                else if (key == ConsoleKey.S)
                {
                    ExportResultsToCsv(results, ip);
                }
            }
        }

        // Print individual result
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

        // Print table header
        static void PrintTableHeader(string ip)
        {
            Console.WriteLine();
            Console.WriteLine($"Results for {ip}: \t\t\tPress 's' to save the results to a CSV file");
            Console.WriteLine("Port\tTCP\tUDP\t| Port\tTCP\tUDP\t| Port\tTCP\tUDP");
            Console.WriteLine("------------------------------------------------------------------------");
        }

        // Export results to CSV file
        static void ExportResultsToCsv(List<PortScanResult> results, string ip)
        {
            try
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to export results to CSV: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true); // Wait for the user to press any key
            }
        }

        // Handle unhandled exceptions
        static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine($"An unhandled exception occurred: {((Exception)e.ExceptionObject).Message}");
            Environment.Exit(1);
        }
    }
}
