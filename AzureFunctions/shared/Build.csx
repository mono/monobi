
#load "FailedTest.csx"

using System;
using System.Collections.Generic;

[DataContract]
class Build
{
    [DataMember] public string JobName { get; }
    [DataMember] public string PlatformName { get; }
    [DataMember] public int Id { get; }
    [DataMember] public string Url { get; }
    [DataMember] public string Result { get; }
    [DataMember] public DateTime DateTime { get; }
    [DataMember] public string BabysitterUrl { get; }
    [DataMember] public string GitHash { get; }
    [DataMember] public int PrId { get; }
    [DataMember] public string PrUrl { get; }
    [DataMember] public string PrTitle { get; }
    [DataMember] public string PrAuthor { get; }

    [DataMember] public List<FailedTest> FailedTests { get; }

    // Needed for serialization, see http://stackoverflow.com/questions/10077121/datacontract-exception-cannot-be-serialized
    public Build() {}

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
