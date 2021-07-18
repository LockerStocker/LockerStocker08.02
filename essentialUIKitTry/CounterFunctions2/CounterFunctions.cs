using System;
using System.Linq;
using System.Timers;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Logging;
//using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;

namespace CounterFunctions
{
    public static class CounterFunctions
    {
        private static readonly AzureSignalR SignalR = new AzureSignalR(Environment.GetEnvironmentVariable("AzureSignalRconnectionString"));
        const double interval60Minutes = 60 * 5 * 1000; // milliseconds to one hour


        [FunctionName("negotiate")]
        public static async Task<SignalRConnectionInfo> NegotiateConnection(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage request,
            ILogger log)
        {
            try
            {
                log.LogInformation($"Negotiating connection for user: < {request.Content.ReadAsStringAsync()}>.");
                ConnectionRequest connectionRequest = await ExtractContent<ConnectionRequest>(request);
                log.LogInformation($"Negotiating connection for user: <{connectionRequest.UserId}>.");

                string clientHubUrl = SignalR.GetClientHubUrl("CounterHub");
                string accessToken = SignalR.GenerateAccessToken(clientHubUrl, connectionRequest.UserId);
                return new SignalRConnectionInfo { AccessToken = accessToken, Url = clientHubUrl };
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to negotiate connection.");
                throw;
            }
        }


        // -------------------- OCCUPY AND AVAILABLE ------------------------------
        [FunctionName("set-occupy")]
        public static async Task SetOccupy(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage request,
            [Table("LockerRoom")] CloudTable cloudTable,
            [SignalR(HubName = "CounterHub")] IAsyncCollector<SignalRMessage> signalRMessages,
            ILogger log)
        {
            log.LogInformation("Setting Occupy.");

            Locker counterRequest = await ExtractContent<Locker>(request);

            Locker cloudLocker = await GetOrCreateCounter(cloudTable, counterRequest.Id);
            cloudLocker.available = false;
            cloudLocker.user_key = counterRequest.user_key;
            cloudLocker.release_time = DateTimeOffset.Now.AddHours(1);
            ScheduleRelease(cloudLocker, cloudTable, signalRMessages);
            TableOperation updateOperation = TableOperation.Replace(cloudLocker);
            await cloudTable.ExecuteAsync(updateOperation);
            await signalRMessages.AddAsync(
                new SignalRMessage
                {
                    Target = "CounterUpdate",
                    Arguments = new object[] { cloudLocker }
                });
        }


        [FunctionName("set-available")]
        public static async Task SetAvailable(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage request,
    [Table("LockerRoom")] CloudTable cloudTable,
    [SignalR(HubName = "CounterHub")] IAsyncCollector<SignalRMessage> signalRMessages,
    ILogger log)
        {
            log.LogInformation("Setting Available.");

            Locker counterRequest = await ExtractContent<Locker>(request);

            Locker cloudLocker = await GetOrCreateCounter(cloudTable, counterRequest.Id);
            cloudLocker.available = true;
            cloudLocker.release_time = DateTimeOffset.Now.AddHours(0);
            cloudLocker.user_key = "";
            TableOperation updateOperation = TableOperation.Replace(cloudLocker);
            await cloudTable.ExecuteAsync(updateOperation);
            await signalRMessages.AddAsync(
                new SignalRMessage
                {
                    Target = "CounterUpdate",
                    Arguments = new object[] { cloudLocker }
                });
        }




        // -------------------- SET COSTS ------------------------------
        [FunctionName("set-cost")]
        public static async Task SetCost(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage request,
            [Table("LockerRoom")] CloudTable cloudTable,
            [SignalR(HubName = "CounterHub")] IAsyncCollector<SignalRMessage> signalRMessages,
            ILogger log)
        {
            Locker counterRequest = await ExtractContent<Locker>(request);
            Locker cloudLocker = await GetOrCreateCounter(cloudTable, counterRequest.Id);

            cloudLocker.price_per_hour = counterRequest.price_per_hour;
            ScheduleRelease(cloudLocker, cloudTable, signalRMessages);
            TableOperation updateOperation = TableOperation.Replace(cloudLocker);
            await cloudTable.ExecuteAsync(updateOperation);
            await signalRMessages.AddAsync(
                new SignalRMessage
                {
                    Target = "CounterUpdate",
                    Arguments = new object[] { cloudLocker }
                });
        }











        // -------------------- LOCK AND UNLOCK ------------------------------
        [FunctionName("set-lock")]
        public static async Task SetLock(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage request,
            [Table("LockerRoom")] CloudTable cloudTable,
            [SignalR(HubName = "CounterHub")] IAsyncCollector<SignalRMessage> signalRMessages,
            ILogger log)
        {
            log.LogInformation("Setting Occupy.");

            Locker counterRequest = await ExtractContent<Locker>(request);

            Locker cloudCounter = await GetOrCreateCounter(cloudTable, counterRequest.Id);
            cloudCounter.locked = true;
            TableOperation updateOperation = TableOperation.Replace(cloudCounter);
            await cloudTable.ExecuteAsync(updateOperation);
            await signalRMessages.AddAsync(
                new SignalRMessage
                {
                    Target = "CounterUpdate",
                    Arguments = new object[] { cloudCounter }
                });
        }


