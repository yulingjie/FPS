using UnityEngine;
using System;
using System.Collections.Concurrent;

public enum ESAFlag: byte
{
    Ctrl = 0,
    Seq = 1,
    Ack = 2,
}

public class Message
{
    public static UInt16 HEADER_LEN = sizeof(UInt16) + sizeof(byte) + sizeof(byte) + sizeof(byte) + 1;
    public UInt16 len;
    public byte seq;
    public byte ack;
    public byte syn;
    public byte fin;
    public byte rsd;
    public byte ctrl;

    public byte[] data;

    public UInt16 GetLength()
    {
        if (data != null)
        {
            return (UInt16)(HEADER_LEN + data.Length);
        }
        return HEADER_LEN;
    }
}
class SimpleTimer
{
    public float tick;
    public Action onTickNotify;
}
interface INetworkClient
{
    void PostMessage(Message message);
    void LogSendMessage(Message message);
}

public class NetworkClient : MonoBehaviour, INetworkClient
{
    private static NetworkClient s_Instance;
    public static NetworkClient Instance
    {
        get{
            if(s_Instance == null)
            {
                return null;
            }
            return s_Instance;
        }
    }
       const float SEND_TIME_INI = 3.0f;
   // Start is called before the first frame update

    
    private ConcurrentQueue<Message> _recvMessageQ = new ConcurrentQueue<Message>();

    private float _sendTimer;

    private NetworkConnection _networkConnection;

    

    
  

    void ReqConnect()
    {

    }


    void Start()
    {
        _networkConnection = new NetworkConnection(this);
        _networkConnection.Setup("127.0.0.1",8080);
        //SendSYN();
        //Receive();
    }
    public void Connect()
    {
        _networkConnection.Connect();
        //_socket.Connect(IPAddress.Parse("127.0.0.1"), 8080);
        //_sendState.socket = _socket;
        //_recvState.socket = _socket;

    }
    public void Disconnect()
    {
        _networkConnection.Disconnect();
    }
    
    
    private void SendMessage(Message msg,bool startTimer = false)
    {
        _networkConnection.SendMessage(msg, startTimer);
    }
    enum EMsgType
    {
        MsgNone,
        MsgEcho,
    }
    public void SendEchoMessage()
    {
        Message msg = new Message();
        //msg.seq = GetCliSeq();
        //msg.ack = inverse(_svrSeq);
        msg.syn = 0;
        msg.fin = 0;
        msg.rsd = 0;
        var dataBuff = System.Text.Encoding.UTF8.GetBytes("Echo message");

        msg.data = FillMessageData(EMsgType.MsgEcho, dataBuff);
        SendMessage(msg, true);
    }
    private byte[] FillMessageData(EMsgType eMsgType, byte[] dataBuffer)
    {
        var tmpBuff = BitConverter.GetBytes((ushort)eMsgType);
        Utility.ConvertToBigEndianShort(tmpBuff);
        //Debug.LogFormat("tmpBuff [0] = {0} 1 = {1} ", tmpBuff[0], tmpBuff[1]);
        var dstBuffer = new byte[2 + dataBuffer.Length];
        Array.Copy(tmpBuff,dstBuffer,2);
        Array.Copy(dataBuffer,0,dstBuffer,2,dataBuffer.Length);
        return dstBuffer;
    }

   
         
   
    private void LogReceiveMsg(Message msg)
    {

    }

    private void ProcessRecvMessage(Message msg)
    {
        //Debug.LogFormat("Length = {0}",msg.data.Length);
        Debug.LogFormat("[Info] receive msg ack = {0} seq = {1} fin = {2} syn = {3}, rsd = {4}",msg.ack,msg.seq, msg.fin,msg.syn, msg.rsd);
       
    }
    // Update is called once per frame
    void Update()
    {
        if(_recvMessageQ.Count > 0)
        {
            while(_recvMessageQ.Count >0)
            {
                Message message;
                bool result = _recvMessageQ.TryDequeue(out message);
                if(result)
                {
                    ProcessRecvMessage(message);
                }
            }
        }
    }
    void FixedUpdate()
    {


    }

    void Awake()
    {
        s_Instance = this;

    }

    void AddTimer(float tick, Action callback)
    {
        var timer = new SimpleTimer();
        timer.tick = tick;
        timer.onTickNotify = callback;
    }
    public void PostMessage(Message message)
    {
        _recvMessageQ.Enqueue(message);
    }
    public void LogSendMessage(Message message)
    {
        var tmpBuff = new byte[2];
        if(message.data != null)
        {
            Array.Copy(message.data,0, tmpBuff, 0, 2);
            Utility.ConvertToLocalEndianShort(tmpBuff);
            var eMsgType = (EMsgType)BitConverter.ToUInt16(tmpBuff, 0);
            var dataBuff = new byte[message.data.Length - 2];
            Array.Copy(message.data,2, dataBuff,0, dataBuff.Length);
            Log.InfoFormat("[Info] Send seq = {0} ack = {1} syn = {2} fin = {3} rsd = {4} data length = {5} msgType = {6} data content = {7}",message.seq, message.ack, message.syn, message.fin, message.rsd,
                    message.data.Length, eMsgType,  System.Text.Encoding.UTF8.GetString(dataBuff));
        }
        else
        {
            Log.InfoFormat("[Info] Send seq = {0} ack = {1} syn = {2} fin = {3} rsd = {4}", message.seq, message.ack, message.syn, message.fin, message.rsd); 
        }
    }


}
