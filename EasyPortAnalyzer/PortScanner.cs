using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
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

        public static async Task<List<PortScanResult>> ScanAsync(
            string target,
            int startPort,
            int endPort,
            int timeoutMs = 1000,
            int maxConcurrency = 200,
            CancellationToken token = default)
        {
            var ports = Enumerable.Range(startPort, endPort - startPort + 1);
            return await ScanPortsAsync(target, ports, timeoutMs, maxConcurrency, token);
        }

        public static async Task<List<PortScanResult>> ScanSpecificPortsAsync(
            string target,
            List<int> ports,
            int timeoutMs = 1000,
            int maxConcurrency = 200,
            CancellationToken token = default)
        {
            return await ScanPortsAsync(target, ports, timeoutMs, maxConcurrency, token);
        }

        private static async Task<List<PortScanResult>> ScanPortsAsync(
            string target,
            IEnumerable<int> ports,
            int timeoutMs,
            int maxConcurrency,
            CancellationToken token)
        {
            var results = new List<PortScanResult>();
            var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = new List<Task>();

            foreach (var port in ports)
            {
                token.ThrowIfCancellationRequested();
                await semaphore.WaitAsync(token);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var tcpState = await GetTcpStateAsync(target, port, timeoutMs, token);
                        var udpState = await GetUdpStateAsync(target, port, timeoutMs, token);

                        lock (results)
                        {
                            results.Add(new PortScanResult
                            {
                                Port = port,
                                IsTcpOpen = tcpState == PortState.Open,
                                TcpFiltered = tcpState == PortState.Filtered,
                                IsUdpOpen = udpState == PortState.Open,
                                UdpFiltered = udpState == PortState.Filtered
                            });
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignore, scan was cancelled
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, token));
            }

            await Task.WhenAll(tasks);
            return results.OrderBy(r => r.Port).ToList();
        }

        private static async Task<PortState> GetTcpStateAsync(
            string host,
            int port,
            int timeoutMs,
            CancellationToken token)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(timeoutMs, token);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                if (completedTask == timeoutTask)
                    return PortState.Filtered;

                await connectTask; // Ensure exceptions are observed
                return PortState.Open;
            }
            catch (SocketException se)
            {
                return se.SocketErrorCode == SocketError.ConnectionRefused
                    ? PortState.Closed
                    : PortState.Filtered;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return PortState.Filtered;
            }
        }

        private static async Task<PortState> GetUdpStateAsync(
            string host,
            int port,
            int timeoutMs,
            CancellationToken token)
        {
            try
            {
                using var udpClient = new UdpClient();
                udpClient.Connect(host, port);

                byte[] testBytes = System.Text.Encoding.ASCII.GetBytes("test");
                await udpClient.SendAsync(testBytes, testBytes.Length);

                var receiveTask = udpClient.ReceiveAsync();
                var timeoutTask = Task.Delay(timeoutMs, token);

                var completedTask = await Task.WhenAny(receiveTask, timeoutTask);
                if (completedTask == timeoutTask)
                    return PortState.Filtered;

                await receiveTask; // Response received
                return PortState.Open;
            }
            catch (SocketException se)
            {
                return se.SocketErrorCode == SocketError.ConnectionReset
                    ? PortState.Closed
                    : PortState.Filtered;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return PortState.Filtered;
            }
        }
    }
}