using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using VaultClientIntegrationLib;
using VaultLib;
using System.Net.Http;
using System.Text.RegularExpressions;


namespace SourceGearCheckinForwarder
{
    class Program
    {
        static void Main(string[] args)
        {
            string vaultUrl = GetConfigValue("vaultUrl");  
            string username = GetConfigValue("username"); 
            string password = GetConfigValue("password"); 
            string repos = GetConfigValue("repos"); 
            string root = GetConfigValue("root"); 

            // *****************************************
            long lastVersion = GetFolderLastVersionProcessed();

            // *****************************************
            // 252
            string ontimeUrl = GetConfigValue("ontimeUrl");         // "https://youraccountname.axosoft.com/";
            string ontimeApiKey = GetConfigValue("ontimeApiKey");   // See System Settings->Axosoft API Settings->Manage API Keys;
            long latestVersion = 0;

            // save a list of the days changes
            List<VaultTxHistoryItem> changeList = new List<VaultTxHistoryItem>();


            //while (latestVersion < lastVersion)
            //{
            VaultTxHistoryItem[] historyItems = History(vaultUrl, username, password, repos, root, lastVersion);

            foreach (VaultTxHistoryItem item in historyItems.Reverse())
            {
                if (item.Version <= lastVersion) //item.Version >= 25295 && item.Version <= 25468)
                    continue;

                if (latestVersion == 0 || item.Version > latestVersion)
                    latestVersion = item.Version;

                Console.Write(string.Format("{0}: {1}-{2}-{3}-{4}-{5}", item.Version, item.TxDate.ToUniversalDateTime(), item.Comment, item.UserLogin, item.UserName, item.Type));

                if (!string.IsNullOrEmpty(item.Comment) && !item.Comment.StartsWith(@"Temporary file created by Microsoft"))
                {
                    changeList.Add(item);

                    Regex regex = new Regex(@"(([BRI]#\d*.)\s(.*))");

                    MatchCollection matches = regex.Matches(item.Comment);
                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count >= 3)
                        {
                            string itemIdentifier = match.Groups[2].Value; // B#99999 or R#99999

                            if (!string.IsNullOrEmpty(itemIdentifier))
                            {
                                // get file list
                                List<AxosoftCheckinItemFiles> vaultChangeSetFiles = new List<AxosoftCheckinItemFiles>();
                                vaultChangeSetFiles = GetCheckinFileList(vaultUrl, username, password, repos, root, item.TxID);

                                PublishItemToAxosoft(ontimeUrl, ontimeApiKey, item.Version, item.TxDate.ToUniversalDateTime(), item.UserLogin, itemIdentifier, item.Comment, item.TxID, vaultChangeSetFiles);
                                SetFolderLastVersionProcessed(item.Version);
                            }

                        }

                    }

                }

                Console.WriteLine();
                Console.WriteLine("---------------------------------");

            }

            // }

