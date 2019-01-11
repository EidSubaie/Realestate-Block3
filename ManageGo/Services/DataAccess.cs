﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ManageGo.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ManageGo.Services
{
    public static class DataAccess
    {
        private const string BaseUrl = "https://ploop.dynamo-ny.com/api/pmc_v2/";
        internal static readonly HttpClient client = new HttpClient();
        private static string AccessToken { get; set; }

        private static DateTimeOffset TokenExpiry { get; set; } = DateTimeOffset.FromUnixTimeSeconds(0);

        public static async Task<object> Login(string userName = null, string password = null)
        {
            //todo: may need to remove this at some point
#if DEBUG
            //  userName = "pmc@mobile.test";
            //  password = "111111";
#endif

            Dictionary<string, string> credentials = new Dictionary<string, string>
            {
                { "login", userName },
                { "password", password }
            };

            //todo: fix error when user not found in release mode
            var content = new FormUrlEncodedContent(credentials);
            var response = await client.PostAsync(BaseUrl + APIpaths.authorize, content);
            var responseString = await response.Content.ReadAsStringAsync();
            //create jobject from response
            var responseObject = JObject.Parse(responseString);
            //get the result jtoken
            if (responseObject.TryGetValue("Result", out JToken result))
            {
                if (result.ToObject<object>() is null)
                {
                    if (responseObject.TryGetValue("ErrorMessage", out JToken errorMsg))
                    {
                        throw new Exception(errorMsg.ToObject<string>());
                    }
                    else
                    {
                        throw new Exception("Unable to log in");
                    }
                }

                //result is a dictionary of jobjects
                var jResult = result.ToObject<Dictionary<string, JObject>>();
                //get user-info
                if (jResult.TryGetValue(APIkeys.UserInfo.ToString(), out JObject userInfo))
                {
                    App.UserInfo = userInfo.ToObject<Models.SignedInUserInfo>();
                }
                jResult.TryGetValue(APIkeys.PMCInfo.ToString(), out JObject pmcInfo);
                //user info is a dictionary
                var dic = userInfo.ToObject<Dictionary<string, string>>();
                var pmcDic = pmcInfo.ToObject<Dictionary<string, string>>();
                //get access token
                if (dic.TryGetValue(APIkeys.AccessToken.ToString(), out string token))
                {
                    AccessToken = token;
                    TokenExpiry = DateTimeOffset.Now.AddMinutes(60);
                    client.DefaultRequestHeaders.Remove(APIkeys.AccessToken.ToString());
                    client.DefaultRequestHeaders.Add(APIkeys.AccessToken.ToString(), AccessToken);
                }
                if (dic.TryGetValue(APIkeys.UserFirstName.ToString(), out string firstName)
                    && dic.TryGetValue(APIkeys.UserLastName.ToString(), out string lastName))
                {
                    App.UserName = firstName + " " + lastName;
                }

                if (pmcDic.TryGetValue(APIkeys.PMCName.ToString(), out string pmcName))
                {
                    App.PMCName = pmcName;
                }
                //get Permissions
                if (jResult.TryGetValue(APIkeys.Permissions.ToString(), out JObject permisions))
                {
                    var perm = permisions.ToObject<LoggedInUserPermissions>();

                    //reset the permissions on log in
                    App.UserPermissions = UserPermissions.None;
                    if (perm.CanAccessPayments)
                        App.UserPermissions |= UserPermissions.CanAccessPayments;
                    if (perm.CanAccessMaintenanceTickets)
                        App.UserPermissions |= UserPermissions.CanAccessTickets;
                    if (perm.CanReplyPublicly)
                        App.UserPermissions |= UserPermissions.CanReplyPublicly;
                    if (perm.CanReplyInternally)
                        App.UserPermissions |= UserPermissions.CanReplyInternally;
                    if (perm.CanAccessTenants)
                        App.UserPermissions |= UserPermissions.CanAccessTenants;
                    if (perm.CanAddWorkordersAndEvents)
                        App.UserPermissions |= UserPermissions.CanAddWorkordersAndEvents;
                    if (perm.CanApproveNewTenantsUnits)
                        App.UserPermissions |= UserPermissions.CanApproveNewTenantsUnits;
                    if (perm.CanEditTicket)
                        App.UserPermissions |= UserPermissions.CanEditTicketDetails;

                }
            }
            return responseString;
        }


        public static async Task ResetPassword(string userName)
        {
            Dictionary<string, string> credentials = new Dictionary<string, string>
            {
                { "PMCUserEmailAddress", userName }
            };
            var content = new FormUrlEncodedContent(credentials);
            var response = await client.PostAsync(BaseUrl + APIpaths.authorize.ToString(), content);
            var responseString = await response.Content.ReadAsStringAsync();
        }

        #region MAINTENANCE OBJECT - CATEGORIES
        public static async Task GetAllCategoriesAndTags()
        {
            var response = await client.PostAsync(BaseUrl + APIpaths.MaintenanceObjects.ToString(), null);
            var responseString = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(responseString);
            var result = obj.GetValue("Result");

            if (result.ToObject<Dictionary<string, object>>().TryGetValue("Categories", out object list)
                && list is JContainer && result.ToObject<Dictionary<string, object>>().TryGetValue("Tags", out object tagsList)
                && result.ToObject<Dictionary<string, object>>().TryGetValue("ExternalContacts", out object contactList))
            {
                App.Categories = ((JContainer)list).ToObject<List<Categories>>();
                App.Tags = ((JContainer)tagsList).ToObject<List<Tags>>();
                App.ExternalContacts = ((JContainer)contactList).ToObject<List<ExternalContact>>();
            }
        }

        #endregion

        #region DASHBOARD
        public static async Task<Dictionary<string, string>> GetDashboardAsync()
        {
            if (TokenExpiry < DateTimeOffset.Now)
                await Login();
            var response = await client.PostAsync(BaseUrl + APIpaths.dashboard.ToString(), null);
            return await GetResultDictionaryFromResponse(response);
        }
        #endregion

        public static async Task GetBankAccounts()
        {
            // var param = new Dictionary<string, string> { { "page", "1" } };
            var response = await client.PostAsync(BaseUrl + APIpaths.BankAccounts.ToString(), null);
            var responseString = await response.Content.ReadAsStringAsync();
            var dic = JObject.Parse(responseString);
            if (dic.TryGetValue("Result", out JToken list))
            {
                App.BankAccounts = list.ToObject<List<Models.BankAccount>>();
            }
        }

        #region PENDING NOTIFICATIONS
        public static async Task<List<PendingApprovalItem>> GetPendingNotifications()
        {
            // var param = new Dictionary<string, string> { { "page", "1" } };
            var response = await client.PostAsync(BaseUrl + APIpaths.PendingApprovals.ToString(), null);
            var responseString = await response.Content.ReadAsStringAsync();
            var dic = JObject.Parse(responseString);
            if (dic.TryGetValue("Result", out JToken list))
            {
                return list.ToObject<List<PendingApprovalItem>>();
            }
            throw new Exception("Unable to get notifications");
        }
        public static async Task ApproveItem(PendingApprovalItem item)
        {
            var param = new Dictionary<string, object> {
                { "LeaseID", item.LeaseID },
                { "Action", true}
            };
            var jsonString = JsonConvert.SerializeObject(param);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(BaseUrl + APIpaths.PendingApprovalAction.ToString(), content);
            var responseString = await response.Content.ReadAsStringAsync();
            var dic = JObject.Parse(responseString);
            if (((string)dic["Result"]).ToLower() != "success")
            {
                throw new Exception((string)dic["ErrorMessage"]);
            }
        }
        #endregion


        #region BUILDINGS
        public static async Task GetBuildings()
        {
            var param = new Dictionary<string, string> { { "page", "1" } };
            var response = await client.PostAsync(BaseUrl + APIpaths.buildings.ToString(), new FormUrlEncodedContent(param));
            var responseString = await response.Content.ReadAsStringAsync();
            var dic = JObject.Parse(responseString);
            if (dic.TryGetValue("Result", out JToken list))
            {
                App.Buildings = list.ToObject<List<Building>>();
                App.Buildings.Sort();
            }
        }
        public static async Task<Building> GetBuildingDetails(int id)
        {
            var param = new Dictionary<string, string> { { "BuildingID", $"{id}" } };
            var response = await client.PostAsync(BaseUrl + APIpaths.BuildingDetails.ToString(), new FormUrlEncodedContent(param));
            var responseString = await response.Content.ReadAsStringAsync();
            var dic = JObject.Parse(responseString);
            if (dic.TryGetValue("Result", out JToken list))
            {
                return list.ToObject<Building>();
            }
            else if (dic.TryGetValue("Error", out JToken error))
            {
                throw new Exception(error.ToObject<string>());
            }
            throw new Exception("Unable to get building details");
        }
        #endregion

        #region USERS
        public static async Task GetAllUsers()
        {
            var response = await client.PostAsync(BaseUrl + APIpaths.Users.ToString(), null);
            var responseString = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(responseString);
            var result = obj.GetValue("Result");
            App.Users = result.ToObject<List<User>>();
        }

        internal static async Task UpdateUserInfo(Dictionary<string, object> filtersDictionary)
        {
            var jsonString = JsonConvert.SerializeObject(filtersDictionary);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.UserSettings.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);
            var responseString = await response.Content.ReadAsStringAsync();
        }

        internal static async Task<List<DateTime>> GetEventsList(Dictionary<string, object> filtersDictionary)
        {
            //the API return only list of Dates
            // call EventList to get the actual events
            var jsonString = JsonConvert.SerializeObject(filtersDictionary);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.EventsListDates.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);
            var responseString = await response.Content.ReadAsStringAsync();
            var dic = JObject.Parse(responseString);
            if (dic.TryGetValue("Result", out JToken list))
            {
                return list["Dates"].ToObject<List<DateTime>>();
            }
            else
            {
                throw new Exception("Unable to get tenants");
            }
        }

        internal static async Task<List<Models.CalendarEvent>> GetEventsForDate(DateTime date)
        {
            var pars = new Dictionary<string, DateTime> {
                { "DateFrom", date.Date },
                { "DateTo", date.Date.AddHours(24) }
            };
            var jsonString = JsonConvert.SerializeObject(pars);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.EventList.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);
            var responseString = await response.Content.ReadAsStringAsync();
            var dic = JObject.Parse(responseString);
            if (dic.TryGetValue("Result", out JToken list))
            {
                return list.ToObject<List<CalendarEvent>>();
            }
            else
            {
                throw new Exception("Unable to get tenants");
            }
        }

        internal static async Task<List<Tenant>> GetTenantsAsync(Dictionary<string, object> filtersDictionary)
        {
            var jsonString = JsonConvert.SerializeObject(filtersDictionary);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");//new FormUrlEncodedContent(filters);
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.Tenants.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);
            var responseString = await response.Content.ReadAsStringAsync();
            var dic = JObject.Parse(responseString);
            if (dic.TryGetValue("Result", out JToken list))
            {
                return list.ToObject<List<Tenant>>();
            }
            else
            {
                throw new Exception("Unable to get tenants");
            }
        }
        #endregion

        #region TICKETS

        public static async Task<List<MaintenanceTicket>> GetTicketsAsync(Dictionary<string, object> filters)
        {
            if (TokenExpiry < DateTimeOffset.Now)
                await Login();
            var jsonString = JsonConvert.SerializeObject(filters);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");//new FormUrlEncodedContent(filters);
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.tickets.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);

            return await GetTicketsFromResponse(response);
        }

        internal static async Task<List<MaintenanceTicket>> GetTickets(TicketRequest param)
        {
            if (TokenExpiry < DateTimeOffset.Now)
                await Login();
            var jsonString = JsonConvert.SerializeObject(param);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");//new FormUrlEncodedContent(filters);
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.tickets.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);

            return await GetTicketsFromResponse(response);
        }

        public static async Task UpdateTicket(Dictionary<string, object> parameters)
        {
            if (TokenExpiry < DateTimeOffset.Now)
                await Login();
            var jsonString = JsonConvert.SerializeObject(parameters);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");//new FormUrlEncodedContent(filters);
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.UpdateTicket.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);
            var responseString = await response.Content.ReadAsStringAsync();
            var responseObject = JObject.Parse(responseString);
        }

        public static async Task<int> CreateTicket(Dictionary<string, object> parameters)
        {
            if (TokenExpiry < DateTimeOffset.Now)
                await Login();
            var jsonString = JsonConvert.SerializeObject(parameters);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");//new FormUrlEncodedContent(filters);
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.CreateTicket.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);
            var responseString = await response.Content.ReadAsStringAsync();
            var responseObject = JObject.Parse(responseString);
            if (responseObject.TryGetValue("Status", out JToken status) && (int)status != 1)
            {
                throw new Exception(responseObject.GetValue("ErrorMessage").ToObject<string>());
            }
            return responseObject.TryGetValue("Result", out JToken result) ? (int)result["TicketID"] : 0;
        }

        public static async Task<int> SendNewCommentAsync(Dictionary<string, object> parametes)
        {
            if (TokenExpiry < DateTimeOffset.Now)
                await Login();
            var jsonString = JsonConvert.SerializeObject(parametes);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");//new FormUrlEncodedContent(filters);
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.TicketNewComment.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);
            var responseString = await response.Content.ReadAsStringAsync();
            var responseObject = JObject.Parse(responseString);
            return responseObject.TryGetValue("Result", out JToken result) ? (int)result["CommentID"] : 0;
        }

        public static async Task<int> SendNewEventAsync(Dictionary<string, object> parameters)
        {
            if (TokenExpiry < DateTimeOffset.Now)
                await Login();
            var jsonString = JsonConvert.SerializeObject(parameters);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");//new FormUrlEncodedContent(filters);
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.CreateEvent.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);
            var responseString = await response.Content.ReadAsStringAsync();
            JObject responseObject = JObject.Parse(responseString);
            return responseObject.TryGetValue("Result", out JToken result) ? (int)result["EventID"] : 0;
        }

        public static async Task<int> SendNewWorkOurderAsync(Dictionary<string, object> parameters)
        {
            if (TokenExpiry < DateTimeOffset.Now)
                await Login();
            var jsonString = JsonConvert.SerializeObject(parameters);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");//new FormUrlEncodedContent(filters);
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.CreateWorkOrder.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);
            var responseString = await response.Content.ReadAsStringAsync();
            JObject responseObject = JObject.Parse(responseString);
            return responseObject.TryGetValue("Result", out JToken result) ? (int)result["WorkOrderID"] : 0;
        }

        public static async Task<byte[]> GetCommentFile(Dictionary<string, object> parametes)
        {
            if (TokenExpiry < DateTimeOffset.Now)
                await Login();
            var jsonString = JsonConvert.SerializeObject(parametes);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");//new FormUrlEncodedContent(filters);
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.GetTicketFile.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);
            var responseString = await response.Content.ReadAsStringAsync();
            var responseObject = JObject.Parse(responseString);
            if (responseObject.TryGetValue("Result", out JToken result))
            {
                var array = result.ToObject<byte[]>();
                return array;
            }
            throw new Exception("Downloaded data not valid");
        }


        public static async Task UploadFile(File file)
        {
            if (TokenExpiry < DateTimeOffset.Now)
                await Login();
            var parameters = new Dictionary<string, object> {
                {"CommentID",file.ParentComment},
                {"FileName", file.Name},
                {"File", file.Content},
                {"IsCompleted", false}
            };
            var jsonString = JsonConvert.SerializeObject(parameters);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");//new FormUrlEncodedContent(filters);
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.CommentNewFile.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);
            var responseString = await response.Content.ReadAsStringAsync();
        }

        public static async Task UploadCompleted(int commentId)
        {

            if (TokenExpiry < DateTimeOffset.Now)
                await Login();
            var parameters = new Dictionary<string, object> {
                {"CommentID",commentId},
            };
            var jsonString = JsonConvert.SerializeObject(parameters);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");//new FormUrlEncodedContent(filters);
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.CommentFilesCompleted.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);
        }
        #endregion


        #region Payments
        internal static async Task<List<Models.Payment>> GetPaymentsAsync(PaymentsRequestParamContainer filtersDictionary)
        {
            var jsonString = JsonConvert.SerializeObject(filtersDictionary);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");//new FormUrlEncodedContent(filters);
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.Payments.ToString())
            {
                Content = content
            };


            var response = await client.SendAsync(msg);
            var responseString = await response.Content.ReadAsStringAsync();
            var dic = JObject.Parse(responseString);
            if (dic.TryGetValue("Result", out JToken list))
            {
                return list.ToObject<List<Models.Payment>>();
            }
            throw new Exception("Unable to get payments");



        }

        internal static async Task<List<Models.BankTransaction>> GetTransactionsAsync(Dictionary<string, object> filtersDictionary)
        {
            var jsonString = JsonConvert.SerializeObject(filtersDictionary);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");//new FormUrlEncodedContent(filters);
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.BankTransactions.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);
            var responseString = await response.Content.ReadAsStringAsync();
            var dic = JObject.Parse(responseString);
            if (dic.TryGetValue("Result", out JToken list))
            {
                return list.ToObject<List<Models.BankTransaction>>();
            }
            throw new Exception("Unable to get payments");
        }
        #endregion

        private static async Task<Dictionary<string, string>> GetResultDictionaryFromResponse(HttpResponseMessage response)
        {
            var responseString = await response.Content.ReadAsStringAsync();
            var responseObject = JObject.Parse(responseString);
            if (responseObject.TryGetValue("Result", out JToken token))
            {
                try
                {
                    var dic = token.ToObject<Dictionary<string, string>>();
                    return dic;
                }
                catch //result property types have changed in the backend
                {
                    return new Dictionary<string, string>();
                }
            }
            throw new Exception("Unable to get the Result token from the HTTP response content");
        }

        internal static async Task<TicketDetails> GetTicketDetails(int ticketId)
        {
            var content = new StringContent($"{{TicketID: {ticketId}}}", Encoding.UTF8, "application/json");//new FormUrlEncodedContent(filters);
            var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + APIpaths.TicketsDetails.ToString())
            {
                Content = content
            };
            var response = await client.SendAsync(msg);
            var responseString = await response.Content.ReadAsStringAsync();
            var responseObject = JObject.Parse(responseString);
            if (responseObject.TryGetValue("Result", out JToken token))
            {
                Console.WriteLine(token);
                var ticketDetails = token.ToObject<TicketDetails>();
                return ticketDetails;
            }
            throw new Exception("Unable to get the Result token from the HTTP response content");
        }

        static async Task<List<MaintenanceTicket>> GetTicketsFromResponse(HttpResponseMessage response)
        {
            var responseString = await response.Content.ReadAsStringAsync();
            var responseObject = JObject.Parse(responseString);
            if (responseObject.TryGetValue("Result", out JToken token))
            {
                try
                {
                    var dic = token.ToObject<List<MaintenanceTicket>>();
                    return dic;
                }
                catch //result property types have changed in the backend
                {
                    return new List<MaintenanceTicket>();
                }
            }
            else
            {
                throw new Exception("Unable to get the Result token from the HTTP response content");
            }
        }
    }
}

