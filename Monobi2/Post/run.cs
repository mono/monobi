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
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace Monobi2
{
    public static class run
    {
        [FunctionName("Post")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req, TraceWriter log, CancellationToken token)
        {
            string buildUrl = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "buildUrl", true) == 0)
                .Value ?? "";

            //string buildUrl = await req.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(buildUrl))
                return req.CreateResponse(HttpStatusCode.BadRequest, "missing \"buildUrl\" parameter");

            XElement xml = await RequestXML(buildUrl, token);

            List<Build> builds = new List<Build>();

            if (xml.Name != "matrixBuild")
            {
                builds.Add(await GetBuild(xml, token));
            }
            else
            {
                builds.AddRange(await GetMatrixBuilds(xml, token));
            }

            using (SqlConnection sqlConnection = new SqlConnection(Environment.GetEnvironmentVariable("AzureSqlDatabaseConnectionString")))
            {
                await sqlConnection.OpenAsync(token);

                using (SqlTransaction sqlTransaction = sqlConnection.BeginTransaction())
                {
                    using (SqlCommand sqlCommand = new SqlCommand(
                        "INSERT INTO Builds (JobName, PlatformName, BuildId, Result, DateTime, Url, BabysitterUrl, GitHash, PrId, PrUrl, PrTitle, PrAuthor) " +
                            "VALUES (@JobName, @PlatformName, @BuildId, @Result, @DateTime, @Url, @BabysitterUrl, @GitHash, @PrId, @PrUrl, @PrTitle, @PrAuthor)", sqlConnection, sqlTransaction))
                    {
                        foreach (Build build in builds)
                        {
                            sqlCommand.Parameters.Clear();
                            sqlCommand.Parameters.Add(new SqlParameter("JobName", build.JobName));
                            sqlCommand.Parameters.Add(new SqlParameter("PlatformName", build.PlatformName));
                            sqlCommand.Parameters.Add(new SqlParameter("BuildId", build.Id));
                            sqlCommand.Parameters.Add(new SqlParameter("Url", build.Url));
                            sqlCommand.Parameters.Add(new SqlParameter("Result", build.Result));
                            sqlCommand.Parameters.Add(new SqlParameter("DateTime", build.DateTime));
                            sqlCommand.Parameters.Add(new SqlParameter("BabysitterUrl", build.BabysitterUrl != null ? (object)build.BabysitterUrl : (object)DBNull.Value));
                            sqlCommand.Parameters.Add(new SqlParameter("GitHash", build.GitHash != null ? (object)build.GitHash : (object)DBNull.Value));
                            sqlCommand.Parameters.Add(new SqlParameter("PrId", build.PrId));
                            sqlCommand.Parameters.Add(new SqlParameter("PrUrl", build.PrUrl != null ? (object)build.PrUrl : (object)DBNull.Value));
                            sqlCommand.Parameters.Add(new SqlParameter("PrTitle", build.PrTitle != null ? (object)build.PrTitle : (object)DBNull.Value));
                            sqlCommand.Parameters.Add(new SqlParameter("PrAuthor", build.PrAuthor != null ? (object)build.PrAuthor : (object)DBNull.Value));

                            await sqlCommand.ExecuteNonQueryAsync(token);
                        }
                    }

                    using (SqlCommand sqlCommand = new SqlCommand(
                        "INSERT INTO FailedTests (JobName, PlatformName, BuildId, TestName, Invocation, Failure, FinalCode) " +
                            "VALUES (@JobName, @PlatformName, @BuildId, @TestName, @Invocation, @Failure, @FinalCode)", sqlConnection, sqlTransaction))
                    {
                        foreach (Build build in builds)
                        {
                            foreach (FailedTest failedTest in build.FailedTests)
                            {
                                sqlCommand.Parameters.Clear();
                                sqlCommand.Parameters.Add(new SqlParameter("JobName", build.JobName));
                                sqlCommand.Parameters.Add(new SqlParameter("PlatformName", build.PlatformName));
                                sqlCommand.Parameters.Add(new SqlParameter("BuildId", build.Id));
                                sqlCommand.Parameters.Add(new SqlParameter("TestName", failedTest.TestName));
                                sqlCommand.Parameters.Add(new SqlParameter("Invocation", failedTest.Invocation));
                                sqlCommand.Parameters.Add(new SqlParameter("Failure", failedTest.Failure));
                                sqlCommand.Parameters.Add(new SqlParameter("FinalCode", failedTest.FinalCode));

                                await sqlCommand.ExecuteNonQueryAsync(token);
                            }
                        }
                    }

                    sqlTransaction.Commit();
                }
            }
            
            return req.CreateResponse(HttpStatusCode.OK, builds, "application/json");

            /*
            return name == null
            ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a name on the query string or in the request body")
            : req.CreateResponse(HttpStatusCode.OK, "Hello " + name);
            */
        }


        static async Task<IEnumerable<Build>> GetMatrixBuilds(XElement xml, CancellationToken token)
        {
            List<Build> builds = new List<Build>();

            foreach (XElement run in xml.Elements("run"))
            {
                IEnumerable<XElement> actions;

                actions = run.Elements("action");

                // Check if <action _class="hudson.model.CauseAction" /> exists
                actions = actions.Where(action => action.Attribute("_class")?.Value?.Equals("hudson.model.CauseAction") ?? false);

                // Check if <action><cause _class="hudson.model.Cause$UpstreamCause" /></action> exists
                actions = actions.Where(action => action.Element("cause")?.Attribute("_class")?.Value?.Equals("hudson.model.Cause$UpstreamCause") ?? false);

                // Check if <action><cause><upstreamBuild/></cause></action> is equal to <id/>
                actions = actions.Where(action => action.Element("cause")?.Element("upstreamBuild")?.Value?.Equals(xml.Element("id").Value) ?? false);

                if (!actions.Any())
                    continue;

                builds.Add(await GetBuild(run, token));
            }

            return builds;
        }

        async static Task<Build> GetBuild(XElement xml, CancellationToken token)
        {
            string platformName;
            Build build = IsPrBuild(xml) ?
                new Build(GetJobName(xml, out platformName),
                          platformName,
                          GetBuildId(xml),
                          GetBuildUrl(xml),
                          GetBuildResult(xml),
                          GetBuildDateTime(xml),
                          GetBabysitterUrl(xml),
                          GetPrGitHash(xml),
                          GetPrId(xml),
                          GetPrUrl(xml),
                          GetPrTitle(xml),
                          GetPrAuthor(xml)) :
                new Build(GetJobName(xml, out platformName),
                          platformName,
                          GetBuildId(xml),
                          GetBuildUrl(xml),
                          GetBuildResult(xml),
                          GetBuildDateTime(xml),
                          GetBabysitterUrl(xml),
                          GetGitHash(xml));

            foreach (FailedTest failedTest in await GetFailedTests(build.BabysitterUrl, token))
            {
                build.FailedTests.Add(failedTest);
            }

            return build;
        }

        static bool IsPrBuild(XElement xml)
        {
            IEnumerable<XElement> actions;

            actions = xml.Elements("action");

            // Check if <action _class="hudson.model.CauseAction" /> exists
            actions = actions.Where(a => a.Attribute("_class")?.Value?.Equals("hudson.model.CauseAction") ?? false);

            // Check if <action><cause _class="org.jenkinsci.plugins.ghprb.GhprbCause" /></action> exists
            actions = actions.Where(a => a.Element("cause")?.Attribute("_class")?.Value?.Equals("org.jenkinsci.plugins.ghprb.GhprbCause") ?? false);

            return actions.Any();
        }

        static string GetJobName(XElement xml, out string platformName)
        {
            string[] uriSegments = new Uri(xml.Element("url").Value).Segments;

            int jobIndex = Array.FindLastIndex(uriSegments, s => s == "job/");

            if (uriSegments[jobIndex + 2].StartsWith("label=", StringComparison.Ordinal))
                platformName = uriSegments[jobIndex + 2].Substring("label=".Length, uriSegments[jobIndex + 2].Length - "label=".Length - 1);
            else
                platformName = "";

            return uriSegments[jobIndex + 1].Substring(0, uriSegments[jobIndex + 1].Length - 1);
        }

        static int GetBuildId(XElement xml)
        {
            return int.Parse(xml.Element("id").Value);
        }

        static string GetBuildUrl(XElement xml)
        {
            return xml.Element("url").Value;
        }

        static string GetBuildResult(XElement xml)
        {
            return xml.Element("result").Value.ToLower();
        }

        static DateTime GetBuildDateTime(XElement xml)
        {
            return new DateTime(1970, 1, 1).AddMilliseconds(long.Parse(xml.Element("timestamp").Value));
        }

        static string GetBabysitterUrl(XElement xml)
        {
            IEnumerable<XElement> actions;

            actions = xml.Elements("action");

            // Check if <action _class="com.microsoftopentechnologies.windowsazurestorage.AzureBlobAction" /> exists
            actions = actions.Where(a => a.Attribute("_class")?.Value?.Equals("com.microsoftopentechnologies.windowsazurestorage.AzureBlobAction") ?? false);

            // Check if <action><individualBlob><blobUrl /></individualBlob></action> exists and is not empty
            actions = actions.Where(a => !string.IsNullOrWhiteSpace(a.Element("individualBlob")?.Element("blobURL")?.Value));

            // Return first <action><individualBlob><blobUrl /></individualBlob></action> or null
            return actions.Select(a => a.Element("individualBlob").Element("blobURL").Value).FirstOrDefault<string>();
        }

        static string GetGitHash(XElement xml)
        {
            XElement changeSet;

            changeSet = xml.Element("changeSet");
            if (changeSet == null)
                return null;

            // Check if <changeSet _class="org.jenkinsci.plugins.multiplescms.MultiSCMChangeLogSet" /> exists
            if (!(changeSet.Attribute("_class")?.Value?.Equals("org.jenkinsci.plugins.multiplescms.MultiSCMChangeLogSet") ?? false))
                return null;

            // Check if <changeSet><item _class="hudson.plugins.git.GitChangeSet" /></changeSet> exists
            if (!(changeSet.Element("item")?.Attribute("_class")?.Value?.Equals("hudson.plugins.git.GitChangeSet") ?? false))
                return null;

            // Return <changeSet><item><commitId /></item></changeSet>
            return changeSet.Element("item").Element("commitId").Value;
        }

        static string GetPrGitHash(XElement xml)
        {
            return GetPrField(xml, "ghprbActualCommit");
        }

        static int GetPrId(XElement xml)
        {
            return int.Parse(GetPrField(xml, "ghprbPullId") ?? "-1");
        }

        static string GetPrUrl(XElement xml)
        {
            return GetPrField(xml, "ghprbPullLink");
        }

        static string GetPrTitle(XElement xml)
        {
            return GetPrField(xml, "ghprbPullTitle");
        }

        static string GetPrAuthor(XElement xml)
        {
            return GetPrField(xml, "ghprbPullAuthorLogin");
        }

        static string GetPrField(XElement xml, string field)
        {
            IEnumerable<XElement> actions;

            actions = xml.Elements("action");

            // Check if <action _class="hudson.model.ParametersAction" /> exists
            actions = actions.Where(a => a.Attribute("_class")?.Value?.Equals("hudson.model.ParametersAction") ?? false);

            IEnumerable<XElement> parameters;

            // Select all <action><parameter /></action>
            parameters = actions.SelectMany(a => a.Elements("parameter"));

            // Check if <action><parameter _class="hudson.model.StringParameterValue"></action> exists
            parameters = parameters.Where(p => p.Attribute("_class")?.Value?.Equals("hudson.model.StringParameterValue") ?? false);

            // Check if <action><parameter><name /></parameter></action> equals `field`
            parameters = parameters.Where(p => p.Element("name")?.Value?.Equals(field) ?? false);

            // Return first <action><parameter><value /></parameter></action>
            return parameters.Select(p => p.Element("value").Value).First();
        }

        static async Task<IEnumerable<FailedTest>> GetFailedTests(string babysitterUrl, CancellationToken token)
        {
            if (babysitterUrl == null)
                return Enumerable.Empty<FailedTest>();

            return (await RequestBabysitter(babysitterUrl, token))
                .SelectMany(t =>
                {
                    JToken tests = t["tests"];
                    if (tests == null)
                        return Enumerable.Empty<FailedTest>();

                    string invocation = t["invocation"].Value<string>();
                    int finalCode = t["final_code"].Value<int>();

                    return tests.Children().Select(test =>
                    {
                        string failure;
                        if (test.Value<JProperty>().Value.Children().Any(c => c.Value<JProperty>().Name.Equals("normal_failures")))
                            failure = "normal";
                        else if (test.Value<JProperty>().Value.Children().Any(c => c.Value<JProperty>().Name.Equals("timeout_failures")))
                            failure = "timeout";
                        else if (test.Value<JProperty>().Value.Children().Any(c => c.Value<JProperty>().Name.Equals("crash_failures")))
                            failure = "crash";
                        else
                            throw new NotImplementedException();

                        return new FailedTest(test.Value<JProperty>().Name, invocation, failure, finalCode);
                    });
                });
        }

        static async Task<XElement> RequestXML(string buildUrl, CancellationToken token)
        {
            string treeParameter =
                "id," +
                "result," +
                "building," +
                "timestamp," +
                "url," +
                "changeSet[" +
                    "items[" +
                        "commitId" +
                    "]" +
                "]," +
                "actions[" +
                    "causes[" +
                        "upstreamBuild," +
                        "upstreamProject," +
                        "upstreamUrl" +
                    "]," +
                    "parameters[" +
                        "name," +
                        "value" +
                    "]," +
                    "lastBuiltRevision[" +
                        "SHA1" +
                    "]," +
                    "individualBlobs[" +
                        "blobURL" +
                    "]" +
                "]";

            return XElement.Parse(await RequestUrl(buildUrl + (buildUrl.EndsWith("/", StringComparison.Ordinal) ? "" : "/") + $"api/xml?tree={treeParameter},runs[{treeParameter}]", token));
        }

        static async Task<IEnumerable<JToken>> RequestBabysitter(string babysitterUrl, CancellationToken token)
        {
            return (await RequestUrl(babysitterUrl, token))
                .Split('\n')
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => JToken.Parse(l))
                .Where(t => t != null);
        }

        static async Task<string> RequestUrl(string url, CancellationToken token)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                return await (await httpClient.GetAsync(url, token)).Content.ReadAsStringAsync();
            }
        }
    }
}