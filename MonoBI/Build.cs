
using System;
using System.Collections.Generic;

namespace MonoBI
{
	class Build
	{
		public string JobName { get; }

		public string PlatformName { get; }

		public int Id { get; }

		public string Url { get; }

		public string Result { get; }

		public DateTime DateTime { get; }

		public Build(string jobName, string platformName, int id, string url, string result, DateTime dateTime)
		{
			JobName = jobName;
			PlatformName = platformName;
			Id = id;
			Url = url;
			Result = result;
			DateTime = dateTime;
		}

		public override string ToString()
		{
			return string.Format($"[Build: JobName={JobName}, PlatformName={PlatformName}, Id={Id}, Url={Url}, Result={Result}, DateTime={DateTime}]");
		}
	}
}
