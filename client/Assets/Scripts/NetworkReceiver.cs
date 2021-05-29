using System;
using System.Net.Sockets;



class NetworkReceiver
{
    private State _recvState = new State();
    private Action<Message> _onRecvMessageCallback;

    public NetworkReceiver(Socket socket, Action<Message> onRecvMessage)
    {
       _recvState.socket = socket; 
       _onRecvMessageCallback = onRecvMessage;
    }
    public void Receive()
    {
        _recvState.socket.BeginReceive(_recvState.buffer, _recvState.index, 1024, SocketFlags.None,ReceiveAsyncCallback,_recvState);
    }
    private void ReceiveAsyncCallback(IAsyncResult ar)
    {
        var state = ar.AsyncState as State;
        int byteRecv = state.socket.EndReceive(ar);

        if(byteRecv > 0)
        {
            if(state.length <= 0)
            {
                if(byteRecv >= Message.HEADER_LEN)
                {
                    var bts = new byte[2];
                    Array.Copy(state.buffer, bts, 2);
                    if(BitConverter.IsLittleEndian)
                    {
                        var t = bts[0];
                        bts[0] = bts[1];
                        bts[1] = t;
                    }
                    state.length = BitConverter.ToUInt16(bts,0);
                    Log.InfoFormat("State.Lenght = {0}", state.length);
                }
                else
                {
                }
            }
            state.index += byteRecv;
            if(state.index < state.length)
            {
            }
            else
            {
                ProcessRecvBuffer(state.buffer);
            }
            Receive(); 
        }
        else
        {
            Log.Info("[Info] End Expectedly!!");
        }
    }
   
    private void ProcessRecvBuffer(byte[] buffer)
    {
        Message message = new Message();
        Utility.ConvertByteArrayToMessage(buffer, ref message);
        _recvState.Reset();
        if(_onRecvMessageCallback != null)
        {
            _onRecvMessageCallback(message);
        }
    }
}
