using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using RIN.Core.Models.ClientApi;

namespace RIN.Core.DB
{
    public partial class DB
    {
        public async Task<IEnumerable<MailMessageDto>> GetMailForCharacter(long characterGuid)
        {
            const string SQL = @"
                SELECT 
                    m.id, m.subject, m.sender_guid, m.sender_name, m.body, 
                    NOT m.is_read as unread, m.mail_type, 
                    EXTRACT(EPOCH FROM m.created_at)::int as created_at
                FROM webapi.""MailMessages"" m
                WHERE m.character_guid = @characterGuid
                ORDER BY m.created_at DESC;";

            var messages = await DBCall(conn => conn.QueryAsync<MailMessageDto>(SQL, new { characterGuid }));
            
            if (messages == null) return Enumerable.Empty<MailMessageDto>();

            var messageList = messages.ToList();
            foreach (var msg in messageList)
            {
                msg.attachments = await GetMailAttachments(msg.id);
                msg.attachment_count = (uint)msg.attachments.Count();
            }

            return messageList;
        }

        public async Task<IEnumerable<MailAttachmentsDto>> GetMailAttachments(long messageId)
        {
            const string SQL = @"
                SELECT id, item_sdb_id, item_guid as item_id, quantity, claimed
                FROM webapi.""MailAttachments""
                WHERE message_id = @messageId;";

            return await DBCall(conn => conn.QueryAsync<MailAttachmentsDto>(SQL, new { messageId })) 
                   ?? Enumerable.Empty<MailAttachmentsDto>();
        }

        public async Task<bool> MarkMailAsRead(long characterGuid, IEnumerable<long> messageIds)
        {
            const string SQL = @"
                UPDATE webapi.""MailMessages""
                SET is_read = true
                WHERE character_guid = @characterGuid AND id = ANY(@messageIds);";

            var result = await DBCall(conn => conn.ExecuteAsync(SQL, new { characterGuid, messageIds = messageIds.ToArray() }));
            return result > 0;
        }

        public async Task<bool> DeleteMail(long characterGuid, IEnumerable<long> messageIds)
        {
            const string SQL = @"
                DELETE FROM webapi.""MailMessages""
                WHERE character_guid = @characterGuid AND id = ANY(@messageIds);";

            var result = await DBCall(conn => conn.ExecuteAsync(SQL, new { characterGuid, messageIds = messageIds.ToArray() }));
            return result > 0;
        }

        public async Task<long> SendMail(long characterGuid, long senderGuid, string senderName, string subject, string body, string mailType = "system_message")
        {
            const string SQL = @"SELECT webapi.""SendMail""(@characterGuid, @senderGuid, @senderName, @subject, @body, @mailType);";
            
            return await DBCall(conn => conn.QuerySingleAsync<long>(SQL, new { characterGuid, senderGuid, senderName, subject, body, mailType }));
        }

        public async Task AddMailAttachment(long messageId, int sdbId, long? itemGuid = null, int quantity = 1)
        {
            const string SQL = @"SELECT webapi.""AddMailAttachment""(@messageId, @sdbId, @itemGuid, @quantity);";
            await DBCall(conn => conn.ExecuteAsync(SQL, new { messageId, sdbId, itemGuid, quantity }));
        }

