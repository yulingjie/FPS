package main

import (
	"encoding/binary"
	"errors"
	"fmt"
	"net"

)

func main() {

    var port string = ":8080";
    var porotcol string = "udp";

    var udpAddr *net.UDPAddr ;
    var err error ;
    udpAddr, err = net.ResolveUDPAddr(porotcol, port);
    if err != nil {
        fmt.Println("Wrong Address");
        return
    }
    fmt.Printf("Server Main Start Listen UDP message on %s\n", port)

    var udpConn *net.UDPConn;
    udpConn, err = net.ListenUDP(porotcol, udpAddr);
    if err != nil {
        fmt.Println(err);
    }

    message_sender_chan := make(chan MessageChanData, 10)
    var addr2conn map[string]*Connection
    addr2conn = make(map[string]*Connection)
    go receiver(udpConn, addr2conn,message_sender_chan)
    go sender(udpConn, message_sender_chan)
    for{
    }
}


func inv_seq(seq byte) byte{
    if(seq == 0) {
        return 1
    }
    return 0
}
const (
    non = iota
    listen = iota
    syn_rcvd = iota
    established = iota
    close_wait = iota
    last_ack = iota
    closed = iota
)

type Connection struct{
    state int;
    cli_seq byte;
    svr_seq byte;
    addr net.Addr;
    ring_buffer *RingBuffer;
    msg_receive_chan chan Message;
    msg_send_chan chan MessageChanData;
    quit chan int
}
func (conn * Connection) handler(addr2conn map[string]*Connection){
    for{
        select {
        case msg := <-conn.msg_receive_chan:
            msg_type := MSG_NONE
            if (len(msg.data) >= 2){
                msg_type = conn.get_message_type(msg.data[0:2])
            }
            if(msg_type == MSG_ECHO){
                data := make([]byte,len(msg.data[2:]))
                copy(data, msg.data[2:])
                conn.send_echo_message(data);
            }else{// MSG_NONE do nothing
                //conn.send_ack_message(0,0)
            }
        }
    }
}
const (
    MSG_NONE uint16 = iota
    MSG_ECHO uint16 = iota
)
func log_message(message *Message, info string){
    fmt.Printf("[Info] send %s seq = %d ack = %d syn = %d fin = %d  data length = %d data content = %s\n", info, message.seq, message.ack, message.syn,message.fin, len(message.data), string(message.data))
}
func (conn* Connection) send_fin(){
    message := create_message()
    message.seq = conn.get_svr_seq()
    message.ack = conn.get_cli_ack()
    message.fin = 1
    message.data = []byte("Send Fin")
    message.CalculateLength()
    log_message(message, "Send Fin")
    conn.send(message)
}
func send_fin(svr_seq byte, cli_ack byte,msg_send_chan chan MessageChanData, addr net.Addr){
    message := create_message()
    message.seq = svr_seq
    message.ack = cli_ack
    message.fin = 1
    message.CalculateLength()
    log_message(message, "Send Fin")
    send(msg_send_chan, addr, message)
}
func (conn * Connection) get_message_type(data []byte) uint16{
    if(len(data) >= 2){
        return  binary.BigEndian.Uint16(data)
    }
    return MSG_NONE
}
func (conn * Connection) send(message *Message){
    msg_chan_data := MessageChanData{}
    msg_chan_data.addr = conn.addr
    msg_chan_data.message = message
    conn.msg_send_chan <- msg_chan_data
}
func send(msg_send_chan chan MessageChanData, addr net.Addr, message *Message){
    msg_chan_data := MessageChanData{}
    msg_chan_data.addr = addr
    msg_chan_data.message = message
    msg_send_chan <- msg_chan_data
}

