using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MingXu.Socket.MessageType
{
    public static class MessageFactory
    {
        public static EngineIOMessage Create(string data)
        {
            // get the engine io message type
            int engine_io_type = data[0].ToInt();
            // remove the type-int from the data string
            data = data.Substring(1);
            switch (engine_io_type)
            {
                case 0:
                    var ic = JsonUtility.FromJson<InitialConnectPayload>(data);
                    ic.data = data;
                    return ic;
                case 1:
                    return new Disconnect();
                case 2:
                    return new Ping(data);
                case 3:
                    return new Pong(data);
                case 4:
                    return OnSocketIOEvent(data);
                default:
                    IO.AddToLog("Unknown engine type: " + engine_io_type, DebugLogType.Error);
                    break;
            }
            return null;
        }

        private static EngineIOMessage OnSocketIOEvent(string data)
        {
            // get the start of the data-payload
            // it either starts with a {, [ or "
            int payload_start = Math.Min(
                        Math.Min(
                            data.IndexOf("[") == -1 ? int.MaxValue : data.IndexOf("["),
                            data.IndexOf("{") == -1 ? int.MaxValue : data.IndexOf("{")
                        ),
                        data.IndexOf("\"") == -1 ? int.MaxValue : data.IndexOf("\"")
                    );
            // nsp_end is where the first comma is
            //if not existing, then the namespace can be seen as ending @ 1, so with a length of 0
            int nsp_end = Math.Max(1, data.IndexOf(",")); // 1 if no `,`
            // type of the socket.io message
            int type_id = data[0].ToInt();
            // default namespace is ""
            string nsp = "";
            // only if there is a namespace ( nsp_end != 1 ) AND if the namespace is BEFORE the payload_start (so it's not part of the data)
            // then a namespace exists
            if (nsp_end > 1 && nsp_end < payload_start)
            {
                // the namespace starts at char[1], so `1` has to be substracted from the end of the namespace (nsp_end)
                nsp = data.Substring(1, nsp_end - 1);
            }
            // min value to 0, if there was no payload, the value is int.maxValue - namespace works
            if (payload_start == int.MaxValue) payload_start = 0; // 0 if not data
            int ack = 0;
            // 1. if a payload exists and there is at least one character between the namespace AND the payload (namespace ends default at `1`)
            // 2. if there is no payload but a namespace, the nsp_end shall not be the last character of the data
            // 3. if there is no payload and no namespace, then the length of the whole string has to be greater than one
            // if any of them apply, then there is a ack
            if ((payload_start > 0 && payload_start - nsp_end > 1)
                || (payload_start == 0 && nsp_end > 1 && nsp_end < data.Length - 1)
                || (payload_start == 0 && nsp_end == 1 && data.Length > 1))
            {
                ack = int.Parse(data.Substring(Math.Max(1, nsp_end)));
            }
            // data_s = payload-string
            string data_s = data.Substring(payload_start);
            Message m;
            // depending on the type, a different Message-child is returned
            switch (type_id)
            {
                case 0: // access granted to namespace
                    if (data_s.Length > 1)
                    {
                        m = JsonUtility.FromJson<ConnectedToNamespace>(data_s);
                    } else
                    {
                        m = new ConnectedToNamespace();
                    }
                    break;
                case 1: // access removed from namespace
                    m = new DisconnectedFromNamespace();
                    break;
                case 2: // "standard" event
                    m = new SocketEvent(data_s);
                    break;
                default:
                    // invalid type => invalid Message object
                    IO.AddToLog("Unknown message type: " + type_id, DebugLogType.Error);
                    return null;
            }
            // init the Message object.
            m.data = data_s;
            m.@namespace = nsp;
            m.type_id = type_id;
            m.ack = ack;
            return m;
        }
    }

    public abstract class EngineIOMessage
    {
        [NonSerialized]
        public int engine_io_type_id;
        [NonSerialized]
        public string payload;

        /// <summary>
        /// This function sets the payload of the abstract EngineIOMessage class.
        /// That needs to be a string, that'll be used in the final message.
        /// </summary>
        public abstract void SetPayload();
        public override string ToString()
        {
            SetPayload();
            return engine_io_type_id + payload;
        }
    }

    #region EngineIOMessages
    public abstract class InitialConnect : EngineIOMessage
    {
        [NonSerialized]
        public string data = "";

        public abstract void SerializeDataObject();

        public override void SetPayload() => payload = data;

        public InitialConnect() => engine_io_type_id = 0;
    }
    public class Disconnect : EngineIOMessage
    {
        public override void SetPayload() { }

        public Disconnect() => engine_io_type_id = 1;
    }
    public class InitialConnectPayload : InitialConnect
    {
        public string sid;
        public string[] upgrades;
        public int pingInterval;
        public int pingTimeout;

        public override void SerializeDataObject() => data = JsonUtility.ToJson(this);
    }
    public class Ping : EngineIOMessage
    {
        private string d; // context of ping?
        public override void SetPayload() => payload = d;
        public Ping(string d)
        {
            this.d = d;
            engine_io_type_id = 2;
        }
    }
    public class Pong : EngineIOMessage
    {
        private string d; // context of ping/pong?
        public override void SetPayload()
        {
            payload = d;
        }
        public Pong(string d)
        {
            engine_io_type_id = 3;
            this.d = d;
        }
    }
    public abstract class Message : EngineIOMessage
    {
        [NonSerialized]
        public int type_id;
        [NonSerialized]
        public string @namespace = "";
        [NonSerialized]
        public string data = "";
        [NonSerialized]
        public int ack;

        public abstract void SerializeDataObject();

        public override void SetPayload()
        {
            SerializeDataObject();
            payload = type_id + (string.IsNullOrEmpty(@namespace) ? "" : @namespace + ",") + (ack > 0 ? ack.ToString() : "") + data;
        }

        public Message() => engine_io_type_id = 4;
    }
    #endregion

    #region SocketIOMessages
    public class RequestAccessToNamespace : Message
    {
        public string Token;

        public override void SerializeDataObject() => data = JsonUtility.ToJson(this);

        public RequestAccessToNamespace() => type_id = 0;
    }
    public class Ack : Message
    {
        public override void SerializeDataObject() => data = "";

        public Ack() => type_id = 3;
    }
    public class ConnectedToNamespace : Message
    {
        public string sid;

        public override void SerializeDataObject() => data = JsonUtility.ToJson(this);
    }
    public class DisconnectedFromNamespace : Message
    {
        public override void SerializeDataObject() => data = "";
    }
    public class SocketEvent : Message
    {
        private string @event;
        private string d;
        public override void SerializeDataObject() => data = "[\"" + @event + "\",\"" + d + "\"]";

        public KeyValuePair<string, string> EventData;

        public SocketEvent(string data)
        {
            data = data.Substring(1, data.Length - 2);
            @event = data.Substring(0, data.IndexOf(",")).Trim().Trim("\"".ToCharArray());
            d = data.Substring(data.IndexOf(",") + 1).Trim().Trim("\"".ToCharArray());
            Setup();
        }

        public SocketEvent(string name, string data)
        {
            @event = name;
            d = data;
            Setup();
        }
        private void Setup()
        {
            EventData = new KeyValuePair<string, string>(@event, d);
            type_id = 2;
        }
    }
    #endregion
}
