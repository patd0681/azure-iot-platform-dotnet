
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;

namespace Mmm.Platform.IoT.Common.TestHelpers
{
    public class ResultQuery : IQuery
    {
        private const string DeviceIdKey = "DeviceId";
        private readonly List<Twin> results;
        private readonly List<string> deviceQueryResults;

        public ResultQuery(int numResults)
        {
            this.results = new List<Twin>();
            this.deviceQueryResults = new List<string>();
            for (int i = 0; i < numResults; i++)
            {
                this.results.Add(ResultQuery.CreateTestTwin(i));
                this.deviceQueryResults.Add($"{{'{DeviceIdKey}':'device{i}'}}");
                this.HasMoreResults = true;
            }
        }

        public ResultQuery(List<Twin> twins)
        {
            this.results = twins;
            this.deviceQueryResults = twins.Select(x => $"{{'{DeviceIdKey}':'device{x.DeviceId}'}}").ToList();
            this.HasMoreResults = true;
        }

        public bool HasMoreResults { get; set; }

        public Task<IEnumerable<Twin>> GetNextAsTwinAsync()
        {
            this.HasMoreResults = false;
            return Task.FromResult<IEnumerable<Twin>>(this.results);
        }

        public Task<QueryResponse<Twin>> GetNextAsTwinAsync(QueryOptions options)
        {
            this.HasMoreResults = false;
            QueryResponse<Twin> resultResponse;

            if (string.IsNullOrEmpty(options.ContinuationToken))
            {
                resultResponse = new QueryResponse<Twin>(this.results, "continuationToken");
            }
            else
            {
                var index = int.Parse(options.ContinuationToken);
                var count = this.results.Count - index;

                var continuedResults = new List<Twin>();
                if (index < count)
                {
                    continuedResults = this.results.GetRange(index, count);
                }

                resultResponse = new QueryResponse<Twin>(continuedResults, "continuationToken");
            }

            return Task.FromResult(resultResponse);
        }

        public Task<IEnumerable<DeviceJob>> GetNextAsDeviceJobAsync()
        {
            throw new System.NotImplementedException();
        }

        public Task<QueryResponse<DeviceJob>> GetNextAsDeviceJobAsync(QueryOptions options)
        {
            throw new System.NotImplementedException();
        }

        public Task<IEnumerable<JobResponse>> GetNextAsJobResponseAsync()
        {
            throw new System.NotImplementedException();
        }

        public Task<QueryResponse<JobResponse>> GetNextAsJobResponseAsync(QueryOptions options)
        {
            throw new System.NotImplementedException();
        }

        public Task<IEnumerable<string>> GetNextAsJsonAsync()
        {
            this.HasMoreResults = false;
            return Task.FromResult(deviceQueryResults.AsEnumerable());
        }

        public Task<QueryResponse<string>> GetNextAsJsonAsync(QueryOptions options)
        {
            throw new System.NotImplementedException();
        }

        private static Twin CreateTestTwin(int valueToReport)
        {
            var twin = new Twin($"device{valueToReport}")
            {
                Properties = new TwinProperties(),
            };
            twin.Properties.Reported = new TwinCollection("{\"test\":\"value" + valueToReport + "\"}");
            twin.Properties.Desired = new TwinCollection("{\"test\":\"value" + valueToReport + "\"}");
            return twin;
        }
    }
}
