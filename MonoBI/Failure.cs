using System;
namespace MonoBI
{
	class Failure
	{
		public string TestName;

		public override string ToString()
		{
			return string.Format("[Failure: TestName={0}]", TestName);
		}
	}
}