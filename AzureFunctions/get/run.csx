#r "System.Configuration"
#r "System.Data"

#load "../shared/Build.csx"
#load "../shared/FailedTest.csx"

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net;
using System.Threading;
using System.Net.Http.Formatting;

public static HttpResponseMessage Run(HttpRequestMessage req, TraceWriter log)
{
    List<Build> builds = new List<Build>();

    using (SqlConnection sqlConnection = new SqlConnection(Environment.GetEnvironmentVariable("AzureSqlDatabaseConnectionString")))
    {
        sqlConnection.Open();

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
            "FROM Builds " +
            "ORDER BY Builds.JobName, Builds.PlatformName, Builds.BuildId", sqlConnection))
        using (SqlDataReader sqlReader = sqlCommand.ExecuteReader())
        {
            Build build = null;
            while (sqlReader.Read())
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

                    log.Info($"Fetch build {build.JobName} {build.PlatformName} {build.Id}");
                }
            }
        }
    }

    return req.CreateResponse<List<Build>>(HttpStatusCode.OK, builds, new JsonMediaTypeFormatter());
}
