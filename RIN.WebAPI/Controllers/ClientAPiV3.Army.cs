using System.Text.Json;
using System.Text.RegularExpressions;
using System.Transactions;
using Microsoft.AspNetCore.Mvc;
using RIN.Core;
using RIN.Core.ClientApi;
using RIN.Core.DB;
using RIN.WebAPI.Utils;

namespace RIN.WebAPI.Controllers
{
    public partial class ClientAPiV3
    {
        private static short armyMinSize      = 1;
        private static short armyMaxSize      = 50;
        private static short armyQueryMinSize = 1;
        private static short armyQueryMaxSize = 100;

        /// <summary>
        /// Create army, default army ranks, and add owner to the army
        /// </summary>
        [HttpPost("armies")]
        [R5SigAuthRequired]
        public async Task<Error> ArmyCreate([FromBody] CreateArmy args)
        {
            var dateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            if(args.name.Length < 3)
                return new Error() { code = Error.Codes.ERR_NAME_TOO_SHORT, message = "Army name requires more than 3 letters." };
            
            if(args.name.Length > 20)
                return new Error() { code = Error.Codes.ERR_NOT_ENOUGH_LETTERS, message = "Army name requires less than 20 letters." };
            
            if(!Regex.Match(args.name, "^[a-zA-Z0-9]*$").Success)
                return new Error() { code = Error.Codes.ERR_INVALID_CHARACTER, message = "Valid characters for army name is A-Z and 0-9." };
            
            // Get character information
            var loginResult = await Db.GetLoginData(GetUid());
            var characterGuid = await Db.ActiveCharacterGuid(loginResult.account_id);
            
            // Verify that the character is not part of an army already
            if (await Db.ArmyIsMember(characterGuid))
                return new Error() { code = Error.Codes.ERR_UNKNOWN, message = "Can't craete new army while a member of one." };
            
            string uniqueName = args.name.ToUpper();
            
            // Verify that the name doesn't exist before continuing
            if (await Db.ArmyExists(args.name, uniqueName))
                return new Error() { code = Error.Codes.ERR_NAME_IN_USE, message = "Army name is in use. Please specify a different one." };
            
            using (var tx = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                // Create army
                var armyInfo = await Db.ArmyCreate(loginResult.account_id, dateTime, armyMinSize, armyMaxSize, args.name, uniqueName, args.description, args.playstyle, args.personality, args.is_recruiting, characterGuid, args.region, args.website);
                
                if (armyInfo.army_id < 1 || armyInfo.army_guid < 1)
                    return new Error() { code = Error.Codes.ERR_UNKNOWN, message = "Unknown Error. Failed to create army." };
                
                // Create ranks (owner + default)
                var ownerRankId = await Db.ArmyCreateOwnerRank(armyInfo.army_id, armyInfo.army_guid);
                var defaultRankId = await Db.ArmyAddRank(armyInfo.army_id, armyInfo.army_guid, "Soldier", true);
                
                if (ownerRankId < 1 || defaultRankId.code != "SUCCESS")
                    return new Error() { code = Error.Codes.ERR_UNKNOWN, message = "Unknown Error. Failed to create army ranks." };
                
                // Add owner to army and set owner rank
                var ownerMember = await Db.ArmyAddMember(armyInfo.army_id, armyInfo.army_guid, characterGuid, ownerRankId);
                
                if (ownerMember.code != "SUCCESS")
                    return new Error() { code = Error.Codes.ERR_UNKNOWN, message = "Unknown Error. Failed to add owner to army." };
                
                tx.Complete();
            }
            
            return new Error() { code = Error.Codes.SUCCESS };
        }
        