func (conn * Connection) send_echo_message(data []byte){
    message := create_message()
    message.seq = conn.svr_seq
    conn.svr_seq = inv_seq(conn.svr_seq)
    message.ack = inv_seq(conn.cli_seq)
    message.data = data
    message.CalculateLength()
    fmt.Printf("send_echo_message seq = %d\n",conn.svr_seq);
    log_message(message, "echo message")
    conn.send(message)
}
func (conn* Connection) get_svr_seq() byte{
    seq := conn.svr_seq
    conn.svr_seq = inv_seq(conn.svr_seq)
    return seq
}
func (conn* Connection) get_cli_ack() byte{
    return inv_seq(conn.cli_seq)
}

func (conn * Connection) send_syn_recv_message(){
    message := &Message{}
    message.seq = conn.get_svr_seq()
    message.ack = conn.get_cli_ack()
    message.syn = 1
    message.fin = 0
    message.rsd = 0
    message.data = []byte("servr syn recv message")
    message.CalculateLength()
    log_message(message, "syn recv message")
    conn.send(message)
}
func send_syn_recv_message(svr_seq byte, cli_ack byte , msg_send_chan chan MessageChanData, addr net.Addr){
    message := &Message{}
    message.seq = svr_seq
    message.ack = cli_ack
    message.syn = 1
    message.fin = 0
    message.rsd = 0
    message.CalculateLength()
    log_message(message, "syn recv message")
    send(msg_send_chan,addr, message)
}
func (conn * Connection) send_ack_message(){
    message := &Message{}
    message.seq = conn.svr_seq
    conn.svr_seq = inv_seq(conn.svr_seq)
    //fmt.Printf("svr_seq = %d",conn.svr_seq)
    message.ack = inv_seq(conn.cli_seq)
    message.syn = 0
    message.fin = 0
    message.rsd = 0
    message.data = []byte("server ack message");
    message.CalculateLength()
    log_message(message, "ack message")
    conn.send(message)
}

func (conn * Connection) receive() {
    var msg_len uint16
    msg_len = 0
    for{
        if(conn.ring_buffer.Length() < 2){
            break
        }
        if(msg_len <= 0){
            msg_len = binary.BigEndian.Uint16(conn.ring_buffer.data[conn.ring_buffer.r:])
            fmt.Printf("[Info] Connection %s receive msg_len = %d \n", conn.addr.String(),msg_len)
        }
        if(uint16(conn.ring_buffer.Length()) < msg_len){
            fmt.Printf("[Error] Connection %s ringBuffer length %d < msg length %d \n",conn.addr.String(),conn.ring_buffer.Length(), msg_len)
            break;
        }
        msg_buff := make([]byte, msg_len)
        read_len := uint16(conn.ring_buffer.Read(msg_buff))
        if(msg_len != read_len){
            fmt.Printf("[Error] Connection %s msg_buff read  error", conn.addr.String())
        }
        // process Message 
        var index int
        index = 0
        var msg Message
        var header_len uint16
        header_len = MessageHeaderLength
        msg.length = msg_len
        index += 2;
        msg.seq = msg_buff[index]
        index ++
        msg.ack = msg_buff[index]
        index ++
        msg.syn = msg_buff[index]
        index ++
        msg.fin = msg_buff[index]
        index ++
        msg.rsd = msg_buff[index]
        index ++
        msg.data = make([]byte, msg_len - header_len)
        copy(msg.data,msg_buff[index:])
        msg_type := conn.get_message_type(msg.data)
        if(len(msg.data) > 0 ){
            fmt.Printf("[Info] receive seq = %d ack = %d syn = %d fin = %d rsd = %d data length = %d msg_type = %d data content = %s\n",msg.seq, msg.ack, msg.syn, msg.fin, msg.rsd,len(msg.data),msg_type, string(msg.data[2:]))
        }else{
            fmt.Printf("[Info] receive seq = %d ack = %d syn = %d fin = %d rsd = %d\n",msg.seq, msg.ack, msg.syn, msg.fin, msg.rsd )
        }
        if(msg.syn != 1 && msg.ack != conn.svr_seq){
            fmt.Printf("[Error] receive non-match cli-ack %d with server seq %d", msg.ack, conn.svr_seq)
            return
        }
        conn.cli_seq = msg.seq
        conn.msg_receive_chan <- msg
    }

}
type ChanData struct{
    Buff [1024]byte;
    Size int;
}
type Message struct{
    length uint16;
    seq byte;
    ack byte;
    syn byte;
    fin byte;
    rsd byte;
    data []byte;
}

