namespace RIN.WebAPI.Controllers;

public class MailMessage
{
    public uint id { get; set; }
    public string subject { get; set; }
    public ulong sender_guid { get; set; }
    public string sender_name { get; set; }
    public string body { get; set; }
    public bool unread { get; set; }
    public string mail_type { get; set; }
    public uint created_at { get; set; }
    public uint attachment_count { get; set; }
    public MailAttachments[] attachments { get; set; }
}