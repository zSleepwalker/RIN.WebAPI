using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using RIN.InternalAPI.Models;

namespace RIN.InternalAPI
{
    [Service("RIN.GameServerAPI")]
    public interface IGameServerAPI
    {
        public ValueTask<PingResp> Ping(PingReq req);
        public ValueTask<CharacterAndBattleframeVisuals> GetCharacterAndBattleframeVisuals(CharacterID req);
        public ValueTask<CharacterInventoryResponse> GetCharacterInventory(CharacterID req);
        public ValueTask<ConsumeResourceResp> ConsumeCharacterResource(ConsumeResourceReq req);
        public Task Stream(IAsyncStreamReader<Command> commands, IServerStreamWriter<Event> events, ServerCallContext context);
    }
}
