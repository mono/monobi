
using Newtonsoft.Json;

namespace MonoBI
{
	class BuildReference
	{
		[JsonProperty("number", Required = Required.Always)]
		public int Id;
	}
}
