using System;
using System.Net;
using System.Net.Sockets;


class NetworkSender
{ 
    enum ESendState
    {
        Null = 0,
        Idle = 1,
        Sending = 2,
    }

    const int ASYNC_SEND_BUFFER_SIZE = 256;
    const int IM_SEND_BUFFER_SIZE = 128;

    
    private ESendState _eSendState = ESendState.Null;
    private State _sendState = new State();
   
    // Async send buffer 
    private int _asyncSendBase;
    private int _asyncNextSendIndex;
    private Message[] _ringAsyncSendMsgBuffer;
    private int _imSendBase;
    private int _imNextSendIndex;
    private Message[] _ringIMSendMsgBuffer;

    static bool LOG_ENABLE = false;

    public NetworkSender(Socket socket)
    {
        _sendState.socket = socket;
        _eSendState = ESendState.Idle;
        _ringAsyncSendMsgBuffer = new Message[ASYNC_SEND_BUFFER_SIZE];
        _asyncSendBase = _asyncNextSendIndex = 0;

        _ringIMSendMsgBuffer = new Message[ASYNC_SEND_BUFFER_SIZE];
        _imSendBase = _imNextSendIndex = 0;

    }
    private bool IsAsyncSendBufferFull()
    {
        return _asyncSendBase == (_asyncNextSendIndex - 1 + ASYNC_SEND_BUFFER_SIZE) % ASYNC_SEND_BUFFER_SIZE;
    }
    private bool IsAsyncSendBufferEmpty()
    {
        return _asyncSendBase == _asyncNextSendIndex;
    }
    private bool IsIMSendBufferFull()
    {
        return _imSendBase == (_imNextSendIndex - 1 + IM_SEND_BUFFER_SIZE) % IM_SEND_BUFFER_SIZE;
    }
    private bool IsIMSendBufferEmpty()
    {
        return _imSendBase == _imNextSendIndex;
    }
    public void SendImmediate(Message msg)
    {
        if(IsIMSendBufferFull())
        {
            Log.InfoFormat("[Warnning] IM Send Buffer is Full!");
            return;
        }
        if(LOG_ENABLE)
        {
            Log.InfoFormat("[NetworkSender] Send _imNextSendIndex = {0}", _imNextSendIndex);
        }
        _ringIMSendMsgBuffer[_imNextSendIndex] = msg;
        _imNextSendIndex = (_imNextSendIndex + 1) %  IM_SEND_BUFFER_SIZE;
        StartAsyncSend();
    }

    public void Send(Message msg)
    {
        if(IsAsyncSendBufferFull())
        {
            if(LOG_ENABLE)
            {
                Log.InfoFormat("[Warnning] Async Send Buffer is Full!");
            }
            return;
        }
        if(LOG_ENABLE)
        {
            Log.InfoFormat("[NetworkSender] Send _asyncNextSendIndex = {0}", _asyncNextSendIndex);
        }
        _ringAsyncSendMsgBuffer[_asyncNextSendIndex] = msg;
        _asyncNextSendIndex = (_asyncNextSendIndex + 1) % ASYNC_SEND_BUFFER_SIZE;
        StartAsyncSend();
    }
    public void ClearSendMsgBuffer()
    {
        while(_asyncSendBase != _asyncNextSendIndex)
        {
            _ringAsyncSendMsgBuffer[_asyncSendBase] = null;
            _asyncSendBase = (_asyncSendBase + 1) % ASYNC_SEND_BUFFER_SIZE;
        }
    }
    private void StartAsyncSend()
    {
        if(_eSendState == ESendState.Sending)
        {
            if(LOG_ENABLE)
            {
                Log.InfoFormat("[Info] Cur eSendState = {0} StartAsyncSend failed", _eSendState);
            }
            return;
        }
        if(IsAsyncSendBufferEmpty() && IsIMSendBufferEmpty())
        {
            if(LOG_ENABLE)
            {
                Log.InfoFormat("[Info] AsyncSendBuffer is Empty");
            }
            return;
        }
        Message msg = null;
        if(!IsIMSendBufferEmpty())
        {
            if(LOG_ENABLE)
            {
                Log.InfoFormat("[NetworkSender:StartAsyncSend] _imSendBase = {0}", _imSendBase);
            }
            msg = _ringIMSendMsgBuffer[_imSendBase];
            _imSendBase = (_imSendBase + 1) % IM_SEND_BUFFER_SIZE;
        }
        else if(!IsAsyncSendBufferEmpty())
        {
            if(LOG_ENABLE)
            {
                Log.InfoFormat("[NetworkSender::StartAsyncSend] _asyncSendBase = {0}", _asyncSendBase);
            }
            msg = _ringAsyncSendMsgBuffer[_asyncSendBase];
            _asyncSendBase = (_asyncSendBase + 1) % ASYNC_SEND_BUFFER_SIZE;
        }
        if (msg == null)
        {
            return;
        }
        Log.InfoFormat("[NetworkSender] SendAsync seq = {0}", msg.seq); 
        TransferSendState(ESendState.Sending); 
        byte[] buff = new byte[msg.len];         
        Utility.ConvertMessageToByteArray(msg, ref buff);

        Array.Clear(_sendState.buffer, 0, _sendState.buffer.Length);
        Array.Copy(buff, 0, _sendState.buffer, 0, buff.Length);
        _sendState.index = 0;
        _sendState.length = (ushort) buff.Length;
        SendAsync();
    }

    private void TransferSendState(ESendState eSendState)
    {
        var eSendStateOld = _eSendState;
        _eSendState = eSendState;
        if(LOG_ENABLE)
        {
            Log.InfoFormat("[Info] TransferSendState from {0} to {1}", eSendStateOld, _eSendState);
        }
    }

    private void SendAsync()
    {
        if(LOG_ENABLE)
        {
            Log.InfoFormat("[Info] SendAsync");
        }
        _sendState.socket.BeginSend(_sendState.buffer,
                _sendState.index,
                _sendState.length - _sendState.index,
                SocketFlags.None,
                SendAsyncCallback,
                _sendState);

    }
    private void SendAsyncCallback(IAsyncResult ar)
    {
        State state = ar.AsyncState as State;
        UInt16 byteSend = (UInt16) state.socket.EndSend(ar);
        if(byteSend > 0)
        {
            state.index += byteSend;
            if(state.index < byteSend)
            {
                SendAsync();
            }
            else
            {
                state.Reset();
                TransferSendState(ESendState.Idle);
                StartAsyncSend(); 
            }
        }
    }
}

