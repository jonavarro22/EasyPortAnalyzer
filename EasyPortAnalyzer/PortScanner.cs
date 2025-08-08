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
        public bool TcpFiltered { get; set; }
        public bool UdpFiltered { get; set; }
    }

    public static class PortScanner
    {
        private enum PortState { Open, Closed, Filtered }

        public static async Task<List<PortScanResult>> ScanAsync(string target, int startPort, int endPort)
        {
            var results = new List<PortScanResult>();
            var tasks = new Task<PortScanResult>[endPort - startPort + 1];

            for (int port = startPort; port <= endPort; port++)
            {
                int currentPort = port;
                tasks[currentPort - startPort] = Task.Run(async () =>
                {
                    var tcpState = await GetTcpStateAsync(target, currentPort);
                    var udpState = await GetUdpStateAsync(target, currentPort);

                    return new PortScanResult
                    {
                        Port = currentPort,
                        IsTcpOpen = tcpState == PortState.Open,
                        TcpFiltered = tcpState == PortState.Filtered,
                        IsUdpOpen = udpState == PortState.Open,
                        UdpFiltered = udpState == PortState.Filtered
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
                    var tcpState = await GetTcpStateAsync(target, currentPort);
                    var udpState = await GetUdpStateAsync(target, currentPort);

                    return new PortScanResult
                    {
                        Port = currentPort,
                        IsTcpOpen = tcpState == PortState.Open,
                        TcpFiltered = tcpState == PortState.Filtered,
                        IsUdpOpen = udpState == PortState.Open,
                        UdpFiltered = udpState == PortState.Filtered
                    };
                });
            }

            var scanResults = await Task.WhenAll(tasks);
            results.AddRange(scanResults);

            return results;
        }

        private static async Task<PortState> GetTcpStateAsync(string host, int port)
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
                        return PortState.Filtered; // Timeout likely means filtered or silently dropped
                    }

                    await connectTask; // Ensure any exceptions are observed
                    return PortState.Open; // TCP port is open
                }
            }
            catch (SocketException se)
            {
                // ConnectionRefused => closed; many other errors indicate filtered/unreachable
                return se.SocketErrorCode == SocketError.ConnectionRefused ? PortState.Closed : PortState.Filtered;
            }
            catch
            {
                return PortState.Filtered;
            }
        }

        private static async Task<PortState> GetUdpStateAsync(string host, int port)
        {
            try
            {
                using (var udpClient = new UdpClient())
                {
                    udpClient.Connect(host, port);
                    byte[] testBytes = System.Text.Encoding.ASCII.GetBytes("test");
                    await udpClient.SendAsync(testBytes, testBytes.Length);

                    udpClient.Client.ReceiveTimeout = 1000; // 1-second timeout
                    var receiveTask = udpClient.ReceiveAsync();
                    var timeoutTask = Task.Delay(1000); // 1-second timeout

                    var completedTask = await Task.WhenAny(receiveTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        return PortState.Filtered; // No response: could be open or filtered; mark as filtered
                    }

                    await receiveTask; // Some response received
                    return PortState.Open;
                }
            }
            catch (SocketException se)
            {
                // On Windows, ICMP Port Unreachable maps to ConnectionReset
                return se.SocketErrorCode == SocketError.ConnectionReset ? PortState.Closed : PortState.Filtered;
            }
            catch
            {
                return PortState.Filtered;
            }
        }
    }
}
