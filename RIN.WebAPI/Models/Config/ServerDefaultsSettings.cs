namespace RIN.WebAPI.Models.Config
{
    public class ServerDefaultsSettings
    {
        public const string NAME = "ServerDefaults";
            
        public int CharacterLimitPerAccount { get; set; } = 2;
        public int CharacterNameMaxLength   { get; set; } = 40;
        public int CharacterNameMinLength   { get; set; } = 1;
    }
}