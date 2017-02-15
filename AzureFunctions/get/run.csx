#r "System.Configuration"
#r "System.Data"

#load "../shared/Build.csx"
#load "../shared/FailedTest.csx"

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net;
using System.Threading;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, CancellationToken token)
{
    List<Build> builds = new List<Build>();

    using (SqlConnection sqlConnection = new SqlConnection(Environment.GetEnvironmentVariable("AzureSqlDatabaseConnectionString")))
    {
        await sqlConnection.OpenAsync(token);

        using (SqlCommand sqlCommand = new SqlCommand(
            "SELECT " +
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
            "ORDER BY Builds.JobName, Builds.PlatformName, Builds.BuildId", sqlConnection))
        using (SqlDataReader sqlReader = await sqlCommand.ExecuteReaderAsync(token))
        {
            Build build = null;
            while (await sqlReader.ReadAsync(token))
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

    return req.CreateResponse(HttpStatusCode.OK, builds);
}
