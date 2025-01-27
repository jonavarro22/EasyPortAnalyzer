using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace EasyPortAnalyzer
{
    public class PortScanResult
    {
        public int Port { get; set; }
        public bool IsTcpOpen { get; set; }
        public bool IsUdpOpen { get; set; }
    }

    public static class PortScanner
    {
        public static async Task<List<PortScanResult>> ScanAsync(string target, int startPort, int endPort)
        {
            var results = new List<PortScanResult>();
            var tasks = new Task<PortScanResult>[endPort - startPort + 1];

            for (int port = startPort; port <= endPort; port++)
            {
                int currentPort = port;
                tasks[currentPort - startPort] = Task.Run(async () =>
                {
                    bool isTcpOpen = await IsTcpPortOpenAsync(target, currentPort);
                    bool isUdpOpen = await IsUdpPortOpenAsync(target, currentPort);

                    return new PortScanResult
                    {
                        Port = currentPort,
                        IsTcpOpen = isTcpOpen,
                        IsUdpOpen = isUdpOpen
                    };
                });
            }

            var scanResults = await Task.WhenAll(tasks);
            results.AddRange(scanResults);

            return results;
        }

        public static async Task<List<PortScanResult>> ScanSpecificPortsAsync(string target, List<int> ports)
        {
            var results = new List<PortScanResult>();
            var tasks = new Task<PortScanResult>[ports.Count];

            for (int i = 0; i < ports.Count; i++)
            {
                int currentPort = ports[i];
                tasks[i] = Task.Run(async () =>
                {
                    bool isTcpOpen = await IsTcpPortOpenAsync(target, currentPort);
                    bool isUdpOpen = await IsUdpPortOpenAsync(target, currentPort);

                    return new PortScanResult
                    {
                        Port = currentPort,
                        IsTcpOpen = isTcpOpen,
                        IsUdpOpen = isUdpOpen
                    };
                });
            }

            var scanResults = await Task.WhenAll(tasks);
            results.AddRange(scanResults);

            return results;
        }

        private static async Task<bool> IsTcpPortOpenAsync(string host, int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var connectTask = client.ConnectAsync(host, port);
                    var timeoutTask = Task.Delay(1000); // 1-second timeout

                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        return false; // TCP port is closed (timeout)
                    }

                    await connectTask; // Ensure any exceptions are observed
                    return true; // TCP port is open
                }
            }
            catch
            {
                return false; // TCP port is closed
            }
        }

        private static async Task<bool> IsUdpPortOpenAsync(string host, int port)
        {
            try
            {
                using (var udpClient = new UdpClient())
                {
                    udpClient.Connect(host, port);
                    byte[] testBytes = System.Text.Encoding.ASCII.GetBytes("test");
                    await udpClient.SendAsync(testBytes, testBytes.Length);

                    udpClient.Client.ReceiveTimeout = 1000; // 1-second timeout
                    var remoteEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
                    var receiveTask = udpClient.ReceiveAsync();
                    var timeoutTask = Task.Delay(1000); // 1-second timeout

                    var completedTask = await Task.WhenAny(receiveTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        return false; // UDP port is closed (timeout)
                    }

                    await receiveTask; // Ensure any exceptions are observed
                    return true; // UDP port is open
                }
            }
            catch
            {
                return false; // UDP port is closed
            }
        }
    }
}
