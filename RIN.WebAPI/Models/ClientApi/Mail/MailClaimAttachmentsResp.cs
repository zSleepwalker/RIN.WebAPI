namespace RIN.WebAPI.Controllers;

public class MailClaimAttachmentsResp
{
    public long id { get; set; }
    public uint attachment_count { get; set; }
    public MailAttachments[] attachments { get; set; }
}