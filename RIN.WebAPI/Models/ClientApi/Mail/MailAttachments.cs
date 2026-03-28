namespace RIN.WebAPI.Controllers;

public class MailAttachments
{
    public uint item_sdb_id { get; set; }
    public ulong? item_id { get; set; }
    public uint quantity { get; set; }
    public bool claimed { get; set; }
}