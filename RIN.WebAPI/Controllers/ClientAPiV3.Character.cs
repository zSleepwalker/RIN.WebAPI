using Microsoft.AspNetCore.Mvc;
using RIN.WebAPI.Models;
using RIN.WebAPI.Models.ClientApi;
using RIN.Core.Common;
using RIN.WebAPI.Utils;
using RIN.Core.ClientApi;
using System.Drawing;
using System.Security.Cryptography.Xml;

namespace RIN.WebAPI.Controllers
{
    public partial class ClientAPiV3
    {
        [HttpGet("characters/{characterId}/army_invites")]
        public async Task<List<CharacterArmyApplication>> ArmyInvites(long characterId)
        {
            var armyInvites = await Db.ArmyCharacterApplications(characterId, true);
            
            return armyInvites;
        }
        
        [HttpGet("characters/{characterId}/army_applications")]
        public async Task<List<CharacterArmyApplication>> ArmyApplications(long characterId)
        {
            var armyApplication = await Db.ArmyCharacterApplications(characterId, false);
            
            return armyApplication;
        }
    }
}
