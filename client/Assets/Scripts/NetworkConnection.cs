using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;

interface INetworkConnection
{
    void Setup(string ipaddr, int port);
    void SendMessage(Message message, bool startTimer);
    void Connect();
    void Disconnect();
}


class State
{
    const int MAX_SIZE = 8 * 1024;

    public byte[] buffer = new byte[8 * 1024];
    public UInt16 length;
    public int index;

    public Socket socket;




    public void Reset()
    {
        Array.Clear(buffer,0, buffer.Length);
        index = 0;
        length = 0;
    }

}
class NetworkConnection:INetworkConnection
{

    enum EConnectState
    {
        Null = 0,
        CLOSED = 1,
        SYN_SENT = 2,
        ESTABLISHED = 3,
        FIN_WAIT_1 = 4,
        FIN_WAIT_2 = 5,
        TIME_WAIT = 6
    }

    private EConnectState _eConnectState;
    private Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    private NetworkSender _sender;
    private NetworkReceiver _receiver;

    private uint _sendTimerCountdownId;

    private float _rttTime;

    private byte _svrSeq;

    private INetworkClient _networkclient;

    private byte _base;
    private byte _nextSendSeq;
    private Message[] _ringSendMsgAckBuffer;
    private const int ACK_BUFFER_SIZE = 256;
    public NetworkConnection(INetworkClient networkclient)
    {
        _rttTime = 3.0f;
        _networkclient = networkclient;
        _eConnectState = EConnectState.Null;
    }
    public void Setup(string ipaddr, int port)
    {
        _socket.Connect(IPAddress.Parse(ipaddr), port);
        _sender = new NetworkSender(_socket);
        _receiver = new NetworkReceiver(_socket, ProcessRecvBuffer);
        _svrSeq = 0;

        _base = 0;
        _nextSendSeq = 0;
        _ringSendMsgAckBuffer = new Message[ACK_BUFFER_SIZE];
    }

  
    private bool IsSendAckBufferFull()
    {
        if(_nextSendSeq == (_base - 1 + ACK_BUFFER_SIZE) % ACK_BUFFER_SIZE)
        {
            return true;
        }
        return false;
    }


    public void SendMessage(Message msg, bool startTimer = false)
    {
        msg.ctrl = (byte)ESAFlag.Seq;
        UInt16 len = msg.GetLength();
        msg.len = len;
        msg.seq = GetCliSeq();
        _ringSendMsgAckBuffer[msg.seq] = msg;

        _sender.Send(msg); 
    }

    public void Connect()
    {
        SendSYN();
        _receiver.Receive();
    }

    public void Disconnect()
    {
        // clear unack message
        _sender.ClearSendMsgBuffer() ;
        _base = _nextSendSeq;
        StopSendTimer();
        SendFIN();
    }
    private void SendSYN()
    {
        Log.Info("[Info] Send SYN");
        Message msg = new Message();
        msg.seq = GetCliSeq();
        msg.ack = GetSvrSeq();
        msg.syn = 1;
        msg.fin = 0;
        msg.rsd = 1;
        msg.ctrl = (byte)ESAFlag.Ctrl;
        UInt16 len = msg.GetLength();
        msg.len = len;
        _ringSendMsgAckBuffer[msg.seq] = msg;
        _sender.SendImmediate(msg); 
        _eConnectState = EConnectState.SYN_SENT;
        Log.InfoFormat("[Info] SendSYN seq = {0} ack = {1}", msg.seq, msg.ack);
    }
    void SendFIN()
    {
        Log.Info("[Info] Send FIN");
        Message msg = new Message();
        msg.seq = GetCliSeq();
        msg.ack = GetSvrSeq();
        msg.syn = 0;
        msg.fin = 1;
        msg.rsd = 0;
        msg.ctrl = (byte)ESAFlag.Ctrl;
        UInt16 len = msg.GetLength();
        msg.len = len;
        _ringSendMsgAckBuffer[msg.seq] = msg;
        _sender.Send(msg); 
        _eConnectState = EConnectState.FIN_WAIT_1;
        //        _finishTick = Time.realtimeSinceStartup;
    }


    private void StartSendTimer()
    {
        if(_nextSendSeq == _base)
        {
            _sendTimerCountdownId = CountdownTimer.Instance.StartTimer(_rttTime,OnSendTimeOut);
            Log.InfoFormat("[Info] Start SendTimer id = {0}",_sendTimerCountdownId);
        }

    }
    private void RestartSendTimer()
    {
        CountdownTimer.Instance.RestartTimer(_sendTimerCountdownId); 
        Log.InfoFormat("[Info] RestartSendTimer {0}",_sendTimerCountdownId);
    }
    private void StopSendTimer()
    {
        CountdownTimer.Instance.StopTimer(_sendTimerCountdownId);
        _sendTimerCountdownId = 0;
        Log.InfoFormat("[Info] StopTimer {0}",_sendTimerCountdownId);
    }

    void OnSendTimeOut()
    {
        Log.Info("[Info] Resend OnTimeOut");
        Resend();
        StartSendTimer();
    }
    private void Resend()
    {
        int index = _base;
        while(index != _nextSendSeq)
        {
            _sender.Send(_ringSendMsgAckBuffer[index]);
            index ++;
            index = index % 256;
        }
    }


