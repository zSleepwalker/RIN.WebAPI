namespace RIN.WebAPI.Controllers;

public class Mail
{
    public uint count { get; set; }
    public MailMessage[] results { get; set; } = System.Array.Empty<MailMessage>();
}