
#load "FailedTest.csx"

using System;
using System.Collections.Generic;

class Build
{
	public string JobName { get; }
	public string PlatformName { get; }
	public int Id { get; }
	public string Url { get; }
	public string Result { get; }
	public DateTime DateTime { get; }
    public string BabysitterUrl { get; }
    public string GitHash { get; }
    public int PrId { get; }
    public string PrUrl { get; }
    public string PrTitle { get; }
    public string PrAuthor { get; }

    public List<FailedTest> FailedTests { get; }

    public Build(string jobName, string platformName, int id, string url, string result, DateTime dateTime, string babysitterUrl, string gitHash)
        : this(jobName, platformName, id, url, result, dateTime, babysitterUrl, gitHash, -1, null, null, null)
    {
    }

    public Build(string jobName, string platformName, int id, string url, string result, DateTime dateTime, string babysitterUrl, string gitHash, int prId, string prUrl, string prTitle, string prAuthor)
	{
		JobName = jobName;
		PlatformName = platformName;
		Id = id;
		Url = url;
		Result = result;
		DateTime = dateTime;
        BabysitterUrl = babysitterUrl;
        GitHash = gitHash;
        PrId = prId;
        PrUrl = prUrl;
        PrTitle = prTitle;
        PrAuthor = prAuthor;

        FailedTests = new List<FailedTest>();
	}

	public override string ToString()
	{
		return $"[Build: JobName={JobName}, PlatformName={PlatformName}, Id={Id}, Url={Url}, Result={Result}, DateTime={DateTime}, GitHash={GitHash}, PrId={PrId}, PrUrl={PrUrl}, PrTitle={PrTitle}, PrAuthor={PrAuthor}]";
    }
}