        [FunctionName("set-unlock")]
        public static async Task SetUnlock(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage request,
    [Table("LockerRoom")] CloudTable cloudTable,
    [SignalR(HubName = "CounterHub")] IAsyncCollector<SignalRMessage> signalRMessages,
    ILogger log)
        {
            log.LogInformation("Setting Available.");

            Locker counterRequest = await ExtractContent<Locker>(request);

            Locker cloudCounter = await GetOrCreateCounter(cloudTable, counterRequest.Id);
            cloudCounter.locked = false;
            TableOperation updateOperation = TableOperation.Replace(cloudCounter);
            await cloudTable.ExecuteAsync(updateOperation);
            await signalRMessages.AddAsync(
                new SignalRMessage
                {
                    Target = "CounterUpdate",
                    Arguments = new object[] { cloudCounter }
                });
        }



        // --------------------  GET/SET USER KEY  ------------------------------
            [FunctionName("set-user-key")]
            public static async Task SetUserKey(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "set-user-key/{user_key}")] HttpRequestMessage request,
            [Table("LockerRoom")] CloudTable cloudTable,
            [SignalR(HubName = "CounterHub")] IAsyncCollector<SignalRMessage> signalRMessages,
            ILogger log,
            String user_key)
        {
            log.LogInformation("Setting user key.");
            Locker counterRequest = await ExtractContent<Locker>(request);
            Locker cloudLocker = await GetOrCreateCounter(cloudTable, counterRequest.Id);
            cloudLocker.user_key = user_key;
            TableOperation updateOperation = TableOperation.Replace(cloudLocker);
            await cloudTable.ExecuteAsync(updateOperation);
            await signalRMessages.AddAsync(
                new SignalRMessage
                {
                    Target = "CounterUpdate",
                    Arguments = new object[] { cloudLocker }
                });
        }


        [FunctionName("get-user-key")]
        public static async Task<string> GetUserKey(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "get-user-key/{id}")] HttpRequestMessage request,
            [Table("LockerRoom")] CloudTable cloudTable,
            string id,
            ILogger log)
        {
            log.LogInformation("Getting User Key.");
            Locker locker = await GetOrCreateCounter(cloudTable, int.Parse(id));
            log.LogInformation("Returning: " + locker.user_key);
            return locker.user_key;
        }





        // ------------------------------------------------------------------

        [FunctionName("get-locker")]
        public static async Task<Locker> GetLocker(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "get-locker/{id}")] HttpRequestMessage request,
            [Table("LockerRoom")] CloudTable cloudTable,
            string id,
            ILogger log)
        {
            log.LogInformation("Getting locker.");
            Locker locker = await GetOrCreateCounter(cloudTable, int.Parse(id));
            string locker_str = JsonConvert.SerializeObject(locker);
            log.LogInformation("Getting locker. " + locker_str);
            return locker;
            //return locker_str;
        }


        private static async Task<T> ExtractContent<T>(HttpRequestMessage request)
        {
            string connectionRequestJson = await request.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(connectionRequestJson);
        }

        public static async Task<Locker> GetOrCreateCounter(CloudTable cloudTable, int counterId)
        {
            TableQuery<Locker> idQuery = new TableQuery<Locker>()
                .Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, counterId.ToString()));

            TableQuerySegment<Locker> queryResult = await cloudTable.ExecuteQuerySegmentedAsync(idQuery, null);
            Locker cloudLocker = queryResult.FirstOrDefault();
            if (cloudLocker == null)
            {
                cloudLocker = new Locker { Id = counterId };
                TableOperation insertOperation = TableOperation.InsertOrReplace(cloudLocker);
                cloudLocker.PartitionKey = "locker";
                cloudLocker.RowKey = cloudLocker.Id.ToString();
                TableResult tableResult = await cloudTable.ExecuteAsync(insertOperation);
                return await GetOrCreateCounter(cloudTable, counterId);
            }

            return cloudLocker;
        }


        //public static void checkForTime_Elapsed(object sender, ElapsedEventArgs e, Locker locker)
        public static async void checkForTime_Elapsed(object sender, Locker locker,
            [Table("LockerRoom")] CloudTable cloudTable,
            [SignalR(HubName = "CounterHub")] IAsyncCollector<SignalRMessage> signalRMessages)
        {
            locker.locked = false;
            locker.available = true;
            locker.user_key = "";
            TableOperation updateOperation = TableOperation.Replace(locker);
            await cloudTable.ExecuteAsync(updateOperation);
            await signalRMessages.AddAsync(
                new SignalRMessage
                {
                    Target = "CounterUpdate",
                    Arguments = new object[] { locker }
                });
        }

        public static async void ScheduleRelease(Locker locker,
            [Table("LockerRoom")] CloudTable cloudTable,
            [SignalR(HubName = "CounterHub")] IAsyncCollector<SignalRMessage> signalRMessages)
        {
            Timer checkForTime = new Timer(interval60Minutes);
            checkForTime.Elapsed += (sender, args) => checkForTime_Elapsed(sender, locker, cloudTable, signalRMessages);
            checkForTime.Enabled = true;
        }

    }
}
