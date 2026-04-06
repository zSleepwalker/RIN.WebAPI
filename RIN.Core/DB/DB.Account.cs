using System;
using System.Data;
using System.Net.Security;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Dapper;
using FauFau.Net.Web;
using RIN.Core.DB;
using RIN.Core.Utils;

namespace RIN.Core.DB
{
    public partial class DB
    {
        // Register a new account in the DB
        // Checks to make sure the email isn't already in use
        // Returns the account id and an error message if there is one
        public async Task<(long id, string errorStr)> RegisterNewAccount(string email, string password, string country, DateTime birthday, string referralKey, bool emailOptin = false)
        {
            var uid          = Auth.GenerateUserId(email.AsSpan()).ToString();
            var secret       = Auth.GenerateSecret(email.AsSpan(), password.AsSpan()).ToString();
            var passwordHash = AuthUtil.CreatePasswordHashAndSalt(password);

            var result = await DBCall(async conn =>
            {
                var p = new DynamicParameters();
                p.Add("@email", email);
                p.Add("@uid", uid);
                p.Add("@secret", secret);
                p.Add("@password_hash", passwordHash);
                p.Add("@country", country);
                p.Add("@birthday", birthday, DbType.Date);
                p.Add("@email_optin", emailOptin);

                p.Add("@error_text", dbType: DbType.String, direction: ParameterDirection.Output);
                p.Add("@new_account_id", dbType: DbType.Int64, direction: ParameterDirection.Output);

                var r = await conn.ExecuteAsync("webapi.\"CreateNewAccount\"", p, commandType: CommandType.StoredProcedure);

                return (p.Get<long>("@new_account_id"), p.Get<string>("@error_text"));
            });

            return result;
        }

        // Get login data needed to verify a login and respond
        public async Task<LoginResult?> GetLoginData(string uid)
        {
            const string SELECT_SQL = @"SELECT
                webapi.""Accounts"".account_id,
                is_dev,
                created_at,
                secret,
                character_limit,
                true AS                                                             can_login,      
                (vip.account_id IS NOT NULL AND vip.start_date <= NOW() AND vip.expiration_date > NOW()) AS is_vip,
                CASE
                    WHEN vip.account_id IS NOT NULL AND vip.start_date <= NOW() AND vip.expiration_date > NOW()
                        THEN EXTRACT(EPOCH FROM vip.expiration_date) * 1000
                    ELSE -1
                END AS vip_expiration,
                '' AS                                                               error,          
                '' AS                                                               error_msg

                FROM webapi.""Accounts""
                LEFT JOIN webapi.""VipData"" AS vip ON vip.account_id = webapi.""Accounts"".account_id
                WHERE uid = @uid";

            var result = await DBCall(async conn =>
            {
                var r = await conn.QueryFirstOrDefaultAsync<LoginResult>(SELECT_SQL, new {uid});
                return r;
            });

            return result;
        }

        // Update the last time this account logged in successfully
        public async Task<bool> UpdateLastLoginTime(long accountId, DateTime? loginTime = null)
        {
            const string UPDATE_SQL = @"update webapi.""Accounts"" set last_login = @loginTime where account_id = @accountId;";

            loginTime ??= DateTime.Now;
            var result = await DBCall( conn => conn.ExecuteAsync(UPDATE_SQL, new {accountId, loginTime}));

            return result > 0;
        }

        public async Task<bool> UpdateLanguage(long accountId, String language)
        {

            const string UPDATE_SQL = @"update webapi.""Accounts"" set language = @language where account_id = @accountId;";

            var result = await DBCall(conn => conn.ExecuteAsync(UPDATE_SQL, new { language, accountId }));

            return result > 0;
        } 

        public async Task<AccountMTX> GetAccountMTXData(long accountId)
        {
            const string SELECT_SQL = @"SELECT
                rb_balance,

                (SELECT
                price as name_change_cost
                FROM webapi.""Costs""
                WHERE name = 'name_change_cost')

                FROM webapi.""Accounts""
                WHERE account_id = @accountId";

            var result = await DBCall(async conn =>
            {
                var r = await conn.QueryFirstOrDefaultAsync<AccountMTX>(SELECT_SQL, new { accountId });
                return r;
            });

            return result!;
        }

        public async Task<int> GetCharacterLimit(long accountId)
        {
            const string SELECT_SQL = @"SELECT character_limit FROM webapi.""Accounts"" WHERE account_id = @accountId";
            return await DBCall(conn => conn.ExecuteScalarAsync<int>(SELECT_SQL, new { accountId }));
        }

        public async Task<(bool success, string error)> IncrementCharacterLimit(long accountId, int rbCost, int maxSlots)
        {
            return await DBCall(async conn =>
            {
                using var transaction = conn.BeginTransaction();
                try
                {
                    const string SELECT_SQL = @"SELECT rb_balance, character_limit FROM webapi.""Accounts"" WHERE account_id = @accountId FOR UPDATE";
                    var account = await conn.QueryFirstOrDefaultAsync<dynamic>(SELECT_SQL, new { accountId }, transaction);

                    if (account == null) return (false, "Account not found");
                    if (account.character_limit == -1) return (false, "Account already has unlimited slots");
                    if (account.rb_balance < rbCost) return (false, "Not enough Red Beans");
                    if (account.character_limit >= maxSlots) return (false, "Already at maximum character slots");

                    const string UPDATE_SQL = @"UPDATE webapi.""Accounts"" 
                                                SET character_limit = character_limit + 1, 
                                                    rb_balance = rb_balance - @rbCost 
                                                WHERE account_id = @accountId";
                    
                    await conn.ExecuteAsync(UPDATE_SQL, new { accountId, rbCost }, transaction);
                    transaction.Commit();
                    
                    return (true, string.Empty);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Serilog.Log.Error(ex, "Error incrementing character limit for account {accountId}", accountId);
                    return (false, "An error occurred while processing the purchase");
                }
            });
        }

        public async Task<bool> AddOrExtendVip(long accountId, int durationSecs)
        {
            const string UPSERT_SQL = @"
                INSERT INTO webapi.""VipData"" (account_id, start_date, expiration_date)
                VALUES (@accountId, NOW(), NOW() + (@durationSecs || ' seconds')::interval)
                ON CONFLICT (account_id)
                DO UPDATE SET 
                    expiration_date = GREATEST(webapi.""VipData"".expiration_date, NOW()) + (@durationSecs || ' seconds')::interval;";

            var result = await DBCall(conn => conn.ExecuteAsync(UPSERT_SQL, new { accountId, durationSecs }));
            return result > 0;
        }
    }
}