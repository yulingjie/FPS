import {
    "net"
}

type Connection struct{
    addr net.Addr;
    ringBuffer *RingBuffer;
}


