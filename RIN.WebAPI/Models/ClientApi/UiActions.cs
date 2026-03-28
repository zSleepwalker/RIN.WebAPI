namespace RIN.WebAPI.Models.ClientApi;

public class UiActions
{
    public uint   screen_reference_id { get; set; }
    public string screen { get; set; } = null!;
    public string action { get; set; } = null!;
}