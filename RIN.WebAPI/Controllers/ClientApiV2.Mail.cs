using Microsoft.AspNetCore.Mvc;
using RIN.Core;
using RIN.WebAPI.Models.ClientApi;
using RIN.WebAPI.Utils;

namespace RIN.WebAPI.Controllers;


public partial class ClientApiV2
{
    [HttpGet("characters/{characterId}/mail")]
    [R5SigAuthRequired]
    public Mail GetMail([FromQuery] uint page = 1, long characterId = 0)
    {
        if (characterId <= 0)
            return new Mail();
        
        var mail = new Mail()
        {
            count = 2,
            results = new[]
            {
                new MailMessage
                {
                    id = 373321,
                    subject = "It's been a crazy day - Free gifts inside",
                    sender_guid = 0,
                    sender_name = "Red 5 Studios",
                    body = "Hello,\n\nLast night we released a new patch to Firefall that provided many new updates to the game including; more content, the first story campaign missions, new progression changes, and more. As part of this update, the way gear and equipment is calculated changed requiring us to mark your old gear as un-equippable. We provided temporary gear that would be appropriate for your battleframes, but this gear had constraint values that were scaled too high for the quality of the equipment making it difficult to equip your battleframes.\n\nThe Accord are delivering new crates of equipment to you and should be available via the Calldown section of your inventory. If you are tier 3 or 4, this new gear crate will have two sets of equipment at different qualities so that you can mix and match.\n\nWe also realize that many of you have spent large amounts of time and resources to craft the gear that is now obsolete. As our way of saying thank you for your understanding and patience with these changes during our Beta process, we've attached a few rewards to this message. These include:\n\n   - A 7-day VIP package giving you access to crystite bonuses, additional Marketplace slots, and more Workbenches for crafting.\n\n   - A stack of 5 1-hour 20% resource boosts that you can apply to rebuild your stockpiles.\n\nThe attached items will appear in your inventory after clicking the Redeem All button below.\n\nYou are important to us and we want to thank you for participating in the Firefall Beta and your patience while we work towards making Firefall the best game it can be.\n\nSincerely,\n\nThe Red 5 Tribe",
                    unread = true,
                    mail_type = "system_message",
                    created_at = 1391247422,
                    attachment_count = 6,
                    attachments = new[]
                    {
                        new MailAttachments { item_sdb_id = 86360, item_id = 9160962539553639932, quantity = 1, claimed = false },
                        new MailAttachments { item_sdb_id = 81361, item_id = 9160962539553639933, quantity = 1, claimed = false },
                        new MailAttachments { item_sdb_id = 81361, item_id = 9157318587727971069, quantity = 1, claimed = false },
                        new MailAttachments { item_sdb_id = 81361, item_id = 9177587098021734397, quantity = 1, claimed = false },
                        new MailAttachments { item_sdb_id = 81361, item_id = 9168393176584511997, quantity = 1, claimed = false },
                        new MailAttachments { item_sdb_id = 81361, item_id = 9164439237885112061, quantity = 1, claimed = false }
                    }
                },
                new MailMessage
                {
                    id = 3453221,
                    subject = "Happy Valentine's Day!",
                    sender_guid = 0,
                    sender_name = "Red 5 Studios",
                    body = "Happy Valentine's Day! \r\n\r\nThank you for joining us in New Eden, today! Please accept this gift of 5 candy heart consumables. You can use them to \"show the love\" to other players, giving them a 1 hour boost to XP.\r\n\r\nWith Love,\r\nThe Firefall Dev Team\r\n",
                    unread = false,
                    mail_type = "system_message",
                    created_at = 1392395174,
                    attachment_count = 1,
                    attachments = new[]
                    {
                        new MailAttachments { item_sdb_id = 85824, item_id = 9160962539553639930, quantity = 5, claimed = false }
                    }
                }
            }
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
    public object ClaimMailAttachments(long characterId = 0, long messageId = 0)
    {
        if (characterId <= 0 || characterId != GetCid())
            return ReturnError(
                Error.Codes.ERR_UNKNOWN,
                "Access denied",
                StatusCodes.Status401Unauthorized
            );
        
        if (messageId <= 0 || !isMailOwner(characterId, messageId))
            return ReturnError(
                Error.Codes.ERR_UNKNOWN,
                "Bad request data",
                StatusCodes.Status400BadRequest
            );
        
        /* TODO:
         * Verify Attachments to database values - report if modified
         * Claim all attachments
         * Get attachment info from database
         * Add to user inventory
         * Remove attachment from mail 
         */
        if (messageId == 373321)
        {
            return new MailClaimAttachmentsResp()
            {
                id = messageId,
                attachment_count = 1,
                attachments = new[]
                {
                    new MailAttachments { item_sdb_id = 86360, item_id = 9160962539553639932, quantity = 1, claimed = false },
                    new MailAttachments { item_sdb_id = 81361, item_id = 9160962539553639933, quantity = 1, claimed = false },
                    new MailAttachments { item_sdb_id = 81361, item_id = 9157318587727971069, quantity = 1, claimed = false },
                    new MailAttachments { item_sdb_id = 81361, item_id = 9177587098021734397, quantity = 1, claimed = false },
                    new MailAttachments { item_sdb_id = 81361, item_id = 9168393176584511997, quantity = 1, claimed = false },
                    new MailAttachments { item_sdb_id = 81361, item_id = 9164439237885112061, quantity = 1, claimed = false }
                }
            };
        }
        
        if (messageId == 3453221)
        {
            return new MailClaimAttachmentsResp()
            {
                id = messageId,
                attachment_count = 1,
                attachments = new[]
                {
                    new MailAttachments { item_sdb_id = 85824, item_id = 9160962539553639930, quantity = 5, claimed = true }
                }
            };
        }

        return new MailClaimAttachmentsResp();
    }
    
    [HttpPost("characters/{characterId}/mail/batch/delete")]
    [R5SigAuthRequired]
    public object BatchDeleteMail(MailMarkRead messageIds, long characterId = 0)
    {
        if (characterId <= 0)
            return ReturnError(
                Error.Codes.ERR_UNKNOWN,
                "You do not have permission to kick members from this army.",
                StatusCodes.Status403Forbidden
            );
        
        return Ok();
    }
    
    [HttpPost("characters/{characterId}/mail/{messageId}/delete")]
    [R5SigAuthRequired]
    public object DeleteMail(long characterId = 0, long messageId = 0)
    {
        if (characterId  <= 0 || messageId <= 0)
            return Ok();
        
        /* TODO:
         * Remove mail
         * If attachments remove/claim?
         * Mark mail for removal
         */
        
        return Ok();
    }
    
    [HttpPost("characters/{characterId}/mail/batch/mark_read")]
    [R5SigAuthRequired]
    public bool BatchMarkMailRead(MailMarkRead messageIds, long characterId = 0)
    {
        if (characterId <= 0)
            return false;
        
        /*
         * UPDATE mailbox SET is_read = true WHERE id = any(array[1, 2, 3])
         */
        
        return true;
    }
    
    [HttpPost("characters/{characterId}/mail/{messageId}/mark_read")]
    [R5SigAuthRequired]
    public bool MarkMailRead(long characterId = 0, long messageId = 0)
    {
        if (characterId  <= 0 || messageId <= 0)
            return false;
        
        /*
         * UPDATE mailbox SET is_read = true WHERE id = 1)
         */
        
        return true;
    }

    public bool isMailOwner(long characterId, long messageId)
    {
        /*
         * SELECT COUNT(id) FROM mailbox WHERE mid = messageId AND cid = characterId
         *
         * if (numResults = 1)
         *     return true
         *
         * return false
         */
        
        return true;
    }
}