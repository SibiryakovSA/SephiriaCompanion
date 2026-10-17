using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SephiriaCompanion
{
    // One persistent response per screen. Slow consumers receive the latest value
    // of each channel, so an idle/paused WebView cannot accumulate snapshots.
    internal sealed class EventHub : IDisposable
    {
        public static readonly byte[] Heartbeat = Frame("heartbeat", "{}");
        internal sealed class Subscription
        {
            readonly object gate = new object();
            readonly Dictionary<string, byte[]> pending = new Dictionary<string, byte[]>();
            bool closed;

            public void Push(string identity, byte[] frame)
            {
                lock (gate)
                {
                    if (closed) return;
                    pending[identity] = frame;
                    Monitor.Pulse(gate);
                }
            }

            public byte[][] Wait()
            {
                lock (gate)
                {
                    if (pending.Count == 0 && !closed) Monitor.Wait(gate, 1000);
                    if (closed) return null;
                    var result = new byte[pending.Count][];
                    pending.Values.CopyTo(result, 0);
                    pending.Clear();
                    return result;
                }
            }

            public void Close()
            {
                lock (gate) { closed = true; pending.Clear(); Monitor.PulseAll(gate); }
            }
        }

        readonly object gate = new object();
        readonly HashSet<Subscription> subscribers = new HashSet<Subscription>();
        bool closed;
        public bool HasSubscribers { get { lock (gate) return subscribers.Count > 0; } }

        public Subscription Subscribe()
        {
            lock (gate)
            {
                if (closed || subscribers.Count >= 2) return null;
                var subscription = new Subscription();
                subscribers.Add(subscription);
                return subscription;
            }
        }

        public static byte[] Frame(string channel, string json) => Encoding.UTF8.GetBytes("event: " + channel + "\ndata: " + json + "\n\n");

        public void Publish(string identity, string channel, string json)
        {
            lock (gate)
            {
                if (subscribers.Count == 0) return;
                var frame = Frame(channel, json);
                foreach (var subscriber in subscribers) subscriber.Push(identity, frame);
            }
        }

        public void Remove(Subscription subscription)
        {
            lock (gate) { subscribers.Remove(subscription); subscription.Close(); }
        }

        public void Dispose()
        {
            lock (gate)
            {
                closed = true;
                foreach (var subscriber in subscribers) subscriber.Close();
                subscribers.Clear();
            }
        }
    }
}
