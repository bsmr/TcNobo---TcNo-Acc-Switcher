﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TcNo_Acc_Switcher_Globals;
using TcNo_Acc_Switcher_Server.Data;
using TcNo_Acc_Switcher_Server.Pages.General;
using TcNo_Acc_Switcher_Server.Pages.BattleNet;
using Formatting = System.Xml.Formatting;

namespace TcNo_Acc_Switcher_Server.Pages.BattleNet
{
    public class BattleNetSwitcherFuncs
    {
        private static readonly Data.Settings.BattleNet BattleNet = Data.Settings.BattleNet.Instance;
        private static string _battleNetRoaming;
        private static string _battleNetProgramData;
        private static List<BattleNetSwitcherBase.BattleNetUser> _accounts;
        
        /// <summary>
        /// Main function for Battle.net Account Switcher. Run on load.
        /// Prepares HTML Elements string for insertion into the account switcher GUI.
        /// </summary>
        /// <returns>Whether account loading is successful, or a path reset is needed (invalid dir saved)</returns>
        public static async void LoadProfiles()
        {
            Globals.DebugWriteLine($@"[Func:BattleNet\BattleNetSwitcherFuncs.LoadProfiles] Loading BattleNet profiles");
            _accounts = new List<BattleNetSwitcherBase.BattleNetUser>();
            _battleNetRoaming = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battle.net");
            _battleNetProgramData = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Battle.net");

            BattleNet.LoadIgnoredAccounts();

            var file = await File.ReadAllTextAsync(_battleNetRoaming + "\\Battle.net.config");
            foreach (var mail in (JsonConvert.DeserializeObject(file) as JObject)?.SelectToken("Client.SavedAccountNames")?.ToString()?.Split(','))
            {
                if (BattleNet.IgnoredAccounts.Count(x => x.Key == mail) == 0) // If not on IgnoredAccounts list
                    _accounts.Add(new BattleNetSwitcherBase.BattleNetUser() { Email = mail, BTag = BattleNet.BTags.ContainsKey(mail) ? BattleNet.BTags[mail] : null });
            }
            foreach (var acc in _accounts)
            {
                var element =
                    $"<input type=\"radio\" id=\"{acc.Email}\" class=\"acc\" name=\"accounts\" onchange=\"SelectedItemChanged()\" />\r\n" +
                    $"<label for=\"{acc.Email}\" class=\"acc\">\r\n" +
                    $"<img src=\"\\img\\BattleNetDefault.png\" draggable=\"false\" />\r\n" +
                    $"<h6>{acc.BTag ?? acc.Email}</h6>\r\n";
                //$"<p>{UnixTimeStampToDateTime(ua.LastLogin)}</p>\r\n</label>";  TODO: Add some sort of "Last logged in" json file
                await AppData.ActiveIJsRuntime.InvokeVoidAsync("jQueryAppend", new object[] { "#acc_list", element });
            }
            await AppData.ActiveIJsRuntime.InvokeVoidAsync("initContextMenu");
        }


        /// <summary>
        /// Restart Battle.net with a new account selected. Leave args empty to log into a new account.
        /// </summary>
        /// <param name="accName">(Optional) User's login username</param>
        public static void SwapBattleNetAccounts(string accName = "")
        {
            Globals.DebugWriteLine($@"[Func:BattleNet\BattleNetSwitcherFuncs.SwapBattleNetAccounts] Swapping to: {accName}.");
            AppData.ActiveIJsRuntime.InvokeVoidAsync("updateStatus", "Starting BattleNet");
            if (!CloseBattleNet()) return;
            
            var account = _accounts.First(x => x.Email == accName);
            // Load settings into JObject
            var file = File.ReadAllText(_battleNetRoaming + "\\Battle.net.config");
            var jObject = JsonConvert.DeserializeObject(file) as JObject;

            // Select the JToken with the Account Emails
            var jToken = jObject?.SelectToken("Client.SavedAccountNames");
            
            // Set the to be logged in Account to idx 0
            _accounts.Remove(account);
            _accounts.Insert(0, account);

            // Build the string with the Emails with the Email that's should get logged in at first
            var replaceString = "";
            for (var i = 0; i < _accounts.Count; i++)
            {
                replaceString += _accounts[i].Email;
                if (i < _accounts.Count - 1)
                {
                    replaceString += ",";
                }
            }
            
            // Replace and write the new Json
            jToken?.Replace(replaceString);
            File.WriteAllText(_battleNetRoaming + "\\Battle.net.config", jObject?.ToString());

            GeneralFuncs.StartProgram(BattleNet.Exe(), BattleNet.Admin);
        }

        /// <summary>
        /// Used in JS. Gets whether forget account is enabled (Whether to NOT show prompt, or show it).
        /// </summary>
        /// <returns></returns>
        [JSInvokable]
        public static Task<bool> GetBattleNetForgetAcc() => Task.FromResult(BattleNet.ForgetAccountEnabled);

        /// <summary>
        /// Kills Battle.net processes when run via cmd.exe
        /// </summary>
        public static bool CloseBattleNet()
        {
            Globals.DebugWriteLine($@"[Func:BattleNet\BattleNetSwitcherFuncs.CloseBattleNet]");
            if (!GeneralFuncs.CanKillProcess("Battle.net")) return false;
            Globals.KillProcess("Battle.net");
            return true;
        }

        public static void ChangeUsername(string id, string newName)
        {
            Globals.DebugWriteLine($@"[Func:BattleNet\BattleNetSwitcherFuncs.SetBattleTag] id:{id}, newName:{newName}");
            BattleNet.BTags.Remove(id);
            BattleNet.BTags.Add(id, newName);
            BattleNet.SaveSettings();
            AppData.ActiveNavMan?.NavigateTo("/BattleNet/?cacheReload&toast_type=success&toast_title=Success&toast_message=" + Uri.EscapeUriString("Changed username"), true);
        }

        public static void ForgetAccount(string accName)
        {
            Globals.DebugWriteLine($@"[Func:BattleNet\BattleNetSwitcherFuncs.ForgetAccount] accName:{accName}");
            BattleNet.AddIgnoredAccount(accName);
            BattleNet.BTags.Remove(accName);
            BattleNet.SaveSettings();
            AppData.ActiveNavMan?.NavigateTo("/BattleNet/?cacheReload&toast_type=success&toast_title=Success&toast_message=" + Uri.EscapeUriString("Forgot account"), true);
        }
    }
}
