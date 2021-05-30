using System;


static class Utility
{
    public static  void ConvertToLocalEndianShort(byte[] tmpBuffer)
    {
        if(BitConverter.IsLittleEndian)
        {
            byte t = tmpBuffer[0];
            tmpBuffer[0] = tmpBuffer[1];
            tmpBuffer[1] = t;
        }
    }
    public static void ConvertToBigEndianShort(byte[] tmpBuffer)
    {
        if(BitConverter.IsLittleEndian)
        {
            byte t = tmpBuffer[0];
            tmpBuffer[0] = tmpBuffer[1];
            tmpBuffer[1] = t;
        }
    }
    public static byte increase(byte seq)
    {
       seq += 1; 
       return (byte)(seq);
    }
    public static int WriteUInt16(byte[] btarr, int index, UInt16 num)
    {
        var tmpBuff = BitConverter.GetBytes(num);
        ConvertToBigEndianShort(tmpBuff);
        Array.Copy(tmpBuff, 0,btarr, index, sizeof(UInt16));
        index += sizeof(UInt16);
        return index;
    }
    public static int WriteUInt8(byte[] btarr, int index, byte num)
    {
        var tmpBuff = BitConverter.GetBytes(num);
        Array.Copy(tmpBuff,0, btarr, index, sizeof(byte));
        index += sizeof(byte);
        return index;
    }
    public static int WriteByte(byte[] btarr, int index, byte bt)
    {
        btarr[index] = bt;
        index++;
        return index;
    }
    public static UInt16 ReadUInt16(byte[] btarr,ref int index)
    {
        var sz = sizeof(UInt16);
        var lenBuff = new byte[sz];
        Array.Copy(btarr,index, lenBuff,0, sz);
        index += sizeof(UInt16);
        Utility.ConvertToBigEndianShort(lenBuff);
        return BitConverter.ToUInt16(lenBuff,0);
    }
    public static byte ReadUInt8(byte[] btarr, ref int index)
    {
        var sz = sizeof(byte);
        var tmpBuff = new byte[sz];
        Array.Copy(btarr, index, tmpBuff, 0, sz);
        index += sz;
        return (byte)tmpBuff[0];
    }
    public static void ConvertMessageToByteArray(Message message, ref byte[] btarr)
    {
        int index = 0;
        index = Utility.WriteUInt16(btarr, index, message.len);
        index = Utility.WriteUInt8(btarr, index, message.seq);
        index = Utility.WriteUInt8(btarr, index, message.ack);
        byte bt = 0;
        bt |= message.syn;
        bt |= (byte)(message.fin <<1);
        bt |= (byte)(message.rsd <<2);
        bt |= (byte)(message.ctrl << 3);
        btarr[index++] = bt; 
        if (message.data != null)
        {
            Array.Copy(message.data, 0, btarr, index, message.data.Length);
        }
    }
    public static void ConvertByteArrayToMessage(byte[] btarr, ref Message message)
    {
        int index =0;
        var lenBuf = new byte[2];
        Array.Copy(btarr,lenBuf,2);
        Utility.ConvertToBigEndianShort(lenBuf);

        message.len = Utility.ReadUInt16(btarr,ref index); 
        message.seq = Utility.ReadUInt8(btarr, ref index);
        message.ack = Utility.ReadUInt8(btarr, ref index);
        byte bt = btarr[index++];
        message.syn = (byte)(bt & 0x01);
        message.fin = (byte)((bt >> 1) & 0x01);
        message.rsd = (byte)((bt >> 2) & 0x01);
        message.ctrl = (byte)((bt >> 3) & 0x01);
        var dataLen = message.len  - Message.HEADER_LEN;
        if(dataLen > 0)
        {
            message.data = new byte[dataLen];
            Array.Copy(btarr,index, message.data, 0, dataLen);
        }
        else{
            message.data = null;
        }
        Log.InfoFormat("[ConvertByteArrayToMessage] seq = {0} ack = {1} syn = {2} fin = {3} rsd = {4}",
                message.seq, message.ack, message.syn, message.fin, message.rsd);
    }



}