func create_message() *Message{
    message := &Message{}
    message.seq = 0
    message.ack = 0
    message.syn = 0
    message.fin = 0
    message.rsd = 0
    return message
}
const(
    MessageHeaderLength = 2 + 1 +1 + 1 +1 +1
)
type MessageChanData struct{
    message *Message;
    addr net.Addr;
}

type RingBuffer struct{
    r int;
    w int;
    data []byte;
    capacity int;
}
func (msg *Message) CalculateLength() uint16 {
    length:= uint16(MessageHeaderLength + len(msg.data))
    msg.length = length
    return length
}
func (msg *Message) ConvertToBuffer(buff []byte) int{
    var index int
    index = 0
    binary.BigEndian.PutUint16(buff, msg.length)
    index += 2
    buff[index] = msg.seq
    index++
    buff[index] = msg.ack
    index++
    buff[index] = msg.syn
    index++
    buff[index] = msg.fin
    index++
    buff[index] = msg.rsd
    index ++
    //fmt.Printf("[ConvertToBuffer] seq = %d ack = %d syn = %d fin = %d rsd = %d length = %d\n", msg.seq,
//msg.ack, msg.syn, msg.fin, msg.rsd, msg.length);
    copy(buff[index:],msg.data)
    return int(msg.length)
}
func buff_seq(buff []byte) byte{
    return buff[2]
}
func buff_ack(buff []byte) byte{
    return buff[3]
}
func buff_syn(buff []byte) byte{
    return buff[4]
}
func buff_fin(buff []byte) byte{
    return buff[5]
}
func buff_rsd(buff []byte) byte{
    return buff[6];
}

func New(capacity int) *RingBuffer{
    return &RingBuffer{
        data : make([]byte, capacity),
        capacity: capacity,
    }
}
func (rb * RingBuffer) Length() int{
    if( rb.r == rb.w) {
        return 0
    }
    return (rb.w + rb.capacity - rb.r) %(rb.capacity)
}
func (rb* RingBuffer) Remain() int{
    if(rb.w == rb.r -1){
        return 0
    }
    return (rb.r - 1 + rb.capacity - rb.w) %(rb.capacity)
}
func (rb * RingBuffer) IncR(l uint){
    rb.r += int(l)
    rb.r = rb.r % rb.capacity;
}
func (rb *RingBuffer) IncW(l uint){
    rb.w += int(l)
    rb.w = rb.w % rb.capacity;
}
func (rb * RingBuffer) Read(p []byte) int{
    rlen := len(p)
    if(rlen > rb.Length()){
        rlen = rb.Length()
    }
    copy(p, rb.data[rb.r:rb.r + rlen])
    rb.IncR(uint(rlen))
    return rlen
}
func (rb * RingBuffer) Write(p []byte) int{
    wlen := len(p)
    rem := rb.Remain()
    if(wlen > rem){
        wlen = rem
    }
    copy(rb.data[rb.w:rb.w + wlen], p[:])
    rb.IncW(uint(wlen))
    return wlen
}
func (rb* RingBuffer) ReadByte() (byte,error){
    if(rb.Length()> 1){
        bt := rb.data[rb.r]
        rb.IncR(1)
        return bt,nil
    }
    return 0,errors.New("data not available")
}

func create_new_connection(addr net.Addr, msg_send_chan chan MessageChanData) *Connection{
    conn := &Connection{}
    conn.ring_buffer = New(8*10240)
    conn.addr = addr
    conn.msg_receive_chan = make(chan Message, 10)
    conn.msg_send_chan = msg_send_chan
    conn.state = non
    return conn
}

