using ProtoBuf;

namespace RIN.InternalAPI.Models
{
    [ProtoContract]
    public class PingReq
    {
        [ProtoMember(1)]
        public DateTime SentTime { get; set; }
    }

    [ProtoContract]
    public class PingResp
    {
        [ProtoMember(1)]
        public DateTime ClientSentTime { get; set; }

        [ProtoMember(2)]
        public DateTime ServerReciveTime { get; set; }
    }
}
