
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MonoBI
{
	class Job
	{
		[JsonProperty("builds", Required = Required.Always)]
		public List<BuildReference> Builds;
	}
}
