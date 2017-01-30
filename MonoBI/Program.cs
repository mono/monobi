
using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Data.SqlClient;

namespace MonoBI
{
	class MainClass
	{
		public static void Main()
		{
			string jobName;
			string platformName;
			int buildId;
			string buildURL;

			string envJobName = Environment.GetEnvironmentVariable("UPSTREAM_JOB_NAME");
			if (envJobName == null)
			{
				Console.Error.WriteLine("We need \"JOB_NAME\" env variable defined");
				Environment.Exit(1);
			}

			string envBuildId = Environment.GetEnvironmentVariable("UPSTREAM_BUILD_ID");
			if (envBuildId == null)
			{
				Console.Error.WriteLine("We need \"BUILD_ID\" env variable defined");
				Environment.Exit(2);
			}

			string envBuildURL = Environment.GetEnvironmentVariable("UPSTREAM_BUILD_URL");
			if (envBuildURL == null)
			{
				Console.Error.WriteLine("We need \"BUILD_URL\" env variable defined");
				Environment.Exit(3);
			}

			int idx;
			if ((idx = envJobName.IndexOf("/label=", StringComparison.InvariantCulture)) != -1)
			{
				jobName = envJobName.Substring(0, idx);
				platformName = envJobName.Substring(idx + "/label=".Length, envJobName.Length - idx - "/label=".Length);
			}
			else
			{
				jobName = envJobName;

				string envLabel = Environment.GetEnvironmentVariable("UPSTREAM_LABEL");
				if (envLabel == null)
				{
					Console.Error.WriteLine("We need \"label\" env variable defined");
					Environment.Exit(5);
				}

				platformName = envLabel;
			}

			buildId = int.Parse(envBuildId);
			buildURL = envBuildURL;

			UploadBuild(jobName, platformName, buildId, buildURL);
		}

		static void UploadBuild(string jobName, string platformName, int buildId, string buildURL)
		{
			string envSQLConnection = Environment.GetEnvironmentVariable("SQL_CONNECTION");
			if (envSQLConnection == null)
			{
				Console.Error.WriteLine("We need \"SQL_CONNECTION\" env variable defined");
				Environment.Exit(4);
			}

			using (WebClient webClient = new WebClient())
			using (SqlConnection sqlConnection = new SqlConnection(envSQLConnection))
			{
				sqlConnection.Open();

				string json = webClient.DownloadString(
					buildURL + (buildURL.EndsWith("/", StringComparison.CurrentCulture) ? "" : "/") +
						"api/json?pretty=1&tree=actions[individualBlobs[*],parameters[*],lastBuiltRevision[*],remoteUrls[*]],timestamp,building,result,id,url");

				Build build = new Build
				{
					JobName = jobName,
					PlatformName = platformName,
					BuildId = buildId,
					BuildURL = buildURL,
				};

				JsonConvert.PopulateObject(json, build);

				using (SqlTransaction sqlTransaction = sqlConnection.BeginTransaction())
				{
					InsertBuild(sqlConnection, sqlTransaction, build);

					foreach (Failure failure in GetFailures(json, webClient))
					{
						InsertFailure(sqlConnection, sqlTransaction, build, failure);
					}

					sqlTransaction.Commit();
				}
			}
		}

		static IEnumerable<Failure> GetFailures(string json, WebClient client)
		{
			JToken jtoken = JToken.Parse(json);
			JToken actions = jtoken["actions"];

			if (actions == null)
				yield break;

			string babysitterURL = null;

			foreach (JToken action in actions.Children())
			{
				if (!action.HasValues)
					continue;

				string _class = action["_class"].Value<string>();
				if (_class != "com.microsoftopentechnologies.windowsazurestorage.AzureBlobAction")
					continue;

				JToken individualBlobs = action["individualBlobs"];
				if (individualBlobs == null)
					continue;

				if (!individualBlobs.HasValues)
					continue;

				string blobURL = individualBlobs[0]["blobURL"].Value<string>();
				if (string.IsNullOrEmpty(blobURL))
					continue;

				babysitterURL = blobURL;
				break;
			}

			if (babysitterURL == null)
				yield break;

			foreach (string line in client.DownloadString(babysitterURL).Split('\n'))
			{
				if (string.IsNullOrWhiteSpace(line))
					continue;

				JToken jstep = JToken.Parse(line);
				if (jstep == null)
					continue;

				JToken tests = jstep["tests"];
				if (tests == null)
					continue;

				foreach (JToken test in tests.Children())
				{
					yield return new Failure { TestName = test.Value<JProperty>().Name };
				}
			}
		}

		static void InsertBuild(SqlConnection sqlConnection, SqlTransaction sqlTransaction, Build build)
		{
			Console.WriteLine(build);

			using (SqlCommand sqlCommand = new SqlCommand(
				"INSERT INTO Builds (JobName, PlatformName, BuildId, Result, DateTime, URL) " +
					"VALUES (@JobName, @PlatformName, @BuildId, @Result, @DateTime, @URL)", sqlConnection, sqlTransaction))
			{
				sqlCommand.Parameters.Add(new SqlParameter("JobName", build.JobName));
				sqlCommand.Parameters.Add(new SqlParameter("PlatformName", build.PlatformName));
				sqlCommand.Parameters.Add(new SqlParameter("BuildId", build.BuildId));
				sqlCommand.Parameters.Add(new SqlParameter("Result", build.Result));
				sqlCommand.Parameters.Add(new SqlParameter("DateTime", build.DateTime));
				sqlCommand.Parameters.Add(new SqlParameter("URL", build.BuildURL));

				sqlCommand.ExecuteNonQuery();
			}
		}

		static void InsertFailure(SqlConnection sqlConnection, SqlTransaction sqlTransaction, Build build, Failure failure)
		{
			Console.WriteLine(failure);

			using (SqlCommand sqlCommand = new SqlCommand(
				"INSERT INTO FailedTests (JobName, PlatformName, BuildId, TestName) " +
					"VALUES (@JobName, @PlatformName, @BuildId, @TestName)", sqlConnection, sqlTransaction))
			{
				sqlCommand.Parameters.Add(new SqlParameter("JobName", build.JobName));
				sqlCommand.Parameters.Add(new SqlParameter("PlatformName", build.PlatformName));
				sqlCommand.Parameters.Add(new SqlParameter("BuildId", build.BuildId));
				sqlCommand.Parameters.Add(new SqlParameter("TestName", failure.TestName));

				sqlCommand.ExecuteNonQuery();
			}
		}
	}
}
