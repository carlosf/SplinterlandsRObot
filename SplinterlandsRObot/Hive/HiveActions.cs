﻿using System.Text;
using HiveAPI.CS;
using static HiveAPI.CS.CHived;
using SplinterlandsRObot.Net;
using SplinterlandsRObot.Constructors;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Data;
using Cryptography.ECDSA;
using SplinterlandsRObot.API;

namespace SplinterlandsRObot.Hive
{
    public class HiveActions
    {
        CHived hive = new CHived(InstanceManager.HttpClient, Constants.HIVE_NODE);
        private object lk = new();
        public string StartNewMatch(User user)
        {
            string n = RandomString(10);
            string json = "{\"match_type\":\"Ranked\",\"app\":\"" + Constants.SPLINTERLANDS_VERSION + "\",\"n\":\"" + n + "\"}";

            COperations.custom_json custom_Json = CreateCustomJson(user, false, true, "sm_find_match", json);

            try
            {
                Logs.LogMessage($"{user.Username}: Finding match...");
                CtransactionData oTransaction = hive.CreateTransaction(new object[] { custom_Json }, new string[] { user.PassCodes.PostingKey });
                var postData = GetStringForSplinterlandsAPI(oTransaction);
                return HttpWebRequest.WebRequestPost(InstanceManager.CookieContainer, postData, "https://battle.splinterlands.com/battle/battle_tx", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logs.LogMessage($"{user.Username}: Error at finding match: " + ex.ToString(), Logs.LOG_WARNING);
            }
            return "";
        }

        public async Task<(string secret, string tx)> SubmitTeam(string tx, JToken matchDetails, JToken team, User user, CardsCollection CardsCached)
        {
            try
            {
                string summoner = team["summoner"].ToString();
                string monsters = "";
                for (int i = 1; i <= 6; i++)
                {
                    string monster = team[$"card{i}"].ToString();

                    if (monster != "")
                    {
                        if (monster.Length == 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                    
                    monsters += "\"" + monster + "\",";
                }
                monsters = monsters[..^1];

                string secret = RandomString(10);
                string n = RandomString(10);

                string monsterClean = monsters.Replace("\"", "");

                string teamHash = GenerateMD5Hash(summoner + "," + monsterClean + "," + secret);

                string json = "{\"trx_id\":\"" + tx + "\",\"team_hash\":\"" + teamHash + "\",\"app\":\"" + Constants.SPLINTERLANDS_VERSION + "\",\"n\":\"" + n + "\"}";

                COperations.custom_json custom_Json = CreateCustomJson(user, false, true, "sm_submit_team", json);

                Logs.LogMessage($"{user.Username}: Submitting team...");
                Logs.LogMessage($"{user.Username}: Creating transaction", supress: true);
                CtransactionData oTransaction = hive.CreateTransaction(new object[] { custom_Json }, new string[] { user.PassCodes.PostingKey });
                Logs.LogMessage($"{user.Username}: Creating postData", supress: true);
                var postData = GetStringForSplinterlandsAPI(oTransaction);
                Logs.LogMessage($"{user.Username}: Fetching battle data from Splinterlands", supress: true);
                var response = HttpWebRequest.WebRequestPost(InstanceManager.CookieContainer, postData, "https://battle.splinterlands.com/battle/battle_tx", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "https://splinterlands.com/", Encoding.UTF8);
                string responseTx = DoQuickRegex("id\":\"(.*?)\"", response);
                Logs.LogMessage($"{user.Username}: Result TX:{responseTx}", supress: true);
                return (secret, responseTx);
            }
            catch (Exception ex)
            {
                Logs.LogMessage($"{user.Username}: Error at submitting team: " + ex.ToString(), Logs.LOG_WARNING);
            }
            return ("", "");
        }

        public bool RevealTeam(string tx, JToken matchDetails, JToken team, string secret, User user, CardsCollection CardsCached)
        {
            try
            {
                string summoner = team["summoner"].ToString();
                string monsters = "";
                for (int i = 1; i <= 6; i++)
                {
                    string monster = team[$"card{i}"].ToString();

                    if (monster != "")
                    {
                        if (monster.Length == 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }

                    monsters += "\"" + monster + "\",";
                }
                monsters = monsters[..^1];

                string n = RandomString(10);

                string monsterClean = monsters.Replace("\"", "");


                string json = "{\"trx_id\":\"" + tx + "\",\"summoner\":\"" + summoner + "\",\"monsters\":[" + monsters + "],\"secret\":\"" + secret + "\",\"app\":\"" + Constants.SPLINTERLANDS_VERSION + "\",\"n\":\"" + n + "\"}";

                COperations.custom_json custom_Json = CreateCustomJson(user, false, true, "sm_team_reveal", json);

                Logs.LogMessage($"{user.Username}: Revealing team...");
                Logs.LogMessage($"{user.Username}: JSON:{JsonConvert.SerializeObject(custom_Json)}", supress: true);
                CtransactionData oTransaction = hive.CreateTransaction(new object[] { custom_Json }, new string[] { user.PassCodes.PostingKey });
                Logs.LogMessage($"{user.Username}: Reveal TX:{JsonConvert.SerializeObject(oTransaction.tx)}", supress: true);
                var postData = GetStringForSplinterlandsAPI(oTransaction);
                var response = HttpWebRequest.WebRequestPost(InstanceManager.CookieContainer, postData, "https://battle.splinterlands.com/battle/battle_tx", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0", "https://splinterlands.com/", Encoding.UTF8);
                string responseTx = DoQuickRegex("id\":\"(.*?)\"", response);
                
                if (responseTx == "")
                    return false;
            }
            catch (Exception ex)
            {
                Logs.LogMessage($"{user.Username}: Error at revealing team: " + ex.ToString(), Logs.LOG_WARNING);
                return false;
            }
            return true;
        }

        public COperations.custom_json CreateCustomJson(User user, bool activeKey, bool postingKey, string methodName, string json)
        {
            COperations.custom_json customJsonOperation = new COperations.custom_json
            {
                required_auths = activeKey ? new string[] { user.Username } : new string[0],
                required_posting_auths = postingKey ? new string[] { user.Username } : new string[0],
                id = methodName,
                json = json
            };
            return customJsonOperation;
        }

        public async Task ClaimSeasonRewards()
        {
            Splinterlands sp_api = new();
            try
            {
                foreach (User user in Users.userList)
                {
                    Logs.LogMessage($"[Season Rewards] {user.Username}: Checking for season rewards... ", supress: true);
                    var bid = "bid_" + RandomString(20);
                    var sid = "sid_" + RandomString(20);
                    var ts = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds().ToString();
                    var hash = Sha256Manager.GetHash(Encoding.ASCII.GetBytes(user.Username + ts));
                    var sig = Secp256K1Manager.SignCompressedCompact(hash, CBase58.DecodePrivateWif(user.PassCodes.PostingKey));
                    var signature = Hex.ToString(sig);
                    var response = await sp_api.GetSeasonDetails(user.Username, bid, sid, signature, ts);

                    var seasonReward = DoQuickRegex("\"season_reward\":(.*?)},\"", response);
                    if (seasonReward == "{\"reward_packs\":0")
                    {
                        Logs.LogMessage($"{user.Username}: No season reward available!", Logs.LOG_ALERT, true);
                    }
                    else
                    {
                        var season = DoQuickRegex("\"season\":(.*?),\"", seasonReward);
                        if (season.Length <= 1)
                        {
                            Logs.LogMessage($"{user.Username}: Error at claiming season rewards: Could not read season!", Logs.LOG_WARNING);
                        }
                        else
                        {
                            Logs.LogMessage($"{user.Username}: Season rewards available.", Logs.LOG_ALERT);
                            string n = RandomString(10);
                            string json = "{\"type\":\"league_season\",\"season\":\"" + season + "\",\"app\":\"" + Constants.SPLINTERLANDS_VERSION + "\",\"n\":\"" + n + "\"}";

                            COperations.custom_json custom_Json = CreateCustomJson(user, false, true, "sm_claim_reward", json);

                            CtransactionData oTransaction = hive.CreateTransaction(new object[] { custom_Json }, new string[] { user.PassCodes.PostingKey });
                            string tx = hive.broadcast_transaction(new object[] { custom_Json }, new string[] { user.PassCodes.PostingKey });
                            for (int i = 0; i < 10; i++)
                            {
                                await Task.Delay(15000);
                                var rewardsRaw = await sp_api.GetTransactionDetails(tx);
                                if (rewardsRaw.Contains(" not found"))
                                {
                                    continue;
                                }
                                else if (rewardsRaw.Contains("has already claimed their rewards from the specified season"))
                                {
                                    Logs.LogMessage($"[Season Rewards] {user.Username}: Rewards already claimed!", Logs.LOG_ALERT);
                                }
                                var rewards = JToken.Parse(rewardsRaw)["trx_info"]["result"];


                                if (!((string)rewards).Contains("success\":true"))
                                {
                                    Logs.LogMessage($"[Season Rewards] {user.Username}: Error at claiming season rewards: " + (string)rewards, Logs.LOG_WARNING);

                                }
                                else if (((string)rewards).Contains("success\":true"))
                                {
                                    Logs.LogMessage($"[Season Rewards] {user.Username}: Successfully claimed season rewards!", Logs.LOG_SUCCESS);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logs.LogMessage($"[Season Rewards] Error at claiming season reward: {ex.Message}", Logs.LOG_WARNING);
            }
        }

        private string GetStringForSplinterlandsAPI(CtransactionData oTransaction)
        {
            string json = JsonConvert.SerializeObject(oTransaction.tx);
            string postData = "signed_tx=" + json.Replace("operations\":[{", "operations\":[[\"custom_json\",{")
                .Replace(",\"opid\":18}", "}]");
            return postData;
        }

        public string RandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            Random random = new Random();
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public string DoQuickRegex(string Pattern, string Match)
        {
            Regex r = new Regex(Pattern, RegexOptions.Singleline);
            return r.Match(Match).Groups[1].Value;
        }
        public string GenerateMD5Hash(string input)
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        internal string AdvanceLeague(User user)
        {
            string n = RandomString(10);
            string json = "{\"notify\":\"false\",\"app\":\"" + Constants.SPLINTERLANDS_VERSION + "\",\"n\":\"" + n + "\"}";

            COperations.custom_json custom_Json = CreateCustomJson(user, false, true, "sm_advance_league", json);

            string tx = hive.broadcast_transaction(new object[] { custom_Json }, new string[] { user.PassCodes.PostingKey });
            return tx;
        }

        internal string ClaimQuest(User user, string questId)
        {
            string n = RandomString(10);
            string json = "{\"type\":\"quest\",\"quest_id\":\"" + questId + "\",\"app\":\"" + Constants.SPLINTERLANDS_VERSION + "\",\"n\":\"" + n + "\"}";

            COperations.custom_json custom_Json = CreateCustomJson(user, false, true, "sm_claim_reward", json);

            string tx = hive.broadcast_transaction(new object[] { custom_Json }, new string[] { user.PassCodes.PostingKey });
            return tx;
        }

        

        internal async Task<bool> NewQuest(User user)
        {
            string n = RandomString(10);
            string json = "{\"type\":\"daily\",\"app\":\"" + Constants.SPLINTERLANDS_VERSION + "\",\"n\":\"" + n + "\"}";

            COperations.custom_json custom_Json = CreateCustomJson(user, false, true, "sm_refresh_quest", json);

            string tx = hive.broadcast_transaction(new object[] { custom_Json }, new string[] { user.PassCodes.PostingKey });
            if (tx != null)
                return true;

            return false;
        }

        internal async Task<bool> StartQuest(User user)
        {
            string n = RandomString(10);
            string json = "{\"type\":\"daily\",\"app\":\"" + Constants.SPLINTERLANDS_VERSION + "\",\"n\":\"" + n + "\"}";

            COperations.custom_json custom_Json = CreateCustomJson(user, false, true, "sm_start_quest", json);

            string tx = hive.broadcast_transaction(new object[] { custom_Json }, new string[] { user.PassCodes.PostingKey });
            if (tx != null)
                return true;

            return false;
        }
    }
}