        public async Task<bool> ClaimMailAttachment(long characterGuid, long messageId, long attachmentId)
        {
            // 1. Get attachment info and verify ownership
            const string VERIFY_SQL = @"
                SELECT a.id, a.item_sdb_id, a.item_guid, a.quantity, a.claimed
                FROM webapi.""MailAttachments"" a
                JOIN webapi.""MailMessages"" m ON a.message_id = m.id
                WHERE m.character_guid = @characterGuid AND m.id = @messageId AND a.id = @attachmentId;";

            var attachment = await DBCall(conn => conn.QueryFirstOrDefaultAsync<MailAttachmentsDto>(VERIFY_SQL, new { characterGuid, messageId, attachmentId }));

            if (attachment == null || attachment.claimed)
            {
                return false;
            }

            int sdbId = attachment.item_sdb_id;
            int quantity = attachment.quantity;
            long? itemGuid = attachment.item_id;

            // 2. Add to inventory
            if (itemGuid != null && itemGuid > 0)
            {
                // Unique item (we assume it's already created or we just move the GUID? 
                // Usually for mail attachments they are created on claim or pre-created.
                // If it's a pre-created item_guid, we just need to re-assign it to the character?
                // Actually, the current AddCharacterItem creates a new GUID.
                // If the mail has a pre-defined GUID, we should probably use a version of AddCharacterItem that takes a GUID.
                
                // For now, let's assume if item_guid is set, it's a unique item.
                // We'll create a new one for simplicity if it doesn't exist, OR we should have a 'move' logic.
                await AddCharacterItem(characterGuid, sdbId); 
            }
            else
            {
                // Stackable resource
                await AddOrUpdateCharacterResource(characterGuid, sdbId, quantity);
            }

            // 3. Mark as claimed
            const string CLAIM_SQL = @"UPDATE webapi.""MailAttachments"" SET claimed = true WHERE id = @attachmentId;";
            await DBCall(conn => conn.ExecuteAsync(CLAIM_SQL, new { attachmentId }));

            return true;
        }

        public async Task SendWelcomeMail(long characterGuid)
        {
            // Message 1: Red 5 Studios - Free Gifts
            string body1 = @"Hello,

Last night we released a new patch to Firefall that provided many new updates to the game including; more content, the first story campaign missions, new progression changes, and more. As part of this update, the way gear and equipment is calculated changed requiring us to mark your old gear as un-equippable. We provided temporary gear that would be appropriate for your battleframes, but this gear had constraint values that were scaled too high for the quality of the equipment making it difficult to equip your battleframes.

The Accord are delivering new crates of equipment to you and should be available via the Calldown section of your inventory. If you are tier 3 or 4, this new gear crate will have two sets of equipment at different qualities so that you can mix and match.

We also realize that many of you have spent large amounts of time and resources to craft the gear that is now obsolete. As our way of saying thank you for your understanding and patience with these changes during our Beta process, we've attached a few rewards to this message. These include:

   - A 7-day VIP package giving you access to crystite bonuses, additional Marketplace slots, and more Workbenches for crafting.

   - A stack of 5 1-hour 20% resource boosts that you can apply to rebuild your stockpiles.

The attached items will appear in your inventory after clicking the Redeem All button below.

You are important to us and we want to thank you for participating in the Firefall Beta and your patience while we work towards making Firefall the best game it can be.

Sincerely,

The Red 5 Tribe";

            long mail1 = await SendMail(characterGuid, 0, "Red 5 Studios", "It's been a crazy day - Free gifts inside", body1);
            await AddMailAttachment(mail1, 86360, 1, 1); // VIP Package
            for (int i = 0; i < 5; i++)
            {
                await AddMailAttachment(mail1, 81361, 1, 1); // Resource boosts
            }

            // Message 2: Valentine's Day
            string body2 = @"Happy Valentine's Day! 

Thank you for joining us in New Eden, today! Please accept this gift of 5 candy heart consumables. You can use them to ""show the love"" to other players, giving them a 1 hour boost to XP.

With Love,
The Firefall Dev Team
";
            long mail2 = await SendMail(characterGuid, 0, "Red 5 Studios", "Happy Valentine's Day!", body2);
            await AddMailAttachment(mail2, 85824, 0, 5); // Candy Hearts (Stackable resource)
        }
    }

    // DTOs to match the expected API format
    public class MailMessageDto
    {
        public long id { get; set; }
        public string subject { get; set; } = null!;
        public ulong sender_guid { get; set; }
        public string sender_name { get; set; } = null!;
        public string body { get; set; } = null!;
        public bool unread { get; set; }
        public string mail_type { get; set; } = null!;
        public uint created_at { get; set; }
        public uint attachment_count { get; set; }
        public IEnumerable<MailAttachmentsDto> attachments { get; set; } = Enumerable.Empty<MailAttachmentsDto>();
    }

    public class MailAttachmentsDto
    {
        public long id { get; set; }
        public int item_sdb_id { get; set; }
        public long? item_id { get; set; }
        public int quantity { get; set; }
        public bool claimed { get; set; }
    }
}
