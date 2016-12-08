
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MonoBI
{
	class Build
	{
		public string JobName;

		public string PlatformName;

		[JsonProperty("id", Required = Required.Always)]
		public int Id;

		[JsonProperty("result")]
		public string Result;

		[JsonProperty("timestamp", Required = Required.Always)]
		[JsonConverter(typeof(MillisecondsTimestampDateTimeConverter))]
		public DateTime DateTime;

		[JsonProperty("url", Required = Required.Always)]
		public string URL;

		[JsonProperty("building", Required = Required.Always)]
		public bool Building;

		public List<Failure> Failures = new List<Failure>();

		public override string ToString()
		{
			return string.Format("[Build: JobName={0}, PlatformName={1}, Id={2}, Result={3}, DateTime={4}, URL={5}]",
								 JobName, PlatformName, Id, Result, DateTime, Uri.EscapeDataString(URL));
		}

		class MillisecondsTimestampDateTimeConverter : JsonConverter
		{
			public override bool CanConvert(Type objectType)
			{
				return objectType == typeof(DateTime);
			}

			public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
			{
				if (reader.TokenType != JsonToken.Integer)
					throw new JsonSerializationException(string.Format("Unexpected token parsing timestamp. Expected Integer, got {0}.", reader.TokenType));

				return new DateTime(1970, 1, 1).AddMilliseconds((long)reader.Value);
			}

			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
			{
				throw new NotImplementedException();
			}
		}
	}
}
