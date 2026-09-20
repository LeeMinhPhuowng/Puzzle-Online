using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PuzzleOnline.Network
{
    public sealed class RealtimeClient : MonoBehaviour
    {
        public event Action<NetMessage> MessageReceived;

        public bool IsConnected { get; private set; }
        public bool IsConnecting { get; private set; }
        public string ConnectionLabel { get; private set; } = "Chưa kết nối";

        private readonly ConcurrentQueue<NetMessage> _inbox = new ConcurrentQueue<NetMessage>();
        private readonly object _writerLock = new object();
        private TcpClient _tcp;
        private StreamReader _reader;
        private StreamWriter _writer;
        private CancellationTokenSource _lifetime;

        public async void Connect(string host, int port)
        {
            if (IsConnected || IsConnecting) return;
            DisconnectInternal(false);
            IsConnecting = true;
            ConnectionLabel = "Đang kết nối...";
            _lifetime = new CancellationTokenSource();

            try
            {
                var tcp = new TcpClient { NoDelay = true };
                var connectTask = tcp.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(6), _lifetime.Token);
                if (await Task.WhenAny(connectTask, timeoutTask) != connectTask)
                    throw new TimeoutException("Server không phản hồi trong 6 giây.");
                await connectTask;

                _tcp = tcp;
                var stream = tcp.GetStream();
                _reader = new StreamReader(stream, new UTF8Encoding(false));
                _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                IsConnected = true;
                IsConnecting = false;
                ConnectionLabel = "Đã kết nối";
                _inbox.Enqueue(new NetMessage("CONNECTED"));
                _ = ReadLoop(_lifetime.Token);
            }
            catch (Exception exception)
            {
                IsConnecting = false;
                IsConnected = false;
                ConnectionLabel = "Kết nối thất bại";
                _inbox.Enqueue(new NetMessage("CONNECTION_FAILED",
                    new Dictionary<string, string> { ["message"] = exception.Message }));
                DisconnectInternal(false);
            }
        }

        public void Send(string type, params (string Key, object Value)[] fields)
        {
            if (!IsConnected || _writer == null) return;
            var line = WireProtocol.Encode(type, fields);
            try
            {
                lock (_writerLock) _writer.WriteLine(line);
            }
            catch (Exception exception)
            {
                _inbox.Enqueue(new NetMessage("DISCONNECTED",
                    new Dictionary<string, string> { ["message"] = exception.Message }));
                DisconnectInternal(false);
            }
        }

        public void Disconnect()
        {
            DisconnectInternal(true);
        }

        private async Task ReadLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _reader != null)
                {
                    var line = await _reader.ReadLineAsync();
                    if (line == null) break;
                    if (line.Length > 0) _inbox.Enqueue(WireProtocol.Decode(line));
                }
            }
            catch (Exception exception)
            {
                if (!token.IsCancellationRequested)
                    _inbox.Enqueue(new NetMessage("NETWORK_ERROR",
                        new Dictionary<string, string> { ["message"] = exception.Message }));
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    _inbox.Enqueue(new NetMessage("DISCONNECTED"));
                DisconnectInternal(false);
            }
        }

        private void Update()
        {
            var budget = 200;
            while (budget-- > 0 && _inbox.TryDequeue(out var message))
                MessageReceived?.Invoke(message);
        }

        private void OnApplicationQuit()
        {
            DisconnectInternal(false);
        }

        private void OnDestroy()
        {
            DisconnectInternal(false);
        }

        private void DisconnectInternal(bool clean)
        {
            try { _lifetime?.Cancel(); } catch { }
            try { _lifetime?.Dispose(); } catch { }
            _lifetime = null;

            try { _reader?.Dispose(); } catch { }
            _reader = null;

            try { _writer?.Dispose(); } catch { }
            _writer = null;

            try { _tcp?.Close(); } catch { }
            _tcp = null;

            IsConnected = false;
            IsConnecting = false;
            ConnectionLabel = clean ? "Đã ngắt kết nối" : "Mất kết nối";
        }
    }
}
