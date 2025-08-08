using System.Net;
using System.Linq;
using System.Threading;

namespace EasyPortAnalyzer
{
    class Program
    {
        static bool keepRunning = true;
        static string lastUsedIp = string.Empty;
        const string open = "Open";
        const string filtered = "Filtered";
        const string closed = "Closed";
        const int PortFieldWidth = 3;
        const int TcpFieldWidth = 8;
        const int UdpFieldWidth = 8;
        const int SpacePortTcp = 3;
        const int SpaceTcpUdp = 1;
        const int SpaceUdpPort = 4;

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            Console.WriteLine("Welcome to the Easy Port Analyzer!");
            Console.WriteLine("Created by Joaquin Navarro for joaquinlab.com");
            Console.WriteLine("Press Ctrl+C to exit at any time.\n");

            while (keepRunning)
            {
                string target = await GetTargetIpAsync();

                int startPort = 0, endPort = 0;
                List<int>? specificPorts = null;

                if (File.Exists("Ports.txt") || File.Exists("Ports.csv"))
                {
                    Console.WriteLine("A ports file (Ports.txt or Ports.csv) is available.");
                    Console.Write("Do you want to read ports from the file? (y/n): ");
                    if (Console.ReadLine()?.Trim().ToLower() == "y")
                    {
                        specificPorts = ReadPortsFromFile();
                    }
                }

                if (specificPorts == null)
                {
                    int choice = DisplayMenu();
                    switch (choice)
                    {
                        case 1: startPort = 0; endPort = 1023; break;
                        case 2: startPort = 1024; endPort = 49151; break;
                        case 3: startPort = 49152; endPort = 65535; break;
                        case 4:
                            startPort = GetPortInput("Enter starting port: ");
                            endPort = GetPortInput("Enter ending port: ");
                            break;
                        case 5:
                            Console.Write("Enter specific ports (comma-separated): ");
                            var inputPorts = Console.ReadLine();
                            if (!string.IsNullOrWhiteSpace(inputPorts))
                            {
                                specificPorts = inputPorts
                                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(p => int.TryParse(p.Trim(), out int port) && port >= 0 && port <= 65535 ? port : -1)
                                    .Where(p => p != -1)
                                    .ToList();

                                if (specificPorts.Count > 0)
                                    SavePortsToFile(specificPorts);
                            }
                            break;
                        case 6:
                            Console.WriteLine("Warning: Scanning all ports (0-65535) might take a long time.");
                            Console.Write("Do you want to proceed? (y/n): ");
                            if (Console.ReadLine()?.Trim().ToLower() == "y")
                            {
                                startPort = 0; endPort = 65535;
                            }
                            else
                            {
                                Console.WriteLine("Operation cancelled.");
                                continue;
                            }
                            break;
                        default:
                            Console.WriteLine("Invalid choice. Defaulting to Well-Known Ports.");
                            startPort = 0; endPort = 1023;
                            break;
                    }
                }

                Console.WriteLine("\nScanning ports... (Press ESC to cancel)");

                using var cts = new CancellationTokenSource();
                var escListener = Task.Run(() =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                        {
                            cts.Cancel();
                            break;
                        }
                    }
                });

