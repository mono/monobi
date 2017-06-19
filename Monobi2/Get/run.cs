using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

using System.Data.SqlClient;
using System;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host;

namespace Monobi2
{
    public static class Get
    {
        [FunctionName("Get")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            List<Build> builds = new List<Build>();

            string jobName = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "jobName", true) == 0)
                .Value ?? "";
            string platformName = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "platformName", true) == 0)
                .Value ?? "";

            string top = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "top", true) == 0)
                .Value ?? "50";

            // parse query parameter
            string laterThan = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "laterThan", true) == 0)
                .Value ?? "1753-01-01";

            string olderThan = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "olderThan", true) == 0)
                .Value ?? "";

            // Get request body
            //dynamic data = await req.Content.ReadAsAsync<object>();

            // Set name to query string or body data
            //name = name ?? data?.name;


            using (SqlConnection sqlConnection = new SqlConnection(Environment.GetEnvironmentVariable("AzureSqlDatabaseConnectionString")))
            {
                await sqlConnection.OpenAsync();

                SqlParameter[] sqlParameters = new SqlParameter[] {
                  new SqlParameter { ParameterName = "@laterThan", Value = laterThan },
                  new SqlParameter { ParameterName = "@olderThan", Value = olderThan },
                  new SqlParameter { ParameterName = "@jobName", Value = jobName },
                  new SqlParameter { ParameterName = "@platformName", Value = platformName },
                };

                string sqlQuery = "SELECT " +
                    "  Builds.JobName " +
                    ", Builds.PlatformName " +
                    ", Builds.BuildId " +
                    ", Builds.Url " +
                    ", Builds.Result " +
                    ", Builds.DateTime " +
                    ", Builds.BabysitterUrl " +
                    ", Builds.GitHash " +
                    ", Builds.PrId " +
                    ", Builds.PrUrl " +
                    ", Builds.PrTitle " +
                    ", Builds.PrAuthor " +
                    ", FailedTests.TestName " +
                    ", FailedTests.Invocation " +
                    ", FailedTests.Failure " +
                    ", FailedTests.FinalCode " +
                "FROM Builds " +
                "LEFT JOIN FailedTests ON " +
                    "    Builds.JobName = FailedTests.JobName " +
                    "AND Builds.PlatformName = FailedTests.PlatformName " +
                    "AND Builds.BuildId = FailedTests.BuildId " +

                "WHERE Builds.DateTime > @laterThan ";
                if (olderThan != "")
                    sqlQuery += "AND Builds.DateTime < @olderThan ";
                if (jobName != "")
                    sqlQuery += "AND Builds.JobName = @jobName ";
                if (platformName != "")
                    sqlQuery += "AND Builds.PlatformName = @platformName ";

                sqlQuery += "ORDER BY Builds.JobName, Builds.PlatformName, Builds.BuildId";

                SqlCommand sqlCommand = new SqlCommand(sqlQuery, sqlConnection);

                sqlCommand.Parameters.AddRange(sqlParameters);

                using (sqlCommand)
                using (SqlDataReader sqlReader = await sqlCommand.ExecuteReaderAsync())
                {
                    Build build = null;
                    while (await sqlReader.ReadAsync())
                    {
                        if (build == null || sqlReader.GetString(0) != build.JobName || sqlReader.GetString(1) != build.PlatformName || sqlReader.GetInt32(2) != build.Id)
                        {
                            if (build != null)
                            {
                                builds.Add(build);
                            }

                            build = new Build(sqlReader.GetString(0),
                                              sqlReader.GetString(1),
                                              sqlReader.GetInt32(2),
                                              sqlReader.GetString(3),
                                              sqlReader.GetString(4),
                                              sqlReader.GetDateTime(5),
                                              sqlReader.IsDBNull(6) ? null : sqlReader.GetString(6),
                                              sqlReader.IsDBNull(7) ? null : sqlReader.GetString(7),
                                              sqlReader.GetInt32(8),
                                              sqlReader.IsDBNull(9) ? null : sqlReader.GetString(9),
                                              sqlReader.IsDBNull(10) ? null : sqlReader.GetString(10),
                                              sqlReader.IsDBNull(11) ? null : sqlReader.GetString(11));

                            //log.Warning($"build f: {build}");

                        }

                        if (!sqlReader.IsDBNull(12))
                        {
                            build.FailedTests.Add(new FailedTest(sqlReader.GetString(12),
                                                                 sqlReader.GetString(13),
                                                                 sqlReader.GetString(14),
                                                                 sqlReader.GetInt32(15)));
                        }
                    }
                }
            }

            log.Warning(builds.ToString());

            return req.CreateResponse(HttpStatusCode.OK, builds, "application/json");

            /*
            return name == null
            ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a name on the query string or in the request body")
            : req.CreateResponse(HttpStatusCode.OK, "Hello " + name);
            */
        }
    }
}