        /// <summary>
        /// List armies, with the option of searching
        /// </summary>
        [HttpGet("armies")]
        [R5SigAuthRequired]
        public async Task<Armies> ArmyList([FromQuery] Pagination args)
        {
            // TODO: Handle search query
            var armies = await Db.ArmyList(args.page, args.per_page);
            var armiesCount = armies.Count();
            var armiesResp = new Armies
            {
                page = args.page,
                total_count = armiesCount,
                results = (armiesCount > 0 ? armies : new List<Army>())
            };

            return armiesResp;
        }
        
        [HttpGet("armies/{armyGuid}")]
        [R5SigAuthRequired]
        public async Task<ArmyInfo> ArmyInfo(long armyGuid)
        {
            var army = await Db.ArmyInfo(armyGuid);
            var officerList = await Db.ArmyOfficers(armyGuid);
            var temp = army.TryUpdate(officerList);

            return army;
        }
        
        [HttpPut("armies/{armyGuid}")]
        [R5SigAuthRequired]
        public async Task<object> ArmySetInfo([FromBody] object args, long armyGuid)
        {
            Logger.LogError("Function [PUT]ArmySetInfo args: {args}", args.ToString());
            return true;
        }
        
        [HttpPut("armies/{armyGuid}/establish")]
        [R5SigAuthRequired]
        public async Task<object> ArmyEstablish([FromBody] object args, long armyGuid)
        {
            Logger.LogError("Function [PUT]ArmyEstablish args: {args}", args.ToString());

            // Get character information
            /*
            if(args.tag.Length < 2)
                return new Error() { code = Error.Codes.ERR_NAME_TOO_SHORT, message = "Army tag requires more than 2 letters." };
            
            if(args.tag.Length > 5)
                return new Error() { code = Error.Codes.ERR_NOT_ENOUGH_LETTERS, message = "Army tag requires less than 5 letters." };
            
            if(!Regex.Match(args.tag, "^[a-zA-Z0-9]*$").Success)
                return new Error() { code = Error.Codes.ERR_INVALID_CHARACTER, message = "Valid characters for army name is A-Z and 0-9." };
            var loginResult = await Db.GetLoginData(GetUid());
            var characterGuid = await Db.ActiveCharacterGuid(loginResult.account_id);
            var establishArmy = await Db.ArmyEstablish(armyGuid, characterGuid, args.tag);
            
            return establishArmy;
            */
            return true;
        }
        
        [HttpPut("armies/{armyGuid}/leave")]
        [R5SigAuthRequired]
        public async Task<object> ArmyLeave([FromBody] object args, long armyGuid)
        {
            Logger.LogError("Function [PUT]ArmyLeave args: {args}", args.ToString());
            return true;
        }
        
        [HttpPost("armies/{armyGuid}/step_down")]
        [R5SigAuthRequired]
        public async Task<object> ArmyStepDown([FromBody] object args, long armyGuid)
        {
            Logger.LogError("Function [POST]ArmyStepDown args: {args}", args.ToString());
            return true;
        }
        
        [HttpPut("armies/{armyGuid}/disband")]
        [R5SigAuthRequired]
        public async Task<Error> ArmyDisband([FromBody] object args, long armyGuid)
        {
            Logger.LogError("Function [PUT]ArmyDisband args: {args}", args.ToString());
            var loginResult = await Db.GetLoginData(GetUid());
            var characterGuid = await Db.ActiveCharacterGuid(loginResult.account_id);
            var dispandArmy = await Db.ArmyDisband(armyGuid, characterGuid);
            
            return dispandArmy;
        }
        
        [HttpGet("armies/{armyGuid}/ranks")]
        [R5SigAuthRequired]
        public async Task<List<ArmyRank>> ArmyRankList(long armyGuid)
        {
            var ranksList = await Db.ArmyRankList(armyGuid);

            return ranksList;
        }

        [HttpPost("armies/{armyGuid}/ranks")]
        [R5SigAuthRequired]
        public async Task<object> ArmyAddRank([FromBody] object args, long armyGuid)
        {
            Logger.LogError("Function [POST]ArmyStepDown args: {args}", args.ToString());
            return true;
        }
        