                List<PortScanResult> results;
                try
                {
                    if (specificPorts != null)
                        results = await PortScanner.ScanSpecificPortsAsync(target, specificPorts, timeoutMs: 1000, maxConcurrency: 200, cts.Token);
                    else
                        results = await PortScanner.ScanAsync(target, startPort, endPort, timeoutMs: 1000, maxConcurrency: 200, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\nScan cancelled by user.");
                    continue;
                }

                if (!results.Any(r => r.IsTcpOpen || r.IsUdpOpen))
                {
                    if (HandleNoOpenPorts(target))
                        continue;
                    else
                        break;
                }

                PrintResults(results, target);

                int option = DisplayScanOptionsMenu();
                switch (option)
                {
                    case 1: break; // same IP
                    case 2: lastUsedIp = string.Empty; break;
                    case 3: keepRunning = false; break;
                }
            }
        }

        static async Task<string> GetTargetIpAsync()
        {
            while (true)
            {
                if (!string.IsNullOrEmpty(lastUsedIp))
                    return lastUsedIp;

                Console.Write("Enter target IP or hostname: ");
                string? input = Console.ReadLine();

                if (input is not null && await IsValidIpOrHostnameAsync(input))
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

        static async Task<bool> IsValidIpOrHostnameAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            if (IPAddress.TryParse(input, out _)) return true;

            try
            {
                var hostEntry = await Dns.GetHostEntryAsync(input);
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
                    return port;
                else
                    Console.WriteLine("Invalid port number. Please enter a number between 0 and 65535.");
            }
        }

        static List<int> ReadPortsFromFile()
        {
            string filePath = File.Exists("Ports.txt") ? "Ports.txt" : "Ports.csv";
            var ports = new List<int>();

            foreach (var token in File.ReadAllText(filePath)
                .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(token.Trim(), out int port) && port >= 0 && port <= 65535)
                    ports.Add(port);
            }
            return ports;
        }

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

        // New method for scan options menu with arrow key selection
        static int DisplayScanOptionsMenu()
        {
            string[] options = {
                "Scan again with same IP",
                "Scan a different IP",
                "Exit"
            };

            int selectedIndex = 0;

            ConsoleKey key;
            do
            {
                Console.Clear();
                Console.WriteLine("Scan options:");

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

        // Show message when no open ports were found and ask next action
        static bool HandleNoOpenPorts(string ip)
        {
            int selectedIndex = 0;
            string[] options = { "Retry same IP", "Change IP", "Exit" };

            ConsoleKey key;
            do
            {
                Console.Clear();
                Console.WriteLine($"No open ports were found on {ip}.");
                Console.WriteLine("Choose an option:");

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
                else if (key == ConsoleKey.Escape)
                {
                    // Treat Esc as Exit
                    keepRunning = false;
                    Console.Clear();
                    return false;
                }
            } while (key != ConsoleKey.Enter);

            Console.Clear();
            switch (selectedIndex)
            {
                case 0:
                    // Retry same IP
                    return true;
                case 1:
                    // Change IP on next loop
                    lastUsedIp = string.Empty;
                    return true;
                default:
                    keepRunning = false;
                    return false;
            }
        }

        // Print scan results with smart display toggle
        static void PrintResults(List<PortScanResult> results, string ip)
        {
            int currentLine = 0; // top visible row index
            int maxRows = (Console.WindowHeight - 6); // more space for results
            int totalVisibleCapacity = maxRows * 3;

            // Show all ports unless too many to fit
            bool showAll = results.Count <= totalVisibleCapacity;

            while (true)
            {
                Console.Clear();

                // Compact header
                string viewMode = showAll ? "all ports" : "open ports only";
                Console.WriteLine($"[{ip}]");
                Console.WriteLine("Showing: {viewMode} | Scroll using arrows | Space toggle | S to save | Esc to exit");

                // Compact legend
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Open");
                Console.ResetColor();
                Console.Write("=Green  ");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Filtered");
                Console.ResetColor();
                Console.Write("=Yellow  ");

                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("Closed");
                Console.ResetColor();
                Console.Write("=Red");

                // Short filtered note only if needed
                if (showAll && results.Any(r => r.TcpFiltered || r.UdpFiltered))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write(" Note: Filtered = no response (possible firewall)");
                    Console.ResetColor();
                }

                Console.WriteLine("");

                var filteredResults = showAll ? results : results.FindAll(r => r.IsTcpOpen || r.IsUdpOpen);

                if (filteredResults.Count == 0)
                {
                    Console.WriteLine("\nNo results to display in this view.");
                    Console.WriteLine("Press Space to show all ports, Enter to go back, or Esc to exit.");

                    var emptyKey = Console.ReadKey(true).Key;
                    if (emptyKey == ConsoleKey.Spacebar)
                    {
                        showAll = !showAll;
                        currentLine = 0;
                        continue;
                    }
                    else if (emptyKey == ConsoleKey.Escape)
                    {
                        keepRunning = false;
                        break;
                    }
                    else if (emptyKey == ConsoleKey.Enter)
                    {
                        Console.Clear();
                        break;
                    }
                    else if (emptyKey == ConsoleKey.S)
                    {
                        ExportResultsToCsv(results, ip);
                    }
                    continue;
                }

                // Column layout
                int relPortX = 0;
                int relTcpX = relPortX + PortFieldWidth + SpacePortTcp;
                int relUdpX = relTcpX + TcpFieldWidth + SpaceTcpUdp;

                int minColumnWidth = relUdpX + UdpFieldWidth;
                int colWidth = minColumnWidth;
                int col1X = 0;
                int col2X = Math.Min(Console.BufferWidth - 1, col1X + colWidth + SpaceUdpPort);
                int col3X = Math.Min(Console.BufferWidth - 1, col2X + colWidth + SpaceUdpPort);

                // Column headers
                Console.SetCursorPosition(col1X + relPortX, Console.CursorTop);
                Console.Write("Port");
                Console.SetCursorPosition(col1X + relTcpX, Console.CursorTop);
                Console.Write("TCP");
                Console.SetCursorPosition(col1X + relUdpX, Console.CursorTop);
                Console.Write("UDP");

                Console.SetCursorPosition(col2X + relPortX - 2, Console.CursorTop);
                Console.Write("|");

                Console.SetCursorPosition(col2X + relPortX, Console.CursorTop);
                Console.Write("Port");
                Console.SetCursorPosition(col2X + relTcpX, Console.CursorTop);
                Console.Write("TCP");
                Console.SetCursorPosition(col2X + relUdpX, Console.CursorTop);
                Console.Write("UDP");

                Console.SetCursorPosition(col3X + relPortX - 2, Console.CursorTop);
                Console.Write("|");

                Console.SetCursorPosition(col3X + relPortX, Console.CursorTop);
                Console.Write("Port");
                Console.SetCursorPosition(col3X + relTcpX, Console.CursorTop);
                Console.Write("TCP");
                Console.SetCursorPosition(col3X + relUdpX, Console.CursorTop);
                Console.Write("UDP");

                Console.WriteLine();
                int clearLen = Math.Min(Console.WindowWidth - 1, col3X + colWidth);
                Console.WriteLine(new string('-', Math.Max(0, clearLen)));

                int bodyTop = Console.CursorTop;
                int totalRows = (int)Math.Ceiling(filteredResults.Count / 3.0);
                int visibleRows = Math.Min(maxRows, Math.Max(0, totalRows - currentLine));

                int maxRowsByBuffer = Math.Max(0, Console.BufferHeight - bodyTop - 1);
                visibleRows = Math.Min(visibleRows, maxRowsByBuffer);

                for (int i = 0; i < visibleRows; i++)
                {
                    int baseRow = currentLine + i;
                    int index1 = baseRow;
                    int index2 = baseRow + totalRows;
                    int index3 = baseRow + 2 * totalRows;

                    int targetY = bodyTop + i;
                    if (targetY < 0 || targetY >= Console.BufferHeight) break;

                    Console.SetCursorPosition(0, targetY);
                    Console.Write(new string(' ', Math.Max(0, clearLen)));

                    if (index1 < filteredResults.Count)
                        PrintCellAt(filteredResults[index1], col1X + relPortX, col1X + relTcpX, col1X + relUdpX, targetY);
                    if (index2 < filteredResults.Count)
                    {
                        Console.SetCursorPosition(col2X + relPortX - 2, Console.CursorTop);
                        Console.Write("|");
                        PrintCellAt(filteredResults[index2], col2X + relPortX, col2X + relTcpX, col2X + relUdpX, targetY);
                    }
                        
                    if (index3 < filteredResults.Count)
                    {
                        Console.SetCursorPosition(col3X + relPortX - 2, Console.CursorTop);
                        Console.Write("|");
                        PrintCellAt(filteredResults[index3], col3X + relPortX, col3X + relTcpX, col3X + relUdpX, targetY);
                    }        
                }

                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.DownArrow)
                {
                    if (currentLine + visibleRows < totalRows) currentLine++;
                }
                else if (key == ConsoleKey.UpArrow)
                {
                    if (currentLine > 0) currentLine--;
                }
                else if (key == ConsoleKey.RightArrow)
                {
                    currentLine = Math.Min(currentLine + visibleRows, Math.Max(totalRows - visibleRows, 0));
                }
                else if (key == ConsoleKey.LeftArrow)
                {
                    currentLine = Math.Max(currentLine - visibleRows, 0);
                }
                else if (key == ConsoleKey.Spacebar)
                {
                    showAll = !showAll;
                    currentLine = 0;
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

        // Helper: print one row aligned at given absolute positions for Port/TCP/UDP
        static void PrintCellAt(PortScanResult result, int absPortX, int absTcpX, int absUdpX, int y)
        {
            int safeY = Math.Min(Math.Max(0, y), Math.Max(0, Console.BufferHeight - 1));

            // Port aligned under "Port"
            Console.SetCursorPosition(Math.Min(absPortX, Console.BufferWidth - 1), safeY);
            string portText = result.Port.ToString();
            Console.Write(portText);

            // TCP aligned under "TCP"
            Console.SetCursorPosition(Math.Min(absTcpX, Console.BufferWidth - 1), safeY);
            var tcpStatus = (result.IsTcpOpen ? open : (result.TcpFiltered ? filtered : closed)).PadRight(TcpFieldWidth + PortFieldWidth);
            Console.ForegroundColor = result.IsTcpOpen ? ConsoleColor.Green : (result.TcpFiltered ? ConsoleColor.Yellow : ConsoleColor.Red);
            Console.Write(tcpStatus);
            Console.ResetColor();

            // UDP aligned under "UDP"
            Console.SetCursorPosition(Math.Min(absUdpX, Console.BufferWidth - 1), safeY);
            var udpStatus = (result.IsUdpOpen ? open : (result.UdpFiltered ? filtered : closed)).PadRight(UdpFieldWidth);
            Console.ForegroundColor = result.IsUdpOpen ? ConsoleColor.Green : (result.UdpFiltered ? ConsoleColor.Yellow : ConsoleColor.Red);
            Console.Write(udpStatus);
            Console.ResetColor();
        }

        // Print table header
        static void PrintTableHeader(string ip)
        {
            Console.WriteLine();
            Console.WriteLine($"Legend: {open.Trim()} = Green, {filtered} = Yellow (no response/timeout), {closed.Trim()} = Red");
            Console.WriteLine($"Results for {ip}: \t\t\tPress 's' to save the results to a CSV file");
            // Column headings and separators are handled by PrintResults using cursor positioning
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
                        string tcp = (result.IsTcpOpen ? open : (result.TcpFiltered ? filtered : closed)).Trim();
                        string udp = (result.IsUdpOpen ? open : (result.UdpFiltered ? filtered : closed)).Trim();
                        writer.WriteLine($"{result.Port},{tcp},{udp}");
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