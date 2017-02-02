using System;
namespace MonoBI
{
	class FailedTest
	{
		public string TestName;

		public override string ToString()
		{
			return string.Format($"[FailedTest: TestName={TestName}]");
		}
	}
}