type PreConnection struct{
    svr_seq_isn byte;
    state int;
    addr net.Addr;
}
func receiver(conn* net.UDPConn, addr2conn map[string]*Connection, msg_send_chan chan MessageChanData){
    fmt.Println("[Info] receiver Start!")
    // addr to ringBuffer map
    //var ringBuffer *RingBuffer;
    //ringBuffer = New(8*10240)
    // total bytes number
    // current parsing message length
    addr2PreCon := make(map[string]*PreConnection)
    var buf [1024]byte
    for{
        select{
        default:
            n,addr, err := conn.ReadFrom(buf[0:])
            fmt.Printf("[Info] connection %s receive message %d\n", addr.String(), n)
            if err != nil{
                fmt.Println(err)
            }
            conn, ok := addr2conn[addr.String()]
            /*if(!ok){
                conn = create_new_connection(addr, msg_send_chan)
                fmt.Printf("[Info] creates a listen socket\n")
                conn.state = listen
                addr2conn[addr.String()] = conn
                go func(cn *Connection){
                    cn.handler(addr2conn)
                }(conn)
            }*/
            if(ok){
                // move connection close procedure forward
                // when current message is fin message
                if(conn.state == established && buff_fin(buf[:]) == 1){
                    // control message should not append extra data
                    // we would not accumulate fin message
                    if(n == MessageHeaderLength) {
                        msg_len := binary.BigEndian.Uint16(buf[0:2])
                        if(msg_len != MessageHeaderLength) {
                            fmt.Printf("[Info] Ignore FIN Message with Length %d not match MessageHeaderLength %d\n",msg_len, MessageHeaderLength)
                            return
                        }
                        seq := buff_seq(buf[:])
                        ack := buff_ack(buf[:])
                        if(ack == conn.svr_seq){
                            // receive fin 
                            // send ack
                            conn.cli_seq = seq
                            conn.state = close_wait
                            conn.send_ack_message()
                            fmt.Printf("[Info] receive FIN send ACK, change connection state to close_wait\n")
                            // send fin
                            send_fin(conn.get_svr_seq(), conn.get_cli_ack(), msg_send_chan, conn.addr)
                            conn.state = last_ack
                            fmt.Printf("[Info] send FIN, change connection state to last_ack\n")
                        }else{
                            fmt.Printf("[Info] Ignore message with ack %d mismatch svr_seq %d\n", ack,conn.svr_seq)
                        }
                        // now we need to wait for final ack
                    }else {
                        fmt.Printf("[Info] FIN message should not has extra data!\n")
                    }
                }else if (conn.state == last_ack){
                    // check is last ack message
                    if(n == MessageHeaderLength){
                        msg_len := binary.BigEndian.Uint16(buf[0:2])
                        if(msg_len != MessageHeaderLength){
                            fmt.Printf("[Info] Ignore LastAck message with Length")
                            return
                        }else{
                            ack := buf[3]
                            if(ack == conn.svr_seq){
                                conn.state = closed
                                fmt.Printf("[Info] receive ack, send nothing, change connection state to closed\n")
                                delete(addr2conn, addr.String())
                            }else{
                                fmt.Printf("[Info] LastAck message cli ack %d mismatch svr seq %d\n",ack, conn.svr_seq)
                            }
                        }
                    }else{
                        fmt.Printf("[Info] LastAck message should not has extra data\n")
                    }
                }else if(conn.state == established){
                    // normal message should be accumulated
                    var ring_buffer *RingBuffer
                    ring_buffer = addr2conn[addr.String()].ring_buffer
                    ring_buffer.Write(buf[0:n])
                    conn.receive()
                }else{
                    fmt.Printf("[Warn] Unhandle Message with connection state = %d\n", conn.state)
                }


            }else{
                fmt.Printf("[Info] Receive message withou connection %d\n", n);
                // message without connection
                // check if syn message
                // move connection establish forward
                // we will not accumulate syn message
                // for connection still not exists
                if(n == MessageHeaderLength){
                    //var index int
                    //index = 0
                    msg_len := binary.BigEndian.Uint16(buf[0:2])
                    if( msg_len != MessageHeaderLength){
                        fmt.Printf("[Info] Ignore SYN Message with Length %d not match Header Length %d\n", msg_len, MessageHeaderLength);
                        return;
                    }
                    seq := buff_seq(buf[:])
                    ack := buff_ack(buf[:])
                    syn := buff_syn(buf[:])
                    //msg.fin = msg_buff[index]
                    //msg.rsd = msg_buff[index]
                    preconn, preok := addr2PreCon[addr.String()]
                    if(!preok){
                        if(syn == 1){

                            // a totally new message with addr never seen
                            // we will try to prepare a new connection
                            preconn = &PreConnection{}
                            preconn.svr_seq_isn = 0 // we set a random server seq initial value
                            preconn.state = syn_rcvd
                            preconn.addr = addr
                            addr2PreCon[addr.String()] = preconn
                            // we send syn & ack
                            send_syn_recv_message(preconn.svr_seq_isn, inv_seq(seq),msg_send_chan,addr)

                            preconn.svr_seq_isn = inv_seq(preconn.svr_seq_isn)
                            fmt.Printf("[Info] receive SYN send SYN & ACK, change connection state to syn_rcvd\n")
                        }else{
                            // non-syn message is ignored
                            fmt.Printf("[Info] receive non syn message from %s before connection setup\n",addr.String())
                        }
                    }else{
                        // a already pre created connection
                        // ideally we need to check if we ar in syn_rcvd state
                        // and we shall wait for ack
                        if(syn == 0){
                            if(ack != preconn.svr_seq_isn){
                                // ignore mismatch msg
                                fmt.Printf("[Info] ignore mismatch message\n")
                            }else{
                                if(preconn.state == syn_rcvd){
                                    // now we are connected
                                    // we create a connection and remove preconn
                                    conn = create_new_connection(addr, msg_send_chan)
                                    conn.cli_seq = seq
                                    conn.svr_seq = preconn.svr_seq_isn
                                    conn.state = established
                                    addr2conn[addr.String()] = conn
                                    delete(addr2PreCon, addr.String())
                                    // start connection handler
                                    go func(cn *Connection){
                                        cn.handler(addr2conn)
                                    }(conn)
                                    fmt.Printf("[Info] receive ACK send nothing, change connection state to established\n")
                                }else {
                                    fmt.Printf("[Info] preconn.state %d is not syn_rcvd\n", preconn.state);
                                }
                            }
                        }else{
                            // receive syn when preconn already created
                            // duplicate message ? ignore
                            fmt.Printf("[Info] receive duplicate syn from %s, wee need ack from cli\n",addr.String())
                        }
                    }
                } else {
                    fmt.Printf("[Info] receive ctrl message with extra data! ignore\n")
                }
            }

        }
    }
}
func sender(conn *net.UDPConn, msg_send_chan chan MessageChanData){

    fmt.Println("[Info] sender Start")

    for{
        select{
        case message_chan := <-msg_send_chan:
            var message *Message
            message = message_chan.message
            msg_buf := make([]byte, message.length)
            //fmt.Printf("[sender] seq = %d syn = %d\n", message.seq, message.syn)
            //fmt.Printf("sender message.length = %d\n", message.length)
            message.ConvertToBuffer(msg_buf[:])
            for{
                n,err := conn.WriteTo(msg_buf[:], message_chan.addr)
                if(err != nil){
                    fmt.Println(err)
                    break
                }
                //fmt.Printf("Send message success Length %d\n", n)
                if( n < len(msg_buf)){
                    msg_buf = msg_buf[n:]
                }else
                {
                    break;
                }
            }

        }

    }

}

