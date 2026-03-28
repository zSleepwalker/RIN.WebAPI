namespace RIN.WebAPI.Controllers;

public class MailCheck
{
    public string recipient { get; set; } = String.Empty;
    public long recipient_guid { get; set; } = 0;
}