    void Receive()
    {
    }
    void PrintBuffer(byte[] buffer)
    {
        var len = GetMessageLength(buffer);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if(buffer != null)
        {
            for(int i = 0; i < len; ++i)
            {
                var b = buffer[i];
                sb.Append(b);
                sb.Append(" ");
            }
        }
        Log.InfoFormat("{0}\n",sb.ToString());
    }
    void ProcessRecvBuffer(Message message)
    {
        Log.InfoFormat("[ProcessRecvBuffer] receive message seq = {0} ack = {1} ctrl = {2}", message.seq, message.ack, message.ctrl);
        if(message.ctrl == (byte)ESAFlag.Ctrl)// ctrl message
        {
            Log.InfoFormat("[Info] ProcessRecvBuffer Receive Ctrl Message");
            //  receive syn & ack message
            if(message.syn == 1)
            {
                if(_eConnectState == EConnectState.SYN_SENT)
                {
                    _base = (byte)(message.ack + 1);
                    _svrSeq = message.seq;
                    OnReceiveAck(); 
                    SendCtrlAck();
                    _eConnectState = EConnectState.ESTABLISHED;
                    Log.InfoFormat("ProcessRecvBuffer _eConnectState from {0} to {1}", EConnectState.SYN_SENT, _eConnectState);
                }
                else
                {
                    Log.InfoFormat("[Info] Receive syn with _eConnectState = {0}", _eConnectState);
                }
            }
            else if(message.fin == 1)
            {
                if(_eConnectState == EConnectState.FIN_WAIT_2)
                {
                    SendCtrlAck();
                    _eConnectState = EConnectState.TIME_WAIT;
                    Log.InfoFormat("[Info] Receive Fin, Send Ack, _eConnectState from {0} to {1}",EConnectState.FIN_WAIT_2, _eConnectState);
                    _finCountdownId = CountdownTimer.Instance.StartTimer(3, FinCountdown);
                }
                else
                {
                    Log.InfoFormat("[Info] Receive Fin while _eConnectState = {0}", _eConnectState);
                }
            }
            else if(_eConnectState == EConnectState.FIN_WAIT_1)// receive ack message for fin
            {
                _base = (byte)(message.ack + 1); 
                OnReceiveAck();
                _eConnectState = EConnectState.FIN_WAIT_2;
                Log.InfoFormat("[Info] Receive Ack Send Nothing, _eConnectState = {0}", _eConnectState);
            }
        }
        else if(message.ctrl == (byte)ESAFlag.Seq) // seq
        {
            Log.InfoFormat("[Info] ProcessRecvBuffer Receive seq message");
            if(message.seq != GetSvrSeq())
            {
                // ignore receiver 's message
                Log.InfoFormat("[Info] Receive message.seq = {0} != _svrSeq {1}, discard", message.seq, _svrSeq);
                ResendAck();
                return;
            }
            // receive message correctly
            // handle message
            // set server seq
            _svrSeq = message.seq;
            Log.InfoFormat("[Info] ProcessRecvBuffer message.seq = {0}",message.seq);

            SendAck();
            _networkclient.PostMessage(message);
        }
        else if(message.ctrl == (byte)ESAFlag.Ack) //ack
        {
            var ack = message.ack;
            _base = (byte)(ack);

            Log.InfoFormat("[Info] ProcessRecvBuffer Receive ack message ack = {0}",ack);
            OnReceiveAck();
        }
    }
    private void OnReceiveAck()
    {
        Log.InfoFormat("OnReceiveAck _base = {0} _nextSendSeq = {1}", _base, _nextSendSeq);
        if(_base == _nextSendSeq)
        {
            StopSendTimer();
        }
        else
        {
            RestartSendTimer();
        }
    }
    private uint _finCountdownId;
    private void FinCountdown()
    {
        Log.Info("[Info] Connection Closed");
        _eConnectState = EConnectState.CLOSED;
        _socket.Disconnect(false);
        _socket.Close();
        _socket.Dispose();
    }
    private void SendAck()
    {
        Message msg = new Message();
        msg.seq = _nextSendSeq; // ack message does not occupy seq
        msg.ack = GetSvrSeq();
        msg.syn = 0;
        msg.fin = 0;
        msg.rsd = 0;
        msg.ctrl = (byte)ESAFlag.Ack; //0 seq, 1 ack
        msg.len = msg.GetLength();
        _sender.SendImmediate(msg);

        Log.InfoFormat("[Info] SendAck seq = {0} ack = {1}", msg.seq, msg.ack);
    }
    private void SendCtrlAck()
    {
        Message msg = new Message();
        msg.ack = GetSvrSeq();
        msg.seq = _nextSendSeq; // ack message will not increase send seq
        msg.ctrl = (byte)ESAFlag.Ctrl;
        msg.len = msg.GetLength();
        _sender.SendImmediate(msg);
        Log.InfoFormat("[Info] SendCtrlAck seq = {0} ack = {1}", msg.seq, msg.ack);
    }

    private void ResendAck()
    {
        Message msg = new Message();
        msg.seq = 0;
        msg.ack = _svrSeq;
        msg.syn = 0;
        msg.fin = 0;
        msg.rsd = 0;
        msg.ctrl = 1;
        msg.len = msg.GetLength();
        _sender.SendImmediate(msg);
    }
    private int GetMessageLength(byte[] buffer)
    {
        var lenBuf = new byte[2];
        Array.Copy(buffer,lenBuf,2);
        Utility.ConvertToBigEndianShort(lenBuf);

        var len = BitConverter.ToUInt16(lenBuf,0);
        return len;
    }

    private byte GetCliSeq()
    {
        _nextSendSeq ++;
        return _nextSendSeq;
    }

    public byte GetSvrSeq()
    {
        return (_svrSeq);
    }

}