        [HttpPut("armies/{armyGuid}/ranks/{rankId}")]
        [R5SigAuthRequired]
        public async Task<object> ArmyUpdateRank([FromBody] object args, long armyGuid, short rankId)
        {
            Logger.LogError("Function [PUT]ArmyUpdateRank args: {args}", args.ToString());
            return true;
        }
        
        [HttpDelete("armies/{armyGuid}/ranks/{rankId}")]
        [R5SigAuthRequired]
        public async Task<object> ArmyDeleteRank([FromBody] object args, long armyGuid, short rankId)
        {
            Logger.LogError("Function [DELETE]ArmyDeleteRank args: {args}", args.ToString());
            return true;
        }
        
        [HttpPut("armies/{armyGuid}/ranks/batch")]
        [R5SigAuthRequired]
        public async Task<object> ArmyBatchUpdateRanks([FromBody] object args, long armyGuid)
        {
            Logger.LogError("Function [PUT]ArmyBatchUpdateRanks args: {args}", args.ToString());
            return true;
        }
        
        [HttpPut("armies/{armyGuid}/ranks/batch_oder")]
        [R5SigAuthRequired]
        public async Task<object> ArmyBatchUpdateRankOrder([FromBody] object args, long armyGuid)
        {
            Logger.LogError("Function [PUT]ArmyBatchUpdateRankOrder args: {args}", args.ToString());
            return true;
        }
        
        [HttpGet("armies/{armyGuid}/members")]
        [R5SigAuthRequired]
        public async Task<ArmyMembers> ArmyMemberList([FromQuery] Pagination args, long armyGuid)
        {
            var members = await Db.ArmyMemberList(armyGuid, args.page, args.per_page);
            var membersCount = members.Count();
            var membersList = new List<ArmyMember>(membersCount);

            if (membersCount > 0)
            {
                foreach (var member in members)
                {
                    member.last_zone_id = 448;
                    membersList.Add(member);
                }
            }

            var membersResp = new ArmyMembers
            {
                page = args.page.ToString(),
                total_count = membersCount,
                results = (membersList.Count > 0 ? membersList : new List<ArmyMember>())
            };
            
            Logger.LogError("Function [PUT]ArmyMemberList resp: {membersResp}", JsonSerializer.Serialize<ArmyMembers>(membersResp).ToString());

            return membersResp;
        }

        [HttpGet("armies/{armyGuid}/members/{characterGuid}/rank")]
        [R5SigAuthRequired]
        public async Task<ArmyRank> ArmyMemberRank(long armyGuid, long characterGuid = 0)
        {
            // TODO: Temporary - remove
            if (characterGuid == 0)
            {
                var loginResult = await Db.GetLoginData(GetUid());
                characterGuid = await Db.ActiveCharacterGuid(loginResult.account_id);
            }
            
            var rankInfo = await Db.ArmyMemberRank(armyGuid, characterGuid);

            return rankInfo;
        }
        
        [HttpPut("armies/{armyGuid}/members/{characterGuid}/rank")]
        [R5SigAuthRequired]
        public async Task<object> ArmyPromoteMemberRank([FromBody] object args, long armyGuid, long characterGuid)
        {
            Logger.LogError("Function [PUT]ArmyPromoteMemberRank args: {args}", args.ToString());
            return true;
        }
        
        [HttpPut("armies/{armyGuid}/members/batch_rank")]
        [R5SigAuthRequired]
        public async Task<object> ArmyBatchSetMemberRanks([FromBody] object args, long armyGuid, long characterGuid)
        {
            Logger.LogError("Function [PUT]ArmyBatchSetMemberRanks args: {args}", args.ToString());
            return true;
        }
        
        [HttpPut("armies/{armyGuid}/members/{characterGuid}/demote")]
        [R5SigAuthRequired]
        public async Task<object> ArmyDemoteMemberRank([FromBody] object args, long armyGuid, long characterGuid)
        {
            Logger.LogError("Function [PUT]ArmyDemoteMemberRank args: {args}", args.ToString());
            return true;
        }
        
