using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Raven.Abstractions.Connection;
using Raven.Abstractions.Extensions;
using Raven.Abstractions.Json;
using Raven.Abstractions.Logging;
using Raven.Abstractions.Util;
using Raven.Json.Linq;

namespace Raven.Client.Changes
{
    public abstract class RemoteChangesClientBase<TChangesApi, TConnectionState, TConventions> : IDisposable, IConnectableChanges<TChangesApi>
                                where TConnectionState : class, IChangesConnectionState
                                where TChangesApi : class, IConnectableChanges
                                where TConventions : ConventionBase
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(RemoteChangesClientBase<TChangesApi, TConnectionState, TConventions>));

        private readonly string url;
        private readonly OperationCredentials credentials;
        private readonly Action onDispose;

        private static int connectionCounter;
        private readonly string id;

        // This is the StateCounters, it is not related to the counters database
        protected readonly AtomicDictionary<TConnectionState> Counters = new AtomicDictionary<TConnectionState>(StringComparer.OrdinalIgnoreCase);

        protected RemoteChangesClientBase(
            string url,
            string apiKey,
            ICredentials credentials,
            TConventions conventions,
            Action onDispose)
        {
            // Precondition
            var api = this as TChangesApi;
            if (api == null)
                throw new InvalidCastException(string.Format("The derived class does not implements {0}. Make sure the {0} interface is implemented by this class.", typeof(TChangesApi).Name));

            ConnectionStatusChanged = LogOnConnectionStatusChanged;

            id = Interlocked.Increment(ref connectionCounter) + "/" + Base62Util.Base62Random();

            this.url = url;
            this.credentials = new OperationCredentials(apiKey, credentials);
            this.onDispose = onDispose;
            Conventions = conventions;
            webSocket = new ClientWebSocket();

            ConnectionTask = EstablishConnection()
                .ObserveException()
                .ContinueWith(task =>
                {
                    task.AssertNotFailed();

                    Task.Run((Func<Task>)Receive);

                    return this as TChangesApi;
                });
        }

        private async Task EstablishConnection()
        {
            if (disposed)
                return;

            var uri = new Uri(url.Replace("http://", "ws://").Replace(".fiddler", "") + "/changes");
            logger.Info("Trying to connect to {0}", uri);
            await webSocket.ConnectAsync(uri, CancellationToken.None);
        }

        protected TConventions Conventions { get; private set; }

        public bool Connected { get; private set; }
        public event EventHandler ConnectionStatusChanged;

        private void LogOnConnectionStatusChanged(object sender, EventArgs eventArgs)
        {
            logger.Info("Connection ({1}) status changed, new status: {0}", Connected, url);
        }


        public Task<TChangesApi> ConnectionTask { get; private set; }

        public void WaitForAllPendingSubscriptions()
        {
            foreach (var kvp in Counters)
            {
                kvp.Value.Task.Wait();
            }
        }

        static UTF8Encoding encoder = new UTF8Encoding();


        private async Task Receive()
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    ms.SetLength(4096);
                    while (webSocket.State == WebSocketState.Open)
                    {
                        ms.Position = 0;
                        ArraySegment<byte> bytes;
                        ms.TryGetBuffer(out bytes);
                        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(bytes.Array, (int)ms.Position, (int)(ms.Capacity - ms.Position)),
                            disposedToken.Token);
                        ms.SetLength(ms.Length + result.Count);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                            return;
                        }
                        if (ms.Capacity - ms.Length < 1024)
                        {
                            ms.Capacity += 4096;
                        }
                        while (result.EndOfMessage == false)
                        {
                            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, length, receiveChunkSize - length), CancellationToken.None);
                            length += result.Count;
                        }

                        RavenJObject ravenJObject;
                        using (var stream = new MemoryStream(buffer, 0, length))
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        using (var jsonReader = new RavenJsonTextReader(reader))
                        {
                            ravenJObject = RavenJObject.Load(jsonReader);
                        }

                        var value = ravenJObject.Value<RavenJObject>("Value");
                        var type = ravenJObject.Value<string>("Type");
                        if (logger.IsDebugEnabled)
                            logger.Debug("Got notification from {0} id {1} of type {2}", url, id, ravenJObject.ToString());

                        switch (type)
                        {
                            case "Disconnect":
                                webSocket.Dispose();
                                // TODO: RenewConnection();
                                break;
                            case "Initialized":
                            case "Heartbeat":
                                throw new NotSupportedException(); // Should be deleted
                            default:
                                NotifySubscribers(type, value, Counters.Snapshot);
                                break;

                        }
                    }
                }
            }
            catch (WebSocketException ex)
            {
                logger.DebugException("Failed to receive a message, client was probably disconnected", ex);
            }
        }

        protected async Task Send(string command, string commandParameter)
        {
            logger.Info("Sending command {0} - {1} to {2} with id {3}", command, commandParameter, url, id);

            var ravenJObject = new RavenJObject
            {
                ["Command"] = command,
                ["Param"] = commandParameter
            };
            var stream = new MemoryStream();
            ravenJObject.WriteTo(stream);
            await webSocket.SendAsync(new ArraySegment<byte>(stream.ToArray(), 0, (int)stream.Length), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
        }

        private readonly CancellationTokenSource disposedToken = new CancellationTokenSource();

        public void Dispose()
        {
            if (disposed)
                return;

            DisposeAsync().Wait();
        }

        private volatile bool disposed;
        private readonly ClientWebSocket webSocket;

        public Task DisposeAsync()
        {
            if (disposed)
                return new CompletedTask();
            disposed = true;

            disposedToken.Cancel();
            onDispose();

            return webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the client", CancellationToken.None)
                .ContinueWith(_ =>
                {
                    try
                    {
                        webSocket?.Dispose();
                    }
                    catch (Exception e)
                    {
                        logger.ErrorException("Got error from server connection for " + url + " on id " + id, e);

                    }
                });
        }


        public virtual void OnError(Exception error)
        {
            logger.ErrorException("Got error from server connection for " + url + " on id " + id, error);

            // TODO: RenewConnection();
        }

        protected Task AfterConnection(Func<Task> action)
        {
            return ConnectionTask.ContinueWith(task =>
            {
                task.AssertNotFailed();
                return action();
            })
            .Unwrap();
        }

        protected abstract Task SubscribeOnServer();
        protected abstract void NotifySubscribers(string type, RavenJObject value, IEnumerable<KeyValuePair<string, TConnectionState>> connections);

        public virtual void OnCompleted()
        { }

        protected TConnectionState GetOrAddConnectionState(string name, string watchCommand, string unwatchCommand, Action afterConnection, Action beforeDisconnect, string value)
        {
            var counter = Counters.GetOrAdd(name, s =>
            {
                Action onZero = () =>
                {
                    beforeDisconnect();
                    Send(unwatchCommand, value);
                    Counters.Remove(name);
                };

                Func<TConnectionState, Task> ensureConnection = existingConnectionState =>
                {
                    TConnectionState _;
                    if (Counters.TryGetValue(name, out _))
                        return _.Task;

                    Counters.GetOrAdd(name, x => existingConnectionState);

                    return AfterConnection(() =>
                    {
                        afterConnection();
                        return Send(watchCommand, value);
                    });
                };

                var counterSubscriptionTask = AfterConnection(() =>
                {
                    afterConnection();
                    return Send(watchCommand, value);
                });

                return CreateTConnectionState(onZero, ensureConnection, counterSubscriptionTask);
            });

            return counter;
        }

        private static TConnectionState CreateTConnectionState(params object[] args)
        {
            return (TConnectionState)Activator.CreateInstance(typeof(TConnectionState), args);
        }
    }
}
