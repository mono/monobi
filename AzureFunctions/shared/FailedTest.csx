
using System;

class FailedTest
{
    public string TestName { get; }
    public string Invocation { get; }
    public string Failure { get; }
    public int FinalCode { get; }

    public FailedTest(string testName, string invocation, string failure, int finalCode)
    {
        TestName = testName;
        Invocation = invocation;
        Failure = failure;
        FinalCode = finalCode;
    }

    public override string ToString()
    {
        return $"[FailedTest: TestName={TestName}, Invocation={Invocation}, Failure={Failure}, FinalCode={FinalCode}]";
    }
}
