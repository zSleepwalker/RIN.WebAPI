using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RIN.Core;
using RIN.Core.DB;
using RIN.Core.Models;
using RIN.WebAPI.Models.ClientApi;
using RIN.WebAPI.Utils;
using System.Linq;
using System.Threading.Tasks;

namespace RIN.WebAPI.Controllers;


public partial class ClientApiV2
{
    [HttpGet("characters/{characterId}/mail")]
    [R5SigAuthRequired]
    public async Task<Mail> GetMail([FromQuery] uint page = 1, long characterId = 0)
    {
        if (characterId <= 0)
            return new Mail();

        var dbMessages = await Db.GetMailForCharacter(characterId);
        
        var mail = new Mail()
        {
            count = (uint)dbMessages.Count(),
            results = dbMessages.Select(m => new MailMessage
            {
                id = (uint)m.id,
                subject = m.subject,
                sender_guid = m.sender_guid,
                sender_name = m.sender_name,
                body = m.body,
                unread = m.unread,
                mail_type = m.mail_type,
                created_at = m.created_at,
                attachment_count = m.attachment_count,
                attachments = m.attachments.Select(a => new MailAttachments
                {
                    item_sdb_id = (uint)a.item_sdb_id,
                    item_id = (ulong?)a.item_id,
                    quantity = (uint)a.quantity,
                    claimed = a.claimed
                }).ToArray()
            }).ToArray()
        }; 
        
        return mail;
    }

    [HttpPost("characters/{characterId}/mail/mail_check")]
    [R5SigAuthRequired]
    public bool MailCheck(MailCheck check, long characterId = 0)
    {
        /*
        if (characterId <= 0 || string.IsNullOrEmpty(recipient) || recipient_guid <= 0)
            return new Mail();
        */

        return true;
    }
    
    [HttpPost("characters/{characterId}/mail/{messageId}/claim_attachments")]
    [R5SigAuthRequired]
    public async Task<object> ClaimMailAttachments(long characterId = 0, long messageId = 0)
    {
        if (characterId <= 0 || characterId != GetCid())
            return ReturnError(
                Error.Codes.ERR_UNKNOWN,
                "Access denied",
                StatusCodes.Status401Unauthorized
            );
        
        if (messageId <= 0 || !await isMailOwner(characterId, messageId))
            return ReturnError(
                Error.Codes.ERR_UNKNOWN,
                "Bad request data",
                StatusCodes.Status400BadRequest
            );
        
        var attachments = await Db.GetMailAttachments(messageId);
        var claimedList = new List<MailAttachments>();

        foreach (var attachment in attachments)
        {
            if (!attachment.claimed)
            {
                var success = await Db.ClaimMailAttachment(characterId, messageId, attachment.id);
                if (success)
                {
                    claimedList.Add(new MailAttachments
                    {
                        item_sdb_id = (uint)attachment.item_sdb_id,
                        item_id = (ulong?)attachment.item_id,
                        quantity = (uint)attachment.quantity,
                        claimed = true
                    });
                }
            }
        }

        return new MailClaimAttachmentsResp()
        {
            id = messageId,
            attachment_count = (uint)claimedList.Count,
            attachments = claimedList.ToArray()
        };
    }
    
    [HttpPost("characters/{characterId}/mail/batch/delete")]
    [R5SigAuthRequired]
    public async Task<object> BatchDeleteMail(MailMarkRead messageIds, long characterId = 0)
    {
        if (characterId <= 0)
            return ReturnError(
                Error.Codes.ERR_UNKNOWN,
                "You do not have permission to delete these emails.",
                StatusCodes.Status403Forbidden
            );
        
        await Db.DeleteMail(characterId, messageIds.message_ids.Select(id => (long)id));
        
        return Ok();
    }
    
    [HttpPost("characters/{characterId}/mail/{messageId}/delete")]
    [R5SigAuthRequired]
    public async Task<object> DeleteMail(long characterId = 0, long messageId = 0)
    {
        if (characterId  <= 0 || messageId <= 0)
            return Ok();
        
        await Db.DeleteMail(characterId, new[] { messageId });
        
        return Ok();
    }
    
    [HttpPost("characters/{characterId}/mail/batch/mark_read")]
    [R5SigAuthRequired]
    public async Task<bool> BatchMarkMailRead(MailMarkRead messageIds, long characterId = 0)
    {
        if (characterId <= 0)
            return false;
        
        return await Db.MarkMailAsRead(characterId, messageIds.message_ids.Select(id => (long)id));
    }
    
    [HttpPost("characters/{characterId}/mail/{messageId}/mark_read")]
    [R5SigAuthRequired]
    public async Task<bool> MarkMailRead(long characterId = 0, long messageId = 0)
    {
        if (characterId  <= 0 || messageId <= 0)
            return false;
        
        return await Db.MarkMailAsRead(characterId, new[] { messageId });
    }

    public async Task<bool> isMailOwner(long characterId, long messageId)
    {
        var mail = await Db.GetMailForCharacter(characterId);
        return mail.Any(m => m.id == messageId);
    }
}