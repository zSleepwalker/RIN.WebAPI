using ProtoBuf;

namespace RIN.InternalAPI.Models
{
    [ProtoContract]
    public class PingReq
    {
        [ProtoMember(1)]
        public long SentTime { get; set; }
    }

    [ProtoContract]
    public class PingResp
    {
        [ProtoMember(1)]
        public long ClientSentTime { get; set; }

        [ProtoMember(2)]
        public long ServerReciveTime { get; set; }
    }
}
