using AsbExplorer.Models;

namespace AsbExplorer.Tests.Models;

public class RequeueResultTests
{
    [Fact]
    public void RequeueResult_Success_HasCorrectProperties()
    {
        var result = new RequeueResult(true, null);
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void RequeueResult_Failure_HasCorrectProperties()
    {
        var result = new RequeueResult(false, "Connection timeout");
        Assert.False(result.Success);
        Assert.Equal("Connection timeout", result.ErrorMessage);
    }

    [Fact]
    public void BulkRequeueResult_AllSuccess_HasCorrectCounts()
    {
        var result = new BulkRequeueResult(5, 0, []);
        Assert.Equal(5, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void BulkRequeueResult_PartialFailure_HasCorrectCounts()
    {
        var failures = new List<(long SequenceNumber, string Error)>
        {
            (123, "Timeout"),
            (456, "Not found")
        };
        var result = new BulkRequeueResult(3, 2, failures);
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(2, result.FailureCount);
        Assert.Equal(2, result.Failures.Count);
    }
}
