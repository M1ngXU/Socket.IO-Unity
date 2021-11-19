using System;
using System.Collections;
using UnityEngine;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using MingXu.Socket.MessageType;

namespace MingXu.Socket
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MingXu/Socket.IO")]
    public class SocketIOUnity : MonoBehaviour
    {
        private IO Socket;
        // inspector gui stuff
        public bool DontShowLogs = true;
        public bool DontShowWarnings = true;
        public bool DontShowErrors;
        public string Adress = "";
        public State SocketState = State.Closed;

        private Dictionary<string, Dictionary<string, Action<string>>> Listeners = new Dictionary<string, Dictionary<string, Action<string>>>();

        #region Unity Functions
        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }
        private void Update()
        {
            SocketState = Socket.state;
            if (Socket.state == State.Opening) return;
            // Log debug stuff
            IO.GetLog().ForEach(s =>
            {
                switch (s.SocketIOLogType)
                {
                    case DebugLogType.Error:
                        if (!DontShowErrors) Debug.LogError(s.Content, gameObject);
                        break;
                    case DebugLogType.Warning:
                        if (!DontShowWarnings) Debug.LogWarning(s.Content, gameObject);
                        break;
                    default:
                        if (!DontShowLogs) Debug.Log(s.Content, gameObject);
                        break;
                }
            });
            // Do messages stuff
            Socket.GetMessages().ForEach(s =>
            {
                if (!(Listeners.ContainsKey(s.@namespace) && Listeners[s.@namespace].ContainsKey(s.EventData.Key)))
                {
                    Debug.LogWarning("Incoming event in " + s.@namespace + " with name " + s.EventData.Key + " not registered.");
                    return;
                }
                Listeners[s.@namespace][s.EventData.Key].Invoke(s.EventData.Value);
            });
        }

        private void OnApplicationQuit()
        {
            if (Socket == null) return;
            Socket.Dispose();
        }
        #endregion

        #region Socket Functions
        #region Socket.Connect()
        /// <summary>
        /// Connects to the Websocket.
        /// </summary>
        /// <param name="adress">Adress of the socket.io server, if the path and query are empty (or `/`), the standard socket.io path/query is used.</param>
        /// <returns></returns>
        public async Task Connect(string adress)
        {
            await Connect(new UriBuilder(adress));
        }
        /// <summary>
        /// Connects to the Websocket.
        /// </summary>
        /// <param name="uri">UriBuilder of the adress, <see cref="Connect(string)"/> for more information on the adress.</param>
        /// <returns></returns>
        public async Task Connect(UriBuilder uri)
        {
            Socket = new IO();
            Adress = uri.ToString();
            await Socket.ConnectAsync(uri);
        }
        #endregion
        #region Socket.On()
        /// <summary>
        /// Callback for a specified event in the default namespace.
        /// </summary>
        /// <param name="name">Event name.</param>
        /// <param name="method">A function that is executed on the main thread when the Event is received.</param>
        public void On(string name, Action<string> method)
        {
            On("", name, method);
        }
        /// <summary>
        /// Callback for a specified event in the specified namespace.
        /// </summary>
        /// <param name="nsp">Namespace of the event.</param>
        /// <param name="name">Event name.</param>
        /// <param name="method">A function that is executed on the main thread when the Event is received.</param>
        public void On(string nsp, string name, Action<string> method)
        {
            if (!Listeners.ContainsKey(nsp))
            {
                Listeners.Add(nsp, new Dictionary<string, Action<string>>());
            }
            Listeners[nsp].Add(name, method);
        }
        #endregion
        #region Socket.Emit()
        /// <summary>
        /// Emits the specified event with the specified data to the default namespace.
        /// </summary>
        /// <param name="event">Event name.</param>
        /// <param name="data">Data parsed as a string.</param>
        public void Emit(string @event, string data)
        {
            Emit("", @event, data);
        }
        /// <summary>
        /// Emits the specified event with the specified data to the specified namespace.
        /// </summary>
        /// <param name="namespace">Namespace of the event.</param>
        /// <param name="event">Event name.</param>
        /// <param name="data">Data parsed as a string.</param>
        public void Emit(string @namespace, string @event, string data)
        {
            _ = Socket.Send(new SocketEvent(@event, data)
            {
                @namespace = @namespace
            });
        }
        #endregion
        #endregion
    }
}