            // update the 
            if (latestVersion > 0 && latestVersion > lastVersion)
            {
                SetFolderLastVersionProcessed(latestVersion);
                BuildChangeReport(changeList);

            }
            else
            {
                Console.WriteLine("No checkins processed");
            }

        }

        private static long GetFolderLastVersionProcessed()
        {
            try
            {
                string path = GetConfigValue("controlFile");

                string versionDataFromFile = System.IO.File.ReadAllText(path);
                //24639
                long result = 0;

                if (!long.TryParse(versionDataFromFile, out result))
                    throw new ApplicationException("Control File " + path + " did not contain a valid version number");
                else
                    return result;

            }
            catch (Exception ex)
            {

                throw ex;

            }

        }

        private static void SetFolderLastVersionProcessed(long latestVersion)
        {
            string path = GetConfigValue("controlFile");
            System.IO.File.WriteAllText(path, latestVersion.ToString());

        }

        private static void BuildChangeReport(List<VaultTxHistoryItem> changeList)
        {
            // to be implemented
        }

        private static VaultTxHistoryItem[] History(string vaultUrl, string username, string password, string repos, string reposPath, long lastVersion)
        {
            try
            {
                ServerOperations.client.LoginOptions.URL = vaultUrl;
                ServerOperations.client.LoginOptions.User = username;
                ServerOperations.client.LoginOptions.Password = password;
                ServerOperations.client.LoginOptions.Repository = repos;
                //ServerOperations.client.LoginOptions.ProxyDomain = ;
                //ServerOperations.client.LoginOptions.ProxyServer = ;
                //ServerOperations.client.LoginOptions.ProxyPort = ;
                //ServerOperations.client.LoginOptions.ProxyUser = ;
                //ServerOperations.client.LoginOptions.ProxyPassword = ;
                //ServerOperations.client.Comment = _args.Comment;
                //ServerOperations.client.AutoCommit = _args.AutoCommit;
                ServerOperations.client.Verbose = true;

                ServerOperations.Login();

                // bool bRecursive = true;
                //VaultHistoryItem[] histitems = ServerOperations.ProcessCommandHistory(reposPath, bRecursive, DateSortOption.desc, null, null, VaultDate.EmptyDate().ToString(), VaultDate.EmptyDate().ToString(), null, null, lastVersion, -1, 100);
                VaultTxHistoryItem[] histitems = ServerOperations.ProcessCommandVersionHistory(reposPath, lastVersion, VaultDateTime.Now.AddDays(-60), VaultDateTime.Now, 1000);

                ServerOperations.Logout();
                return histitems;
            }
            catch (Exception e)
            {
                Console.WriteLine(@"Vault->GetHistory() failed -" + e.Message);
                Console.WriteLine(e.StackTrace);

            }
            return null;
        }

        private static List<AxosoftCheckinItemFiles> GetCheckinFileList(string vaultUrl, string username, string password, string repos, string reposPath, long TxId)
        {
            List<AxosoftCheckinItemFiles> vaultChangeSetFiles = new List<AxosoftCheckinItemFiles>();

            try
            {

                ServerOperations.client.LoginOptions.URL = vaultUrl;
                ServerOperations.client.LoginOptions.User = username;
                ServerOperations.client.LoginOptions.Password = password;
                ServerOperations.client.LoginOptions.Repository = repos;

                ServerOperations.client.Verbose = true;

                ServerOperations.Login();

                TxInfo info = ServerOperations.ProcessCommandTxDetail(TxId);

                foreach (VaultTxDetailHistoryItem histItem in info.items)
                {
                    if (histItem.Version == 1)
                        vaultChangeSetFiles.Add(new AxosoftCheckinItemFiles(histItem.Name, histItem.ItemPath1, "added"));
                    else
                        vaultChangeSetFiles.Add(new AxosoftCheckinItemFiles(histItem.Name, histItem.ItemPath1, "modified"));


                }

                ServerOperations.Logout();
            }
            catch (Exception e)
            {
                Console.WriteLine(@"Vault->GetHistory() failed -" + e.Message);
                Console.WriteLine(e.StackTrace);

            }

            return vaultChangeSetFiles;
        }

        private static void PublishItemToAxosoft(string url, string apiKey, long checkinVersionID, DateTime checkinTime, string username, string itemIdentifier, string comment, long itemTxId, List<AxosoftCheckinItemFiles> vaultChangeSetFiles)
        {
            if (string.IsNullOrEmpty(itemIdentifier))
            {
                Console.Write(" ** item identifier not found.");
                return;
            }

            Console.Write(" ** " + itemIdentifier + " ");

            AxosoftCheckinItem checkinItem = null; // new AxosoftCheckinItem();


            // https://developer.axosoft.com/scm-integration.html

            // strip out data from comment...
            string itemType = "d";      // d->bug/defect, f->feature/request
            itemIdentifier = itemIdentifier.Trim();
            if (itemIdentifier.StartsWith("B#") || itemIdentifier.StartsWith("b#"))
            {
                itemType = "d";
            }
            else if (itemIdentifier.StartsWith("R#") || itemIdentifier.StartsWith("r#"))
            {
                itemType = "f";
            }
            else
            {
                return;
            }

            string itemId = itemIdentifier.Substring(2);         // id for bug/request
            string messageTaskId = "[axo{0}: {1}]";

            if (itemId == "0")
            {
                CreateNewItem(url, apiKey, checkinVersionID, checkinTime, username, itemType, itemIdentifier, comment);
                return;
            }

            string axoSoftId = string.Format(messageTaskId, itemType, itemId.Trim());

            checkinItem = new AxosoftCheckinItem(checkinVersionID, comment + " " + axoSoftId, string.Empty, checkinTime, username, username, vaultChangeSetFiles);
            string paramString = Newtonsoft.Json.JsonConvert.SerializeObject(checkinItem);

            string endpointUrl = @"api/v2/source_control_commits";
            string hashParameter = "?hash=";

            string postUrl = endpointUrl + hashParameter + GetHashSha256(paramString, apiKey);

            // -------------------------------------------------------------------------------------

            WebRequest request = WebRequest.CreateHttp(url + postUrl);
            request.Credentials = CredentialCache.DefaultCredentials;
            ((HttpWebRequest)request).UserAgent = "IndiciaSourceGearCheckinForwarder";

            request.Method = "POST";
            request.ContentType = "application/json";
            //request.ContentLength = paramString.Length;
            System.IO.Stream postData = request.GetRequestStream();
            byte[] requestBody = System.Text.Encoding.ASCII.GetBytes(paramString); // Encoding.UTF8.GetBytes(paramString);
            postData.Write(requestBody, 0, requestBody.Length);
            postData.Close();

            WebResponse response = request.GetResponse();

            Console.WriteLine(((HttpWebResponse)response).StatusDescription);

            System.IO.Stream dataStream = response.GetResponseStream();
            System.IO.StreamReader reader = new System.IO.StreamReader(dataStream);

            string responseFromServer = reader.ReadToEnd();

            //Console.WriteLine(responseFromServer);

            reader.Close();
            dataStream.Close();
            response.Close();

            Console.WriteLine("\r\n");

            // -------------------------------------------------------------------------------------



        }

        public static bool CreateNewItem(string url, string apiKey, long checkinVersionID, DateTime checkinTime, string username, string itemType, string itemIdentifier, string comment)
        {
            return false;
        }

        public static string GetHashSha256(string message, string saltOrpepper)
        {
            //now have json object, spit it out to axosoft. 
            var hashedJSON = string.Format("{0}{1}", message, saltOrpepper);

            SHA256 hash = SHA256.Create();
            Byte[] result = hash.ComputeHash(System.Text.Encoding.ASCII.GetBytes(hashedJSON));
            hashedJSON = result.Aggregate("", (current, x) => current + string.Format("{0:x2}", x));

            return hashedJSON;

        }


        public class AxosoftCheckinItem
        {
            public long Version { get; set; }
            public string Comment { get; set; }
            public string Url { get; set; }

            public DateTime CheckInDate { get; set; }

            public string Username { get; set; }
            public string DisplayName { get; set; }

            public List<AxosoftCheckinItemFiles> Files { get; set; }

            public AxosoftCheckinItem()
            {

            }

            public AxosoftCheckinItem(long version, string checkInComment, string url, DateTime checkedInAt, string username, string displayName, List<AxosoftCheckinItemFiles> files)
            {

                this.Version = version;
                this.Comment = checkInComment;
                this.Url = url;
                this.CheckInDate = checkedInAt;
                this.Username = username;
                this.DisplayName = displayName;
                this.Files = files;

                if (this.Files.Count == 0)
                {
                    this.Files.Add(new AxosoftCheckinItemFiles());

                }

            }

        }

        public class AxosoftCheckinItemFiles
        {
            public string FilePath { get; set; }
            public string FileUrl { get; set; }
            public string FileActionType { get; set; }

            public AxosoftCheckinItemFiles()
            {
                FilePath = "$/";
                FileUrl = "http://sourcegearvault";
                FileActionType = "modified";
            }

            public AxosoftCheckinItemFiles(string path, string url, string actionType)
            {
                FilePath = path;
                FileUrl = url;
                FileActionType = actionType;

                if (string.IsNullOrEmpty(FileActionType))
                    FileActionType = "modified";

            }
        }

        private static string GetConfigValue(string key)
        {
            string value = "";

            if (ConfigurationManager.AppSettings.AllKeys.Contains(key))
            {
                value = ConfigurationManager.AppSettings[key] as string;
                if (value == null)
                    value = "";

            }

            return value;

        }

    }
}
