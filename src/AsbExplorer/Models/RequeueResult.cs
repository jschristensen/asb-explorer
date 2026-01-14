namespace AsbExplorer.Models;

public record RequeueResult(bool Success, string? ErrorMessage);

public record BulkRequeueResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<(long SequenceNumber, string Error)> Failures);
