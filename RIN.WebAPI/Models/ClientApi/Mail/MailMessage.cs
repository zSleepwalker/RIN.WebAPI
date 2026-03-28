namespace RIN.WebAPI.Controllers;

public class MailMessage
{
    public uint id { get; set; }
    public string subject { get; set; } = null!;
    public ulong sender_guid { get; set; }
    public string sender_name { get; set; } = null!;
    public string body { get; set; } = null!;
    public bool unread { get; set; }
    public string mail_type { get; set; } = null!;
    public uint created_at { get; set; }
    public uint attachment_count { get; set; }
    public MailAttachments[] attachments { get; set; } = System.Array.Empty<MailAttachments>();
}