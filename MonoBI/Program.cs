
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace MonoBI
{
	class MainClass
	{
		static WebClient webClient;
		static SqlConnection sqlConnection;
		static SqlTransaction sqlTransaction;

		public static void Main()
		{
			string buildUrl = Environment.GetEnvironmentVariable("UPSTREAM_BUILD_URL");
			if (buildUrl == null)
			{
				Console.Error.WriteLine("We need \"BUILD_URL\" env variable defined");
				Environment.Exit(1);
			}

			string sqlConnectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION");
			if (sqlConnectionString == null)
			{
				Console.Error.WriteLine("We need \"SQL_CONNECTION\" env variable defined");
				Environment.Exit(2);
			}

			using (webClient = new WebClient())
			using (sqlConnection = new SqlConnection(sqlConnectionString))
			{
				sqlConnection.Open();

				using (sqlTransaction = sqlConnection.BeginTransaction())
				{
					XElement xml = RequestXML(buildUrl);

					if (IsMatrixBuild(xml))
						UploadMatrixBuild(xml);
					else
						UploadBuild(xml);

					sqlTransaction.Commit();
				}
			}
		}

		static XElement RequestXML(string buildURL)
		{
			string treeParameter =
				"tree=" +
					"id," +
					"result," +
					"building," +
					"timestamp," +
					"url," +
					"actions[" +
						"causes[" +
							"upstreamBuild," +
							"upstreamProject," +
							"upstreamUrl" +
						"]," +
						"individualBlobs[" +
							"blobURL" +
						"]" +
					"]," +
					"runs[" +
						"id," +
						"result," +
						"building," +
						"timestamp," +
						"url," +
						"actions[" +
							"causes[" +
								"upstreamBuild," +
								"upstreamProject," +
								"upstreamUrl" +
							"]," +
							"individualBlobs[" +
								"blobURL" +
							"]" +
						"]" +
					"]";

			string url = buildURL + (buildURL.EndsWith("/", StringComparison.Ordinal) ? "" : "/") + "api/xml?" + treeParameter;

			Console.WriteLine($"Querying {url}");

			return XElement.Parse(webClient.DownloadString(url));
		}

		static bool IsMatrixBuild(XElement xmlDocument)
		{
			if (xmlDocument.Name == "matrixBuild")
				return true;

			return false;
		}

		static void UploadMatrixBuild(XElement xml)
		{
			foreach (XElement run in xml.Elements("run"))
			{
				IEnumerable<XElement> actions;

				actions = run.Elements("action");
				if (!actions.Any())
					continue;

				// Check if <action _class="hudson.model.CauseAction" /> exists
				actions = actions.Where(action => action.Attribute("_class")?.Value?.Equals("hudson.model.CauseAction") ?? false);
				if (!actions.Any())
					continue;

				// Check if <action><cause _class="hudson.model.Cause$UpstreamCause" /></action> exists
				actions = actions.Where(action => action.Element("cause")?.Attribute("_class")?.Value?.Equals("hudson.model.Cause$UpstreamCause") ?? false);
				if (!actions.Any())
					continue;

				// Check if <action><cause><upstreamBuild/></cause></action> is equal to <id/>
				actions = actions.Where(action => action.Element("cause")?.Element("upstreamBuild")?.Value?.Equals(xml.Element("id").Value) ?? false);
				if (!actions.Any())
					continue;

				UploadBuild(run);
			}
		}

		static void UploadBuild(XElement xml)
		{
			string platformName;
			Build build = new Build (GetJobName(xml, out platformName),
									 platformName,
									 GetBuildId(xml),
			                         GetBuildUrl(xml),
			                         GetBuildResult(xml),
			                         GetBuildDateTime(xml));

			InsertBuild(build);

			foreach (FailedTest failedTest in GetFailedTests(xml))
			{
				InsertFailedTest(build, failedTest);
			}
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
			return xml.Element("result").Value;
		}

		static DateTime GetBuildDateTime(XElement xml)
		{
			return new DateTime(1970, 1, 1).AddMilliseconds(long.Parse(xml.Element("timestamp").Value));
		}

		static IEnumerable<FailedTest> GetFailedTests(XElement xml)
		{
			foreach (XElement action in xml.Elements("action"))
			{
				if (!(action.Attribute("_class")?.Value?.Equals("com.microsoftopentechnologies.windowsazurestorage.AzureBlobAction") ?? false))
					continue;

				string babysitterURL = action.Element("individualBlob")?.Element("blobURL")?.Value;
				if (string.IsNullOrWhiteSpace (babysitterURL))
					continue;

				foreach (string line in webClient.DownloadString(babysitterURL).Split('\n'))
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
						yield return new FailedTest { TestName = test.Value<JProperty>().Name };
					}
				}

				// There should be only 1 babysitter upload per build
				break;
			}
		}

		static void InsertBuild(Build build)
		{
			Console.WriteLine(build);

			using (SqlCommand sqlCommand = new SqlCommand(
				"INSERT INTO Builds (JobName, PlatformName, BuildId, Result, DateTime, URL) " +
					"VALUES (@JobName, @PlatformName, @BuildId, @Result, @DateTime, @URL)", sqlConnection, sqlTransaction))
			{
				sqlCommand.Parameters.Add(new SqlParameter("JobName", build.JobName));
				sqlCommand.Parameters.Add(new SqlParameter("PlatformName", build.PlatformName));
				sqlCommand.Parameters.Add(new SqlParameter("BuildId", build.Id));
				sqlCommand.Parameters.Add(new SqlParameter("Result", build.Result));
				sqlCommand.Parameters.Add(new SqlParameter("DateTime", build.DateTime));
				sqlCommand.Parameters.Add(new SqlParameter("URL", build.Url));

				sqlCommand.ExecuteNonQuery();
			}
		}

		static void InsertFailedTest(Build build, FailedTest failure)
		{
			Console.WriteLine(failure);

			using (SqlCommand sqlCommand = new SqlCommand(
				"INSERT INTO FailedTests (JobName, PlatformName, BuildId, TestName) " +
					"VALUES (@JobName, @PlatformName, @BuildId, @TestName)", sqlConnection, sqlTransaction))
			{
				sqlCommand.Parameters.Add(new SqlParameter("JobName", build.JobName));
				sqlCommand.Parameters.Add(new SqlParameter("PlatformName", build.PlatformName));
				sqlCommand.Parameters.Add(new SqlParameter("BuildId", build.Id));
				sqlCommand.Parameters.Add(new SqlParameter("TestName", failure.TestName));

				sqlCommand.ExecuteNonQuery();
			}
		}
	}
}
