using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SephiriaCompanion
{
    public sealed class LocalServer : IDisposable
    {
        public const int DefaultPort = 27310;
        const int HeaderLimit = 8192;
        const int CommandLimit = 1024;
        const int QueueLimit = 8;
        static readonly Regex ImageName = new Regex(@"^(map|icon)--?[0-9]+\.png$", RegexOptions.Compiled);
        static readonly Regex WebName = new Regex(@"^[a-z][a-z0-9-]*\.js$", RegexOptions.Compiled);
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        readonly TcpListener listener;
        readonly Thread thread;
        readonly Func<string, byte[]> resource;
        readonly ConcurrentQueue<string> commands = new ConcurrentQueue<string>();
        readonly ConcurrentDictionary<string, byte[]> channels = new ConcurrentDictionary<string, byte[]>();
        readonly EventHub events = new EventHub();
        readonly object channelGate = new object();
        readonly SemaphoreSlim workers = new SemaphoreSlim(8, 8);
        readonly string key;
        volatile bool running = true;
        int pending;

        public string Session { get; } = Guid.NewGuid().ToString("N");
        public int Port { get; }
        public bool HasSubscribers => events.HasSubscribers;

        public LocalServer(Func<string, byte[]> resource, int port = DefaultPort)
        {
            this.resource = resource;
            var bytes = new byte[24];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            key = Convert.ToBase64String(bytes);
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start(8);
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            thread = new Thread(Accept) { IsBackground = true, Name = "SephiriaCompanion.HTTP" };
            thread.Start();
        }

        public void Publish(string json) => Publish("state", json);

        public int Publish(string channel, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            lock (channelGate)
            {
                channels[channel] = bytes;
                events.Publish(channel, channel, json);
            }
            return bytes.Length;
        }

        public void AssetReady(string path) => events.Publish("asset:" + path, "asset", JsonProtocol.Write(new { path }));

        public bool TryTake(out string json)
        {
            if (!commands.TryDequeue(out json)) return false;
            Interlocked.Decrement(ref pending);
            return true;
        }

        void Accept()
        {
            while (running)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch (SocketException) { if (!running) return; continue; }
                catch (ObjectDisposedException) { return; }
                if (!workers.Wait(100)) { client.Close(); continue; }
                ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
            }
        }

        void HandleClient(TcpClient client)
        {
            try
            {
                using (client)
                {
                    client.ReceiveTimeout = client.SendTimeout = 2500;
                    // Buffered reads prevent a socket syscall for every header byte.
                    // TcpClient owns the stream lifetime, including early rejections.
                    var stream = new BufferedStream(client.GetStream(), 8192);
                    Serve(stream);
                    client.Client.Shutdown(SocketShutdown.Send);
                    // Drain bytes already available without adding a delay to every poll.
                    if (client.Available > 0)
                    {
                        var drain = new byte[Math.Min(client.Available, 16384)];
                        client.GetStream().Read(drain, 0, drain.Length);
                    }
                }
            }
            catch (IOException) { } // The WebView can cancel requests when switching/closing screens.
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            catch (DecoderFallbackException) { }
            finally { workers.Release(); }
        }

        void Serve(Stream stream)
        {
            var request = ReadHeaders(stream);
            if (request == null) return;
            if (!Trusted(request.Headers)) { Reply(stream, 403, "{}"); return; }
            if (request.Headers.ContainsKey("Transfer-Encoding")) { Reply(stream, 400, "{}"); return; }
            if (request.Method == "GET") ServeGet(stream, request);
            else if (request.Method == "POST" && request.Path == "/command") ServeCommand(stream, request.Headers);
            else Reply(stream, 405, "{}");
        }

        sealed class Request
        {
            public string Method, Path;
            public Dictionary<string, string> Headers;
        }

        static Request ReadHeaders(Stream stream)
        {
            var bytes = new byte[HeaderLimit];
            int length = 0;
            while (length < bytes.Length)
            {
                int value = stream.ReadByte();
                if (value < 0) return null;
                bytes[length++] = (byte)value;
                if (length >= 4 && bytes[length - 4] == 13 && bytes[length - 3] == 10 &&
                    bytes[length - 2] == 13 && bytes[length - 1] == 10) break;
            }
            if (length == HeaderLimit) { Reply(stream, 431, "{}"); return null; }
            var lines = Encoding.ASCII.GetString(bytes, 0, length).Split(new[] { "\r\n" }, StringSplitOptions.None);
            var first = lines[0].Split(' ');
            if (first.Length != 3) { Reply(stream, 400, "{}"); return null; }
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 1; index < lines.Length; index++)
            {
                int colon = lines[index].IndexOf(':');
                if (colon > 0) headers[lines[index].Substring(0, colon).Trim()] = lines[index].Substring(colon + 1).Trim();
            }
            return new Request { Method = first[0], Path = first[1].Split('?')[0], Headers = headers };
        }

        bool Trusted(Dictionary<string, string> headers)
        {
            if (!headers.TryGetValue("Host", out var host) || host != "127.0.0.1:" + Port && host != "localhost:" + Port) return false;
            if (headers.TryGetValue("Origin", out var origin) && origin != "http://" + host) return false;
            return !headers.TryGetValue("Sec-Fetch-Site", out var site) || site != "cross-site";
        }

        bool Authorized(Dictionary<string, string> headers) => headers.TryGetValue("X-Companion-Key", out var token) && token == key;

        void ServeGet(Stream stream, Request request)
        {
            string path = request.Path;
            if (path == "/events")
            {
                if (!Authorized(request.Headers)) { Reply(stream, 403, "{}"); return; }
                ServeEvents(stream);
                return;
            }
            if (path == "/health")
            {
                Reply(stream, 200, "{\"app\":\"SephiriaCompanion\",\"version\":\"" + JsonProtocol.Version + "\",\"protocol\":3}");
                return;
            }
            if (path == "/session")
            {
                Reply(stream, 200, "{\"key\":\"" + key + "\",\"session\":\"" + Session + "\"}");
                return;
            }
            if (path == "/state" || path == "/inventory" || path == "/floor-map" || path == "/minimap" || path == "/hud-content")
            {
                if (!Authorized(request.Headers)) { Reply(stream, 403, "{}"); return; }
                if (channels.TryGetValue(path.Substring(1), out var bytes)) Reply(stream, 200, bytes, "application/json; charset=utf-8");
                else Reply(stream, 200, "{}");
                return;
            }
            string file = path == "/" ? "index.html" : path.TrimStart('/');
            bool image = ImageName.IsMatch(file);
            if (file != "index.html" && file != "style.css" && !WebName.IsMatch(file) && !image)
            {
                Reply(stream, 404, "{}");
                return;
            }
            var data = resource(file);
            if (data == null) { Reply(stream, 404, "{}"); return; }
            string contentType = image ? "image/png" : file.EndsWith(".js") ? "application/javascript; charset=utf-8" :
                file.EndsWith(".css") ? "text/css; charset=utf-8" : "text/html; charset=utf-8";
            Reply(stream, 200, data, contentType, image);
        }

        void ServeEvents(Stream stream)
        {
            EventHub.Subscription subscription;
            // Replay and registration are atomic with publication. An older replay
            // must never overwrite a newer event queued during connection setup.
            lock (channelGate)
            {
                subscription = events.Subscribe();
                if (subscription != null) foreach (var channel in channels)
                    subscription.Push(channel.Key, EventHub.Frame(channel.Key, Encoding.UTF8.GetString(channel.Value)));
            }
            if (subscription == null) { Reply(stream, 429, "{}"); return; }
            try
            {
                var header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream; charset=utf-8\r\nCache-Control: no-store\r\nConnection: close\r\nX-Content-Type-Options: nosniff\r\n\r\n");
                stream.Write(header, 0, header.Length);
                stream.Flush();
                while (running)
                {
                    var frames = subscription.Wait();
                    if (frames == null) return;
                    if (frames.Length == 0)
                    {
                        var heartbeat = EventHub.Heartbeat;
                        stream.Write(heartbeat, 0, heartbeat.Length);
                    }
                    else foreach (var frame in frames) stream.Write(frame, 0, frame.Length);
                    stream.Flush();
                }
            }
            finally { events.Remove(subscription); }
        }

        void ServeCommand(Stream stream, Dictionary<string, string> headers)
        {
            if (!Authorized(headers)) { Reply(stream, 403, "{}"); return; }
            if (!headers.TryGetValue("Content-Type", out var type) || !type.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                Reply(stream, 415, "{}");
                return;
            }
            if (!headers.TryGetValue("Content-Length", out var text) || !int.TryParse(text, out int length) || length < 2 || length > CommandLimit)
            {
                Reply(stream, 413, "{}");
                return;
            }
            var body = new byte[length];
            int received = 0;
            while (received < body.Length)
            {
                int count = stream.Read(body, received, body.Length - received);
                if (count == 0) return;
                received += count;
            }
            string json = StrictUtf8.GetString(body);
            if (Interlocked.Increment(ref pending) > QueueLimit)
            {
                Interlocked.Decrement(ref pending);
                Reply(stream, 429, "{}");
                return;
            }
            commands.Enqueue(json);
            Reply(stream, 202, "{\"queued\":true}");
        }

        static void Reply(Stream stream, int code, string text) => Reply(stream, code, Encoding.UTF8.GetBytes(text), "application/json; charset=utf-8");

        static void Reply(Stream stream, int code, byte[] body, string type, bool image = false)
        {
            string reason = code == 200 ? "OK" : code == 202 ? "Accepted" : "Request rejected";
            var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + code + " " + reason + "\r\nContent-Type: " + type +
                "\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\nCache-Control: " +
                (image ? "private, max-age=3600, immutable" : "no-store") + "\r\nX-Content-Type-Options: nosniff" +
                "\r\nContent-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; connect-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'none'" +
                "\r\nReferrer-Policy: no-referrer\r\n\r\n");
            stream.Write(header, 0, header.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        public void Dispose()
        {
            running = false;
            events.Dispose();
            listener.Stop();
            if (Thread.CurrentThread != thread) thread.Join(1000);
            while (commands.TryDequeue(out _)) Interlocked.Decrement(ref pending);
        }
    }
}