        [HttpPut("armies/{armyGuid}/members/{characterGuid}/kick")]
        [R5SigAuthRequired]
        public async Task<object> ArmyKickMember([FromBody] object args, long armyGuid, long characterGuid)
        {
            Logger.LogError("Function [PUT]ArmyKickMember args: {args}", args.ToString());
            return true;
        }
        
        [HttpPut("armies/{armyGuid}/members/batch_kick")]
        [R5SigAuthRequired]
        public async Task<object> ArmyBatchKickMembers([FromBody] object args, long armyGuid, long characterGuid)
        {
            Logger.LogError("Function [PUT]ArmyBatchKickMembers args: {args}", args.ToString());
            return true;
        }
        
        [HttpPost("armies/{armyGuid}/invite")]
        [R5SigAuthRequired]
        public async Task<object> ArmyInvite([FromBody] object args, long armyGuid)
        {
            Logger.LogError("Function [POST]ArmyInvite args: {args}", args.ToString());
            // {character_name=name, message=message} ??
            return true;
        }
        
        [HttpPost("armies/{armyGuid}/apply")]
        [R5SigAuthRequired]
        public async Task<object> ArmyApplication([FromBody] object args, long armyGuid)
        {
            Logger.LogError("Function [POST]ArmyApplication args: {args}", args.ToString());
            // {"message" : "testing to write an application"}
            return true;
        }
        
        [HttpGet("armies/{armyGuid}/applications")]
        [R5SigAuthRequired]
        public async Task<List<ArmyApplication>> ArmyApplicationList(long armyGuid)
        {
            var applicationList = await Db.ArmyApplicationList(armyGuid);
            
            return applicationList;
        }
        
        /// <summary>
        /// This endpoint is used for both application and invite acceptance
        /// </summary>
        /// TODO: Verification so users can't approve their own request
        [HttpPost("armies/{armyGuid}/applications/{applicationId}/approve")]
        [R5SigAuthRequired]
        public async Task<object> ArmyApplicationApprove([FromBody] object args, long armyGuid)
        {
            Logger.LogError("Function [POST]ArmyApplicationApprove args: {args}", args.ToString());
            return true;
        }
        
        [HttpPost("armies/{armyGuid}/applications/batch_approve")]
        [R5SigAuthRequired]
        public async Task<object> ArmyApplicationBatchApprove([FromBody] object args, long armyGuid)
        {
            Logger.LogError("Function [POST]ArmyApplicationBatchApprove args: {args}", args.ToString());
            return true;
        }
        
        /// <summary>
        /// This endpoint is used for both application and invite rejection
        /// </summary>
        /// TODO: Verification so users can't reject their own request
        [HttpPost("armies/{armyGuid}/applications/{applicationId}/reject")]
        [R5SigAuthRequired]
        public async Task<object> ArmyApplicationRejection([FromBody] object args, long armyGuid)
        {
            Logger.LogError("Function [POST]ArmyApplicationRejection args: {args}", args.ToString());
            return true;
        }
        
        [HttpPost("armies/{armyGuid}/applications/batch_reject")]
        [R5SigAuthRequired]
        public async Task<object> ArmyApplicationBatchRejection([FromBody] object args, long armyGuid)
        {
            Logger.LogError("Function [POST]ArmyApplicationBatchRejection args: {args}", args.ToString());
            return true;
        }
        
        [HttpPut("armies/{armyGuid}/members/{characterGuid}/public_note")]
        [R5SigAuthRequired]
        public async Task<bool> ArmySetPublicNote([FromBody] object args, long armyGuid, long characterGuid)
        {
            Logger.LogError("Function [PUT]ArmySetPublicNote args: {args}", args.ToString());
            return true;
        }
    }
}