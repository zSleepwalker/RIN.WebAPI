using System;

namespace RIN.WebAPI.Models.ClientApi
{
    public class CreateAccountReq
    {
        public string referral_key { get; set; } = null!;
        public string email        { get; set; } = null!;
        public bool   email_optin  { get; set; }
        public string password     { get; set; } = null!;
        public string country      { get; set; } = null!;
        public string birthday     { get; set; } = null!;
        
        
        public string? steam_session_ticket { get; set; }
        public string? steam_user_id        { get; set; }
        public string? steam_cdkey          { get; set; }
    }
}