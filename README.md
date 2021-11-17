# Socket.IO-Unity
A Socket.IO implementation for Unity with basic Event callbacks.

Usage example:
`socket = GetComponent<SocketIOUnity>();
_ = socket.Connect("ws://192.168.178.44:3000");
socket.On("connect", s => Debug.Log(":)"));
socket.On("test", s =>
{
    Debug.Log("Received . " + s);
    socket.Emit("event", "test");
});
socket.On("disconnect", s => Debug.Log(":("));`
