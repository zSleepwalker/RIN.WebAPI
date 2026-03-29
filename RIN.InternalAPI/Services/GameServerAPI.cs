using System.Text.Json;
using Grpc.Core;
using Npgsql;
using RIN.Core.DB;
using RIN.Core.Models;
using RIN.InternalAPI.Models;

namespace RIN.InternalAPI.Services
{
    public class GameServerAPI : IGameServerAPI
    {
        private readonly DB DB;
        private readonly ILogger<GameServerAPI> Logger;

        public GameServerAPI(DB db, ILogger<GameServerAPI> logger)
        {
            DB     = db;
            Logger = logger;
        }

        public ValueTask<PingResp> Ping(PingReq req)
        {
            var resp = new PingResp
            {
                ClientSentTime   = req.SentTime,
                ServerReciveTime = DateTime.UtcNow
            };

            return new ValueTask<PingResp>(resp);
        }

        public async ValueTask<CharacterAndBattleframeVisuals> GetCharacterAndBattleframeVisuals(CharacterID req)
        {
            var result    = await DB.GetBasicCharacterAndVisualData(req.ID);
            var bfVisuals = PlayerBattleframeVisuals.CreateDefault();

            var resp = new CharacterAndBattleframeVisuals
            {
                CharacterInfo      = result.Item1,
                CharacterVisuals   = result.Item2,
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

            return resp;
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
            await using var connection = new NpgsqlConnection(DB.ConnStr);
            await connection.OpenAsync();

            await using var cmd = new NpgsqlCommand("LISTEN events", connection);
            await cmd.ExecuteNonQueryAsync();

            connection.Notification += async (_, e) =>
            {
                var dbEvent = e.Payload.Split(["->"], StringSplitOptions.None);
                var eventType = dbEvent[0];
                var payloadJson = dbEvent[1];

                var type = Type.GetType($"RIN.InternalAPI.Models.{eventType}");

                if (type == null)
                {
                    Logger.LogError("Unknown event type: {eventType}", eventType);
                    return;
                }

                var payload = JsonSerializer.Deserialize(payloadJson, type);

                if (payload is not Event evt)
                {
                    Logger.LogError("Failed to deserialize payload for event type: {eventType}", eventType);
                    return;
                }

                if (evt is CharacterVisualsUpdated cvu)
                {
                    var updatedVisuals = await GetCharacterAndBattleframeVisuals(
                        new CharacterID { ID = (long)cvu.CharacterGuid });

                    cvu.CharacterAndBattleframeVisuals = updatedVisuals;

                    await events.WriteAsync(cvu);
                }
                else
                {
                    await events.WriteAsync(evt);
                }
            };

            var commandsTask = Task.Run(async () =>
            {
                await foreach (var command in commands.ReadAllAsync())
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
                    }
                }
            });

            while (!context.CancellationToken.IsCancellationRequested)
            {
                await connection.WaitAsync(context.CancellationToken);
            }

            await commandsTask;
        }
    }
}
