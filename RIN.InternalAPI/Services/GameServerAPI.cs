using System.Threading.Channels;
using Grpc.Core;
using RIN.Core.DB;
using RIN.Core.Models;
using RIN.InternalAPI.Models;

namespace RIN.InternalAPI.Services
{
    public class GameServerAPI : IGameServerAPI
    {
        private readonly DB DB;
        private readonly ILogger<GameServerAPI> Logger;
        private readonly DbEventBus EventBus;
        private readonly IHostApplicationLifetime Lifetime;

        public GameServerAPI(DB db, ILogger<GameServerAPI> logger, DbEventBus eventBus, IHostApplicationLifetime lifetime)
        {
            DB     = db;
            Logger = logger;
            EventBus = eventBus;
            Lifetime = lifetime;
        }

        public async ValueTask<ApplyCharacterBoostResp> ApplyCharacterBoost(ApplyCharacterBoostReq req)
        {
            var success = await DB.AddOrExtendBoost((long)req.CharacterId, req.BoostType, req.Modifier, (int)req.DurationSeconds);
            return new ApplyCharacterBoostResp { Success = success };
        }

        public ValueTask<PingResp> Ping(PingReq req)
        {
            var resp = new PingResp
            {
                ClientSentTime   = req.SentTime,
                ServerReciveTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            return new ValueTask<PingResp>(resp);
        }

        public async ValueTask<CharacterAndBattleframeVisuals> GetCharacterAndBattleframeVisuals(CharacterID req)
        {
            var result    = await DB.GetBasicCharacterAndVisualData(req.ID);
            var bfVisuals = PlayerBattleframeVisuals.CreateDefault();

            var resp = new CharacterAndBattleframeVisuals
            {
                CharacterInfo      = result.info,
                CharacterVisuals   = result.visuals,
                BattleframeVisuals = bfVisuals
            };

            return resp;
        }

        public async ValueTask<CharacterInventoryResponse> GetCharacterInventory(CharacterID req)
        {
            var dbInventory = await DB.GetCharacterInventory(req.ID);
            var dbLoadouts  = await DB.GetCharacterLoadouts(req.ID);
            var resp = new CharacterInventoryResponse();

            foreach (var item in dbInventory.items)
            {
                resp.Items.Add(new CharacterItem
                {
                    Guid = (ulong)item.item_guid,
                    SdbId = (uint)item.sdb_id
                });
            }

            foreach (var resource in dbInventory.resources)
            {
                resp.Resources.Add(new CharacterResource
                {
                    SdbId = (uint)resource.sdb_id,
                    Quantity = (uint)resource.quantity
                });
            }

            foreach (var loadout in dbLoadouts)
            {
                resp.Loadouts.Add(new CharacterLoadout
                {
                    LoadoutId = loadout.LoadoutId,
                    ChassisSdbId = loadout.ChassisSdbId,
                    Visuals = loadout.Visuals,
                    SlottedItems = loadout.SlottedItems
                });
            }

            foreach (var unlock in dbInventory.unlocks)
            {
                resp.Unlocks.Add(new CharacterUnlockEntry
                {
                    UnlockType = unlock.unlock_type,
                    UnlockId = (uint)unlock.unlock_id,
                    FrameId = (uint)Math.Max(unlock.frame_id, 0),
                });
            }

            return resp;
        }

        public async ValueTask<AddCharacterItemResp> AddCharacterItem(AddCharacterItemReq req)
        {
            var guid = await DB.AddCharacterItem((long)req.CharacterId, (int)req.SdbId);
            return new AddCharacterItemResp
            {
                Success = guid != 0,
                Guid = (ulong)guid,
            };
        }

        public async ValueTask<ConsumeResourceResp> ConsumeCharacterResource(ConsumeResourceReq req)
        {
            var success = await DB.ConsumeCharacterResource((long)req.CharacterId, (int)req.SdbId, (int)req.Quantity);
            return new ConsumeResourceResp { Success = success };
        }

        public async ValueTask<ConsumeItemResp> ConsumeCharacterItem(ConsumeItemReq req)
        {
            var success = await DB.ConsumeCharacterItem((long)req.CharacterId, (int)req.SdbId, (int)req.Quantity);
            return new ConsumeItemResp { Success = success };
        }

        public async Task Stream(IAsyncStreamReader<Command> commands, IServerStreamWriter<Event> events, ServerCallContext context)
        {
            var channel = Channel.CreateUnbounded<Event>();
            var subscriptionId = EventBus.Subscribe(channel);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, Lifetime.ApplicationStopping);
            var token = cts.Token;
            try
            {
                var sendEventsTask = Task.Run(async () =>
                {
                    await foreach (var evt in channel.Reader.ReadAllAsync(token))
                    {
                        await events.WriteAsync(evt);
                    }
                });

                var commandsTask = Task.Run(async () =>
                {
                    await foreach (var command in commands.ReadAllAsync(token))
                    {
                        Logger.LogInformation("Received command: {command}", command);

                        switch (command)
                        {
                            case SaveGameSessionData data:
                                await DB.UpdateCharacterAfterGameSession((long)data.CharacterId, (int)data.ZoneId, (int)data.OutpostId, (int)data.TimePlayed);
                                break;
                            case SaveLgvRaceFinish race:
                                await DB.SaveLgvRaceFinish((long)race.CharacterGuid, (int)race.LeaderboardId, (long)race.TimeMs);
                                break;
                            case SaveCharacterLoadout loadout:
                                await DB.SaveCharacterLoadout((long)loadout.CharacterGuid, loadout.LoadoutId, loadout.ChassisSdbId, loadout.VisualsJson, loadout.SlottedItemsJson);
                                break;
                            case SaveCharacterUnlock unlock:
                                await DB.UpsertCharacterUnlock((long)unlock.CharacterGuid, unlock.UnlockType, (int)unlock.UnlockId, (int)unlock.FrameId);
                                break;
                        }
                    }
                });

                await Task.WhenAny(sendEventsTask, commandsTask);
                await cts.CancelAsync();
                await Task.WhenAll(sendEventsTask, commandsTask);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogError(ex, "GRPC Stream crashed");
            }
            finally
            {
                channel.Writer.TryComplete();
                EventBus.Unsubscribe(subscriptionId);
            }
        }
    }
}
