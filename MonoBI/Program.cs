using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace MonoBI
{
	class MainClass
	{
		static readonly List<string> JenkinsJobs = new List<string>()
		{
			{ "test-mono-mainline-community" },
			{ "test-mono-mainline-linux" },
			{ "test-mono-mainline-aot" },
			{ "test-mono-mainline-aot+llvm" },
			{ "test-mono-mainline-bitcode" },
			{ "test-mono-mainline-bitcode-valgrind" },
			{ "test-mono-mainline-checked" },
			{ "test-mono-mainline-coop" },
			{ "test-mono-mainline-docker" },
			{ "test-mono-mainline-fullaot" },
			{ "test-mono-mainline-fullaot+llvm" },
			{ "test-mono-mainline-hybridaot" },
			{ "test-mono-mainline-hybridaot+llvm" },
			{ "test-mono-mainline-mcs" },
			{ "test-mono-mainline-profilerstresstests" },
			{ "test-mono-mainline-staticanalysis" },
			{ "test-mono-mainline-acceptancetests" },
			{ "test-mono-mainline" },
			{ "z" },
		};

		static readonly List<string> JenkinsPlatforms = new List<string>()
		{
			{ "osx-amd64" },
			{ "osx-i386" },
			{ "ubuntu-1404-amd64" },
			{ "ubuntu-1404-i386" },
			{ "debian-8-arm64" },
			{ "debian-8-armhf" },
			{ "debian-8-armel" },
			{ "w32" },
			{ "w64" },
		};

		const string JenkinsBaseURL = "https://jenkins.mono-project.com/job";

		static string JenkinsJobAPIURL(string jobName, string platformName)
		{
			return string.Format ("{0}/{1}/label={2}/api/json", JenkinsBaseURL, jobName, platformName);
		}

		static string JenkinsBuildAPIURL(string jobName, string platformName, int buildId)
		{
			return string.Format("{0}/{1}/label={2}/{3}/api/json?pretty=1&tree=actions[individualBlobs[*],parameters[*],lastBuiltRevision[*],remoteUrls[*]],timestamp,building,result,id,url", JenkinsBaseURL, jobName, platformName, buildId);
		}

		static string JenkinsBuildUIURL(string jobName, string platformName, int buildId)
		{
			return string.Format("{0}/{1}/label={2}/{3}", JenkinsBaseURL, jobName, platformName, buildId);
		}

		public static void Main(string[] args)
		{
			List<Build> builds = new List<Build>();

			Console.WriteLine("JobName,PlatformName,Id,Result,DateTime,URL");
			foreach (Build build in GetBuilds())
			{
				builds.Add(build);

				Console.WriteLine(string.Format("\"{0}\",\"{1}\",{2},\"{3}\",\"{4}\",\"{5}\"",
					build.JobName, build.PlatformName, build.Id, build.Result, build.DateTime, Uri.EscapeDataString(build.URL)));
			}

			Console.WriteLine();

			Console.WriteLine("JobName,PlatformName,BuildId,TestName");
			foreach (Build build in builds)
			{
				foreach (Failure failure in build.Failures)
				{
					Console.WriteLine(string.Format("\"{0}\",\"{1}\",{2},\"{3}\"",
                        build.JobName, build.PlatformName, build.Id, failure.TestName));
				}
			}
		}

		static IEnumerable<Build> GetBuilds()
		{
			using (WebClient client = new WebClient())
			{
				foreach (string jobName in JenkinsJobs)
				{
					foreach (string platformName in JenkinsPlatforms)
					{
						Job job;

						try
						{
							job = JsonConvert.DeserializeObject<Job>(
								client.DownloadString(JenkinsJobAPIURL(jobName, platformName)));
						}
						catch (WebException e)
						{
							if (((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.NotFound)
								continue;

							throw;
						}

						foreach (BuildReference buildReference in job.Builds)
						{
							JToken jtoken = JToken.Parse(
								client.DownloadString(JenkinsBuildAPIURL(jobName, platformName, buildReference.Id)));

							Build build = jtoken.ToObject<Build>();
							if (build.Building)
								continue;

							build.JobName = jobName;
							build.PlatformName = platformName;

							JToken actions = jtoken ["actions"];
							if (actions != null)
							{
								foreach (JToken action in actions.Children ())
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

									string blobResponse = client.DownloadString(blobURL);

									foreach (string line in blobResponse.Split('\n'))
									{
										if (string.IsNullOrWhiteSpace(line))
											continue;

										JToken jstep = JToken.Parse(line);
										if (jstep == null)
											continue;

										JToken tests = jstep["tests"];
										if (tests == null)
											continue;

										foreach (JToken test in tests.Children ())
										{
											build.Failures.Add(new Failure { TestName = test.Value<JProperty>().Name });
										}
									}
								}
							}

							yield return build;
						}
					}
				}
			}
		}
	}
}
