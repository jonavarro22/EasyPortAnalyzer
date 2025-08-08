using System.Net;
using System.Linq;

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
        const int SpacePortTcp = 3;    // spaces between Port and TCP
        const int SpaceTcpUdp = 1;     // spaces between TCP and UDP
        const int SpaceUdpPort = 6; // spaces between UDP and Port in the table
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
                    List<int>? specificPorts = null;

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
                                var inputPorts = Console.ReadLine();
                                if (!string.IsNullOrWhiteSpace(inputPorts))
                                {
                                    specificPorts = inputPorts
                                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(p => int.Parse(p.Trim()))
                                        .ToList();

                                    if (specificPorts.Count > 0)
                                    {
                                        SavePortsToFile(specificPorts); // Save specific ports to file
                                    }
                                }
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
                                    continue; // Return to the beginning of the loop instead of exiting
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

                    // If no open ports were found, show a friendly prompt instead of an empty screen
                    if (!results.Any(r => r.IsTcpOpen || r.IsUdpOpen))
                    {
                        if (HandleNoOpenPorts(target))
                        {
                            continue; // retry or change IP handled, restart loop
                        }
                        else
                        {
                            break; // user chose to exit
                        }
                    }

                    PrintResults(results, target);

                    if (keepRunning) // Only ask if user didn't press Escape
                    {
                        int option = DisplayScanOptionsMenu();

                        switch (option)
                        {
                            case 1:
                                // Continue with same IP (it's already stored in lastUsedIp)
                                break;
                            case 2:
                                // Force new IP input by clearing lastUsedIp
                                lastUsedIp = string.Empty;
                                break;
                            case 3:
                                keepRunning = false;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
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

        static string GetTargetIp()
        {
            while (true)
            {
                if (!string.IsNullOrEmpty(lastUsedIp))
                {
                    
                    return lastUsedIp;
                    
                }

                Console.Write("Enter target IP or hostname: ");
                string? input = Console.ReadLine();

                if (input is not null && IsValidIpOrHostname(input))
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
            int maxRows = (Console.WindowHeight - 8); // Maximum rows visible per column
            int totalVisibleCapacity = maxRows * 3;

            // Smart toggle - show all results by default unless there are too many to display on one screen
            bool showAll = results.Count <= totalVisibleCapacity;

            while (true)
            {
                Console.Clear();
                string viewMode = showAll ? "all ports" : "open ports only";
                Console.WriteLine($"\nCurrently showing: {viewMode}");
                Console.WriteLine("Use Up/Down arrows to scroll line by line, Left/Right arrows to scroll page by page");
                Console.WriteLine("Space to toggle view, Enter to go back to the menu, Esc to exit, S to save results");

                // If any filtered results exist, show a quick explanation before the table
                if (showAll && results.Any(r => r.TcpFiltered || r.UdpFiltered))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Note: 'Filtered' means the port didn't respond (timeout). A firewall may be silently blocking traffic, so the port can't be confirmed open or closed.");
                    Console.ResetColor();
                }

                PrintTableHeader(ip);

                var filteredResults = showAll ? results : results.FindAll(r => r.IsTcpOpen || r.IsUdpOpen);

                // If current view has no results (e.g., open-only but none visible), offer to toggle or exit
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

                // Relative positions inside a column
                int relPortX = 0;
                int relTcpX = relPortX + PortFieldWidth + SpacePortTcp;
                int relUdpX = relTcpX + TcpFieldWidth + SpaceTcpUdp;

                // Column layout using cursor positioning, with tight minimal width
                int minColumnWidth = relUdpX + UdpFieldWidth;
                int colWidth = minColumnWidth; // keep columns tight (avoid large empty space)
                int col1X = 0;
                int col2X = Math.Min(Console.BufferWidth - 1, col1X + colWidth + SpaceTcpUdp);
                int col3X = Math.Min(Console.BufferWidth - 1, col2X + colWidth + SpaceUdpPort);

                // Draw column headers aligned to the same relative positions as values
                int headerTop = Math.Min(Console.CursorTop, Math.Max(0, Console.BufferHeight - 1));
                Console.SetCursorPosition(Math.Min(col1X + relPortX, Console.BufferWidth - 1), headerTop);
                Console.Write("Port");
                Console.SetCursorPosition(Math.Min(col1X + relTcpX, Console.BufferWidth - 1), headerTop);
                Console.Write("TCP");
                Console.SetCursorPosition(Math.Min(col1X + relUdpX, Console.BufferWidth - 1), headerTop);
                Console.Write("UDP");

                Console.SetCursorPosition(Math.Min(col2X + relPortX, Console.BufferWidth - 1), headerTop);
                Console.Write("Port");
                Console.SetCursorPosition(Math.Min(col2X + relTcpX, Console.BufferWidth - 1), headerTop);
                Console.Write("TCP");
                Console.SetCursorPosition(Math.Min(col2X + relUdpX, Console.BufferWidth - 1), headerTop);
                Console.Write("UDP");

                Console.SetCursorPosition(Math.Min(col3X + relPortX, Console.BufferWidth - 1), headerTop);
                Console.Write("Port");
                Console.SetCursorPosition(Math.Min(col3X + relTcpX, Console.BufferWidth - 1), headerTop);
                Console.Write("TCP");
                Console.SetCursorPosition(Math.Min(col3X + relUdpX, Console.BufferWidth - 1), headerTop);
                Console.Write("UDP");

                Console.WriteLine();
                int clearLen = Math.Min(Console.WindowWidth - 1, col3X + colWidth);
                Console.WriteLine(new string('-', Math.Max(0, clearLen)));

                int bodyTop = Console.CursorTop;

                // Compute how many total rows are needed for the 3-column layout
                int totalRows = (int)Math.Ceiling(filteredResults.Count / 3.0);
                int visibleRows = Math.Min(maxRows, Math.Max(0, totalRows - currentLine));

                // Ensure buffer height can accommodate what we'll print (Windows only)
                int neededHeight = bodyTop + visibleRows + 1; // +1 for safety
                if (OperatingSystem.IsWindows() && neededHeight > Console.BufferHeight)
                {
                    try
                    {
                        Console.SetBufferSize(Console.BufferWidth, Math.Max(Console.WindowHeight, neededHeight));
                    }
                    catch { /* ignore if not supported */ }
                }

                // Clamp visibleRows to buffer height to avoid SetCursorPosition errors
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

                    // Clear the entire line area before writing cells
                    Console.SetCursorPosition(0, targetY);
                    Console.Write(new string(' ', Math.Max(0, clearLen)));

                    if (index1 < filteredResults.Count)
                    {
                        PrintCellAt(filteredResults[index1], col1X + relPortX, col1X + relTcpX, col1X + relUdpX, targetY);
                    }
                    if (index2 < filteredResults.Count)
                    {
                        PrintCellAt(filteredResults[index2], col2X + relPortX, col2X + relTcpX, col2X + relUdpX, targetY);
                    }
                    if (index3 < filteredResults.Count)
                    {
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
