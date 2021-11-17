using MingXu.Socket.MessageType;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MingXu.Socket
{
    public static class Utility
    {
        public static int ToInt(this char c) => int.Parse(c.ToString());
    }

    public class IO
    {
        public State state { get; private set; } = State.Opening;

        private ClientWebSocket socket;

        private Queue<SocketEvent> receivedMessages;
        private InitialConnectPayload SocketDetail;
        private static Queue<DebugLogger> Log;
        public IO()
        {
            Log = new Queue<DebugLogger>();
            AddToLog("Initialized.");
            receivedMessages = new Queue<SocketEvent>();

            socket = new ClientWebSocket();
        }

        #region SocketIO Messages
        private void OnNewMessage(string content)
        {
            EngineIOMessage engineMessage = MessageFactory.Create(content);
            switch (engineMessage.engine_io_type_id)
            {
                case 0: // initial connect
                    OnConnected((InitialConnectPayload)engineMessage);
                    break;
                case 1: // disonnect
                    OnDisconnected();
                    break;
                case 2: // ping
                    OnPing((Ping)engineMessage);
                    break;
                case 4: // socket io events
                    OnSocketIOEvent((Message)engineMessage);
                    break;
                default:
                    AddToLog("Unhandled type " + engineMessage.engine_io_type_id, DebugLogType.Error);
                    break;
            }
        }

        #region OnSocketMessages
        private void OnPing(Ping ping)
        {
            AddToLog("Received Ping.");
            _ = Send(new Pong(ping.payload));
        }
        private void OnConnected(InitialConnectPayload i)
        {
            AddToLog("Connected to server.");
            AddNewMessage(new SocketEvent("connect", ""));
            state = State.Open;
            SocketDetail = i;
            _ = AddNameSpace();
        }
        private void OnDisconnected()
        {
            AddToLog("Disconnected from server.");
            AddNewMessage(new SocketEvent("disconnect", ""));
            Dispose();
        }
        private void OnSocketIOEvent(Message msg)
        {
            /*AddToLog(
                "Received message of type " + msg.type_id.ToString() + " in the namespace " + msg.@namespace
                + " with the data: " + (msg.data ?? "")
            );*/// Dev mode
            if (msg.ack > 0)
            {
                _ = Send(new Ack
                {
                    ack = msg.ack,
                    @namespace = msg.@namespace
                });
            }
            switch (msg.type_id)
            {
                case 0: // added to namespace
                    OnNewNamespace((ConnectedToNamespace)msg);
                    break;
                case 1: // removed from namespace
                    OnRemovedNamespace((DisconnectedFromNamespace)msg);
                    break;
                case 2: // "normal" event
                    OnNewMessage((SocketEvent)msg);
                    break;
                default:
                    AddToLog("Unknown message type: " + msg.type_id, DebugLogType.Error);
                    break;
            }
        }

        #region Socket Events
        private void OnNewNamespace(ConnectedToNamespace m)
        {
            AddToLog("Now accessing namespace " + (m.@namespace == string.Empty ? "/" : m.@namespace));
        }
        private void OnRemovedNamespace(DisconnectedFromNamespace m)
        {
            AddToLog("Lost access to namespace " + (m.@namespace == string.Empty ? "/" : m.@namespace));
            if (m.@namespace == "" || m.@namespace == "/") OnDisconnected();
        }
        private void OnNewMessage(SocketEvent m)
        {
            AddNewMessage(m);
        }

        #endregion

        #endregion

        private void AddNewMessage(SocketEvent e)
        {
            receivedMessages.Enqueue(e);
        }
        public List<SocketEvent> GetMessages()
        {
            List<SocketEvent> m = new List<SocketEvent>(receivedMessages);
            receivedMessages.Clear();
            return m;
        }
        #endregion

        #region Log
        public static List<DebugLogger> GetLog()
        {
            List<DebugLogger> m = new List<DebugLogger>(Log);
            Log.Clear();
            return m;
        }

        public static void AddToLog(string content, DebugLogType type = DebugLogType.Message)
        {
            string trace = Environment.StackTrace; // trace includes trace call and this function call
            Log.Enqueue(new DebugLogger(content + "\n" + trace.Substring(trace.IndexOf("at", trace.IndexOf("at", trace.IndexOf("at") + 2) + 2) - 1), type));
        }
        #endregion

        #region Network Stuff - DONT TOUCH
        public void Dispose()
        {
            socket.Dispose();
            state = State.Closed;
        }

        public async Task ConnectAsync(UriBuilder uri)
        {
            if (uri.Path == "/" && uri.Query == String.Empty)
            {
                uri.Path = "/socket.io/";
                uri.Query = "EIO=4&transport=websocket";
            }
            try
            {
                await socket.ConnectAsync(uri.Uri, CancellationToken.None);
                state = State.Open;
                ReceiveInBackground();
            }
            catch (Exception e)
            {
                AddToLog(e.Message + ": " + e.StackTrace, DebugLogType.Error);
                state = State.Closed;
                Dispose();
            }
        }

        public async Task AddNameSpace(string nsp = "")
        {
            RequestAccessToNamespace c = new RequestAccessToNamespace();
            c.@namespace = nsp;
            c.Token = "MaxMaxMaxMax";
            c.SerializeDataObject();
            await Send(c);
        }

        public async Task Send(EngineIOMessage m, WebSocketMessageType type = WebSocketMessageType.Text)
        {
            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(m.ToString())), type, true, CancellationToken.None);
        }

        private async void ReceiveInBackground()
        {
            var buffer = new ArraySegment<byte>(new byte[2048]);
            while (state == State.Open)
            {
                WebSocketReceiveResult result;
                var ms = new MemoryStream();
                do
                {
                    result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                } while (!result.EndOfMessage && state == State.Open);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                ms.Seek(0, SeekOrigin.Begin);
                StreamReader reader = new StreamReader(ms, Encoding.UTF8);
                OnNewMessage(await reader.ReadToEndAsync());
            }
        }
        #endregion
    }
    public class DebugLogger
    {
        public string Content { get; }
        public DebugLogType SocketIOLogType { get; }

        public DebugLogger(string s, DebugLogType t)
        {
            Content = s;
            SocketIOLogType = t;
        }
    }
    public enum DebugLogType
    {
        Message,
        Warning,
        Error
    }
    public enum State
    {
        Open = 0,
        Opening = 1,
        Closed = 2
    }
}