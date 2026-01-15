# DLQ Message Requeue Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enable users to edit and requeue dead-letter queue messages back to their original entities.

**Architecture:** New `MessageRequeueService` handles send/complete operations. `EditMessageDialog` for single-message editing. Multi-select via checkbox column in `MessageListView` (DLQ mode only).

**Tech Stack:** Azure.Messaging.ServiceBus, Terminal.Gui v2, xUnit

---

## Task 1: Entity Path Helper

Extract logic to derive original entity path from DLQ path.

**Files:**
- Create: `src/AsbExplorer/Helpers/EntityPathHelper.cs`
- Create: `src/AsbExplorer.Tests/Helpers/EntityPathHelperTests.cs`

**Step 1: Write the failing tests**

```csharp
// src/AsbExplorer.Tests/Helpers/EntityPathHelperTests.cs
using AsbExplorer.Helpers;

namespace AsbExplorer.Tests.Helpers;

public class EntityPathHelperTests
{
    [Fact]
    public void GetOriginalEntityPath_QueueDlq_ReturnsQueueName()
    {
        var result = EntityPathHelper.GetOriginalEntityPath("orders-queue", isSubscription: false);
        Assert.Equal("orders-queue", result);
    }

    [Fact]
    public void GetOriginalEntityPath_SubscriptionDlq_ReturnsSubscriptionName()
    {
        var result = EntityPathHelper.GetOriginalEntityPath("my-subscription", isSubscription: true);
        Assert.Equal("my-subscription", result);
    }

    [Fact]
    public void GetOriginalEntityPath_NullPath_ReturnsNull()
    {
        var result = EntityPathHelper.GetOriginalEntityPath(null, isSubscription: false);
        Assert.Null(result);
    }

    [Fact]
    public void GetOriginalEntityPath_EmptyPath_ReturnsEmpty()
    {
        var result = EntityPathHelper.GetOriginalEntityPath("", isSubscription: false);
        Assert.Equal("", result);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~EntityPathHelperTests" --verbosity quiet
```

Expected: Build error - `EntityPathHelper` does not exist.

**Step 3: Write minimal implementation**

```csharp
// src/AsbExplorer/Helpers/EntityPathHelper.cs
namespace AsbExplorer.Helpers;

public static class EntityPathHelper
{
    /// <summary>
    /// Returns the original entity path (queue name or subscription name).
    /// For DLQ messages, this is the entity to send requeued messages to.
    /// </summary>
    public static string? GetOriginalEntityPath(string? entityPath, bool isSubscription)
    {
        // Entity path is already the queue/subscription name.
        // The DLQ is accessed via SubQueue option, not path suffix.
        return entityPath;
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~EntityPathHelperTests" --verbosity quiet
```

Expected: All 4 tests pass.

**Step 5: Commit**

```bash
git add src/AsbExplorer/Helpers/EntityPathHelper.cs src/AsbExplorer.Tests/Helpers/EntityPathHelperTests.cs
git commit -m "feat: add EntityPathHelper for DLQ path resolution"
```

---

## Task 2: Result Models

Create result records for requeue operations.

**Files:**
- Create: `src/AsbExplorer/Models/RequeueResult.cs`
- Create: `src/AsbExplorer.Tests/Models/RequeueResultTests.cs`

**Step 1: Write the failing tests**

```csharp
// src/AsbExplorer.Tests/Models/RequeueResultTests.cs
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
```

**Step 2: Run tests to verify they fail**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~RequeueResultTests" --verbosity quiet
```

Expected: Build error - `RequeueResult` does not exist.

**Step 3: Write minimal implementation**

```csharp
// src/AsbExplorer/Models/RequeueResult.cs
namespace AsbExplorer.Models;

public record RequeueResult(bool Success, string? ErrorMessage);

public record BulkRequeueResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<(long SequenceNumber, string Error)> Failures);
```

**Step 4: Run tests to verify they pass**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~RequeueResultTests" --verbosity quiet
```

Expected: All 4 tests pass.

**Step 5: Commit**

```bash
git add src/AsbExplorer/Models/RequeueResult.cs src/AsbExplorer.Tests/Models/RequeueResultTests.cs
git commit -m "feat: add RequeueResult and BulkRequeueResult models"
```

---

## Task 3: MessageRequeueService Interface

Define the service interface for requeue operations.

**Files:**
- Create: `src/AsbExplorer/Services/IMessageRequeueService.cs`

**Step 1: Create interface**

```csharp
// src/AsbExplorer/Services/IMessageRequeueService.cs
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public interface IMessageRequeueService
{
    /// <summary>
    /// Send a message to a queue.
    /// </summary>
    Task<RequeueResult> SendToQueueAsync(
        string connectionName,
        string queueName,
        PeekedMessage originalMessage,
        BinaryData? modifiedBody = null);

    /// <summary>
    /// Send a message to a topic subscription's originating topic.
    /// </summary>
    Task<RequeueResult> SendToTopicAsync(
        string connectionName,
        string topicName,
        PeekedMessage originalMessage,
        BinaryData? modifiedBody = null);

    /// <summary>
    /// Complete (remove) a message from a queue's dead-letter queue.
    /// </summary>
    Task<RequeueResult> CompleteFromQueueDlqAsync(
        string connectionName,
        string queueName,
        long sequenceNumber);

    /// <summary>
    /// Complete (remove) a message from a subscription's dead-letter queue.
    /// </summary>
    Task<RequeueResult> CompleteFromSubscriptionDlqAsync(
        string connectionName,
        string topicName,
        string subscriptionName,
        long sequenceNumber);

    /// <summary>
    /// Requeue multiple messages from DLQ to original entity.
    /// </summary>
    Task<BulkRequeueResult> RequeueMessagesAsync(
        string connectionName,
        string entityPath,
        string? topicName,
        IReadOnlyList<PeekedMessage> messages,
        bool removeOriginals);
}
```

**Step 2: Commit**

```bash
git add src/AsbExplorer/Services/IMessageRequeueService.cs
git commit -m "feat: add IMessageRequeueService interface"
```

---

## Task 4: MessageRequeueService Implementation

Implement the service using Azure Service Bus SDK.

**Files:**
- Create: `src/AsbExplorer/Services/MessageRequeueService.cs`

**Step 1: Write the implementation**

```csharp
// src/AsbExplorer/Services/MessageRequeueService.cs
using Azure.Messaging.ServiceBus;
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class MessageRequeueService : IMessageRequeueService, IAsyncDisposable
{
    private readonly ConnectionStore _connectionStore;
    private ServiceBusClient? _client;
    private string? _currentConnectionName;

    public MessageRequeueService(ConnectionStore connectionStore)
    {
        _connectionStore = connectionStore;
    }

    public async Task<RequeueResult> SendToQueueAsync(
        string connectionName,
        string queueName,
        PeekedMessage originalMessage,
        BinaryData? modifiedBody = null)
    {
        try
        {
            var client = GetOrCreateClient(connectionName);
            if (client is null)
            {
                return new RequeueResult(false, "Connection not found");
            }

            await using var sender = client.CreateSender(queueName);
            var message = CreateServiceBusMessage(originalMessage, modifiedBody);
            await sender.SendMessageAsync(message);

            return new RequeueResult(true, null);
        }
        catch (Exception ex)
        {
            return new RequeueResult(false, ex.Message);
        }
    }

    public async Task<RequeueResult> SendToTopicAsync(
        string connectionName,
        string topicName,
        PeekedMessage originalMessage,
        BinaryData? modifiedBody = null)
    {
        try
        {
            var client = GetOrCreateClient(connectionName);
            if (client is null)
            {
                return new RequeueResult(false, "Connection not found");
            }

            await using var sender = client.CreateSender(topicName);
            var message = CreateServiceBusMessage(originalMessage, modifiedBody);
            await sender.SendMessageAsync(message);

            return new RequeueResult(true, null);
        }
        catch (Exception ex)
        {
            return new RequeueResult(false, ex.Message);
        }
    }

    public async Task<RequeueResult> CompleteFromQueueDlqAsync(
        string connectionName,
        string queueName,
        long sequenceNumber)
    {
        try
        {
            var client = GetOrCreateClient(connectionName);
            if (client is null)
            {
                return new RequeueResult(false, "Connection not found");
            }

            var options = new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            };

            await using var receiver = client.CreateReceiver(queueName, options);
            var message = await receiver.ReceiveDeferredMessageAsync(sequenceNumber);

            if (message is null)
            {
                // Message not deferred, try regular receive with filter
                return await CompleteBySequenceNumberAsync(receiver, sequenceNumber);
            }

            await receiver.CompleteMessageAsync(message);
            return new RequeueResult(true, null);
        }
        catch (Exception ex)
        {
            return new RequeueResult(false, ex.Message);
        }
    }

    public async Task<RequeueResult> CompleteFromSubscriptionDlqAsync(
        string connectionName,
        string topicName,
        string subscriptionName,
        long sequenceNumber)
    {
        try
        {
            var client = GetOrCreateClient(connectionName);
            if (client is null)
            {
                return new RequeueResult(false, "Connection not found");
            }

            var options = new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            };

            await using var receiver = client.CreateReceiver(topicName, subscriptionName, options);
            return await CompleteBySequenceNumberAsync(receiver, sequenceNumber);
        }
        catch (Exception ex)
        {
            return new RequeueResult(false, ex.Message);
        }
    }

    public async Task<BulkRequeueResult> RequeueMessagesAsync(
        string connectionName,
        string entityPath,
        string? topicName,
        IReadOnlyList<PeekedMessage> messages,
        bool removeOriginals)
    {
        var successCount = 0;
        var failures = new List<(long SequenceNumber, string Error)>();

        foreach (var message in messages)
        {
            // Send to original entity
            RequeueResult sendResult;
            if (topicName is not null)
            {
                sendResult = await SendToTopicAsync(connectionName, topicName, message);
            }
            else
            {
                sendResult = await SendToQueueAsync(connectionName, entityPath, message);
            }

            if (!sendResult.Success)
            {
                failures.Add((message.SequenceNumber, sendResult.ErrorMessage ?? "Unknown error"));
                continue;
            }

            // Complete original if requested
            if (removeOriginals)
            {
                RequeueResult completeResult;
                if (topicName is not null)
                {
                    completeResult = await CompleteFromSubscriptionDlqAsync(
                        connectionName, topicName, entityPath, message.SequenceNumber);
                }
                else
                {
                    completeResult = await CompleteFromQueueDlqAsync(
                        connectionName, entityPath, message.SequenceNumber);
                }

                if (!completeResult.Success)
                {
                    // Message was sent but not removed - partial success
                    // Still count as success since message was requeued
                    successCount++;
                    continue;
                }
            }

            successCount++;
        }

        return new BulkRequeueResult(successCount, failures.Count, failures);
    }

    private static ServiceBusMessage CreateServiceBusMessage(PeekedMessage original, BinaryData? modifiedBody)
    {
        var message = new ServiceBusMessage(modifiedBody ?? original.Body)
        {
            ContentType = original.ContentType,
            Subject = original.Subject,
            CorrelationId = original.CorrelationId,
            SessionId = original.SessionId,
            TimeToLive = original.TimeToLive
        };

        foreach (var prop in original.ApplicationProperties)
        {
            message.ApplicationProperties[prop.Key] = prop.Value;
        }

        return message;
    }

    private async Task<RequeueResult> CompleteBySequenceNumberAsync(
        ServiceBusReceiver receiver,
        long sequenceNumber)
    {
        // DLQ messages are not deferred, so we need to receive and find by sequence number
        // Receive in batches and look for our message
        var maxAttempts = 10;
        var batchSize = 50;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var messages = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(5));

            foreach (var msg in messages)
            {
                if (msg.SequenceNumber == sequenceNumber)
                {
                    await receiver.CompleteMessageAsync(msg);
                    return new RequeueResult(true, null);
                }
                else
                {
                    // Not our message, abandon it so others can process
                    await receiver.AbandonMessageAsync(msg);
                }
            }

            if (messages.Count < batchSize)
            {
                // No more messages
                break;
            }
        }

        return new RequeueResult(false, $"Message with sequence number {sequenceNumber} not found in DLQ");
    }

    private ServiceBusClient? GetOrCreateClient(string connectionName)
    {
        if (_client is null || _currentConnectionName != connectionName)
        {
            _client?.DisposeAsync().AsTask().GetAwaiter().GetResult();

            var connection = _connectionStore.GetByName(connectionName);
            if (connection is null)
            {
                return null;
            }

            _client = new ServiceBusClient(connection.ConnectionString);
            _currentConnectionName = connectionName;
        }

        return _client;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }
    }
}
```

**Step 2: Run build to verify compilation**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet build src/AsbExplorer/AsbExplorer.csproj --verbosity quiet
```

Expected: Build succeeds.

**Step 3: Commit**

```bash
git add src/AsbExplorer/Services/MessageRequeueService.cs
git commit -m "feat: implement MessageRequeueService"
```

---

## Task 5: Register Service in DI

Add the new service to dependency injection.

**Files:**
- Modify: `src/AsbExplorer/Program.cs:14`

**Step 1: Add service registration**

Add this line after `services.AddSingleton<MessagePeekService>();` (line 14):

```csharp
services.AddSingleton<IMessageRequeueService, MessageRequeueService>();
```

**Step 2: Run build to verify**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet build src/AsbExplorer/AsbExplorer.csproj --verbosity quiet
```

Expected: Build succeeds.

**Step 3: Commit**

```bash
git add src/AsbExplorer/Program.cs
git commit -m "feat: register MessageRequeueService in DI"
```

---

## Task 6: EditMessageDialog

Create the dialog for editing and requeuing a single message.

**Files:**
- Create: `src/AsbExplorer/Views/EditMessageDialog.cs`

**Step 1: Write the dialog**

```csharp
// src/AsbExplorer/Views/EditMessageDialog.cs
using Terminal.Gui;
using AsbExplorer.Models;

namespace AsbExplorer.Views;

public class EditMessageDialog : Dialog
{
    private readonly TextView _bodyEditor;
    private readonly PeekedMessage _originalMessage;

    public bool Confirmed { get; private set; }
    public bool RemoveOriginal { get; private set; }
    public string EditedBody => _bodyEditor.Text;

    public EditMessageDialog(PeekedMessage message, string originalEntityName)
    {
        _originalMessage = message;

        Title = "Edit Message";
        Width = Dim.Percent(80);
        Height = Dim.Percent(80);

        // Header info
        var entityLabel = new Label
        {
            Text = $"Original Entity: {originalEntityName}",
            X = 1,
            Y = 1
        };

        var seqLabel = new Label
        {
            Text = $"Sequence Number: {message.SequenceNumber}",
            X = 1,
            Y = 2
        };

        var msgIdLabel = new Label
        {
            Text = $"Message ID: {message.MessageId}",
            X = 1,
            Y = 3
        };

        var bodyLabel = new Label
        {
            Text = "Body:",
            X = 1,
            Y = 5
        };

        // Body editor
        _bodyEditor = new TextView
        {
            X = 1,
            Y = 6,
            Width = Dim.Fill(1),
            Height = Dim.Fill(4),
            Text = GetBodyAsString(message.Body),
            ReadOnly = false
        };

        // Buttons
        var duplicateButton = new Button
        {
            Text = "Duplicate",
            X = Pos.Center() - 20,
            Y = Pos.AnchorEnd(2)
        };

        duplicateButton.Accepting += (s, e) =>
        {
            Confirmed = true;
            RemoveOriginal = false;
            Application.RequestStop();
        };

        var moveButton = new Button
        {
            Text = "Move",
            X = Pos.Center() - 5,
            Y = Pos.AnchorEnd(2)
        };

        moveButton.Accepting += (s, e) =>
        {
            Confirmed = true;
            RemoveOriginal = true;
            Application.RequestStop();
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            X = Pos.Center() + 8,
            Y = Pos.AnchorEnd(2)
        };

        cancelButton.Accepting += (s, e) =>
        {
            Confirmed = false;
            Application.RequestStop();
        };

        Add(entityLabel, seqLabel, msgIdLabel, bodyLabel, _bodyEditor,
            duplicateButton, moveButton, cancelButton);

        _bodyEditor.SetFocus();
    }

    private static string GetBodyAsString(BinaryData body)
    {
        try
        {
            return body.ToString();
        }
        catch
        {
            return Convert.ToBase64String(body.ToArray());
        }
    }
}
```

**Step 2: Run build to verify**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet build src/AsbExplorer/AsbExplorer.csproj --verbosity quiet
```

Expected: Build succeeds.

**Step 3: Commit**

```bash
git add src/AsbExplorer/Views/EditMessageDialog.cs
git commit -m "feat: add EditMessageDialog for single message editing"
```

---

## Task 7: RequeueConfirmDialog

Create the confirmation dialog for bulk requeue.

**Files:**
- Create: `src/AsbExplorer/Views/RequeueConfirmDialog.cs`

**Step 1: Write the dialog**

```csharp
// src/AsbExplorer/Views/RequeueConfirmDialog.cs
using Terminal.Gui;

namespace AsbExplorer.Views;

public class RequeueConfirmDialog : Dialog
{
    private readonly CheckBox _removeOriginalsCheckbox;

    public bool Confirmed { get; private set; }
    public bool RemoveOriginals => _removeOriginalsCheckbox.CheckedState == CheckState.Checked;

    public RequeueConfirmDialog(int messageCount)
    {
        Title = "Requeue Messages";
        Width = 60;
        Height = 10;

        var questionLabel = new Label
        {
            Text = $"Requeue {messageCount} message{(messageCount == 1 ? "" : "s")} to their original entities?",
            X = Pos.Center(),
            Y = 2,
            TextAlignment = Alignment.Center
        };

        _removeOriginalsCheckbox = new CheckBox
        {
            Text = "Remove originals from dead-letter queue",
            X = Pos.Center(),
            Y = 4,
            CheckedState = CheckState.UnChecked
        };

        var requeueButton = new Button
        {
            Text = "Requeue",
            X = Pos.Center() - 12,
            Y = 6,
            IsDefault = true
        };

        requeueButton.Accepting += (s, e) =>
        {
            Confirmed = true;
            Application.RequestStop();
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            X = Pos.Center() + 3,
            Y = 6
        };

        cancelButton.Accepting += (s, e) =>
        {
            Confirmed = false;
            Application.RequestStop();
        };

        Add(questionLabel, _removeOriginalsCheckbox, requeueButton, cancelButton);
    }
}
```

**Step 2: Run build to verify**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet build src/AsbExplorer/AsbExplorer.csproj --verbosity quiet
```

Expected: Build succeeds.

**Step 3: Commit**

```bash
git add src/AsbExplorer/Views/RequeueConfirmDialog.cs
git commit -m "feat: add RequeueConfirmDialog for bulk confirmation"
```

---

## Task 8: RequeueResultDialog

Create the results dialog showing operation summary.

**Files:**
- Create: `src/AsbExplorer/Views/RequeueResultDialog.cs`

**Step 1: Write the dialog**

```csharp
// src/AsbExplorer/Views/RequeueResultDialog.cs
using Terminal.Gui;
using AsbExplorer.Models;
using System.Text;

namespace AsbExplorer.Views;

public class RequeueResultDialog : Dialog
{
    public RequeueResultDialog(BulkRequeueResult result)
    {
        Title = "Requeue Complete";
        Width = 60;
        Height = Math.Min(15 + result.Failures.Count, 25);

        var successLabel = new Label
        {
            Text = $"Successfully requeued: {result.SuccessCount}",
            X = 2,
            Y = 2
        };

        var failedLabel = new Label
        {
            Text = $"Failed: {result.FailureCount}",
            X = 2,
            Y = 3
        };

        Add(successLabel, failedLabel);

        if (result.Failures.Count > 0)
        {
            var failuresLabel = new Label
            {
                Text = "Failures:",
                X = 2,
                Y = 5
            };

            var sb = new StringBuilder();
            foreach (var (seq, error) in result.Failures)
            {
                sb.AppendLine($"• Seq {seq}: {error}");
            }

            var failuresText = new TextView
            {
                X = 2,
                Y = 6,
                Width = Dim.Fill(2),
                Height = Dim.Fill(3),
                Text = sb.ToString(),
                ReadOnly = true
            };

            Add(failuresLabel, failuresText);
        }

        var okButton = new Button
        {
            Text = "OK",
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
            IsDefault = true
        };

        okButton.Accepting += (s, e) => Application.RequestStop();

        Add(okButton);
    }
}
```

**Step 2: Run build to verify**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet build src/AsbExplorer/AsbExplorer.csproj --verbosity quiet
```

Expected: Build succeeds.

**Step 3: Commit**

```bash
git add src/AsbExplorer/Views/RequeueResultDialog.cs
git commit -m "feat: add RequeueResultDialog for operation results"
```

---

## Task 9: MessageListView Multi-Select Support

Add checkbox column and selection tracking to MessageListView.

**Files:**
- Modify: `src/AsbExplorer/Views/MessageListView.cs`

**Step 1: Add fields and properties**

Add these fields after line 12 (`private IReadOnlyList<PeekedMessage> _messages = [];`):

```csharp
    private readonly HashSet<long> _selectedSequenceNumbers = [];
    private readonly Button _requeueButton;
    private bool _isDeadLetterMode;
```

**Step 2: Add public property for DLQ mode**

Add after the AutoRefreshToggled event (around line 15):

```csharp
    public event Action<PeekedMessage>? EditMessageRequested;
    public event Action? RequeueSelectedRequested;

    public bool IsDeadLetterMode
    {
        get => _isDeadLetterMode;
        set
        {
            _isDeadLetterMode = value;
            UpdateRequeueButtonVisibility();
            RebuildTable();
        }
    }
```

**Step 3: Add requeue button in constructor**

Add after the `_autoRefreshCheckbox` creation (around line 35):

```csharp
        _requeueButton = new Button
        {
            Text = "Requeue Selected",
            X = 0,
            Y = 0,
            Visible = false
        };

        _requeueButton.Accepting += (s, e) => RequeueSelectedRequested?.Invoke();
```

And add it to the view (update the Add call):

```csharp
        Add(_autoRefreshCheckbox, _requeueButton, _tableView);
```

**Step 4: Add key handling for Space, Ctrl+A, Ctrl+D, Enter**

Override OnKeyDown method:

```csharp
    protected override bool OnKeyDown(Key key)
    {
        if (!_isDeadLetterMode)
        {
            return base.OnKeyDown(key);
        }

        // Enter - edit single message
        if (key.KeyCode == KeyCode.Enter && _tableView.SelectedRow >= 0 && _tableView.SelectedRow < _messages.Count)
        {
            EditMessageRequested?.Invoke(_messages[_tableView.SelectedRow]);
            return true;
        }

        // Space - toggle selection
        if (key.KeyCode == KeyCode.Space && _tableView.SelectedRow >= 0 && _tableView.SelectedRow < _messages.Count)
        {
            var seq = _messages[_tableView.SelectedRow].SequenceNumber;
            if (_selectedSequenceNumbers.Contains(seq))
            {
                _selectedSequenceNumbers.Remove(seq);
            }
            else
            {
                _selectedSequenceNumbers.Add(seq);
            }
            UpdateSelectionDisplay();
            return true;
        }

        // Ctrl+A - select all
        if (key.IsCtrl && key.KeyCode == KeyCode.A)
        {
            foreach (var msg in _messages)
            {
                _selectedSequenceNumbers.Add(msg.SequenceNumber);
            }
            UpdateSelectionDisplay();
            return true;
        }

        // Ctrl+D - deselect all
        if (key.IsCtrl && key.KeyCode == KeyCode.D)
        {
            _selectedSequenceNumbers.Clear();
            UpdateSelectionDisplay();
            return true;
        }

        return base.OnKeyDown(key);
    }
```

**Step 5: Add helper methods**

```csharp
    public IReadOnlyList<PeekedMessage> GetSelectedMessages()
    {
        return _messages.Where(m => _selectedSequenceNumbers.Contains(m.SequenceNumber)).ToList();
    }

    public void ClearSelection()
    {
        _selectedSequenceNumbers.Clear();
        UpdateSelectionDisplay();
    }

    private void UpdateRequeueButtonVisibility()
    {
        _requeueButton.Visible = _isDeadLetterMode && _selectedSequenceNumbers.Count > 0;
        if (_requeueButton.Visible)
        {
            _requeueButton.Text = $"Requeue {_selectedSequenceNumbers.Count} Selected";
        }
    }

    private void UpdateSelectionDisplay()
    {
        UpdateRequeueButtonVisibility();
        RebuildTable();
    }

    private void RebuildTable()
    {
        SetMessages(_messages);
    }
```

**Step 6: Update SetMessages to include checkbox column**

Replace the `SetMessages` method:

```csharp
    public void SetMessages(IReadOnlyList<PeekedMessage> messages)
    {
        _messages = messages;
        _dataTable.Rows.Clear();
        _dataTable.Columns.Clear();

        if (_isDeadLetterMode)
        {
            _dataTable.Columns.Add("☐", typeof(string));
        }
        _dataTable.Columns.Add("#", typeof(long));
        _dataTable.Columns.Add("MessageId", typeof(string));
        _dataTable.Columns.Add("Enqueued", typeof(string));
        _dataTable.Columns.Add("Subject", typeof(string));
        _dataTable.Columns.Add("Size", typeof(string));
        _dataTable.Columns.Add("Delivery", typeof(int));
        _dataTable.Columns.Add("ContentType", typeof(string));

        foreach (var msg in messages)
        {
            var row = new List<object>();

            if (_isDeadLetterMode)
            {
                row.Add(_selectedSequenceNumbers.Contains(msg.SequenceNumber) ? "☑" : "☐");
            }

            row.Add(msg.SequenceNumber);
            row.Add(DisplayHelpers.TruncateId(msg.MessageId, 12));
            row.Add(DisplayHelpers.FormatRelativeTime(msg.EnqueuedTime));
            row.Add(msg.Subject ?? "-");
            row.Add(DisplayHelpers.FormatSize(msg.BodySizeBytes));
            row.Add(msg.DeliveryCount);
            row.Add(msg.ContentType ?? "-");

            _dataTable.Rows.Add(row.ToArray());
        }

        // Set column widths
        _tableView.Style.ColumnStyles.Clear();
        var colIndex = 0;

        if (_isDeadLetterMode)
        {
            _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 2, MaxWidth = 2 }); // Checkbox
        }
        _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 3, MaxWidth = 12 });     // #
        _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 12, MaxWidth = 14 });   // MessageId
        _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 10, MaxWidth = 12 });   // Enqueued
        _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 10, MaxWidth = 30 });   // Subject
        _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 6, MaxWidth = 8 });     // Size
        _tableView.Style.ColumnStyles.Add(colIndex++, new ColumnStyle { MinWidth = 3, MaxWidth = 8 });     // Delivery
        _tableView.Style.ExpandLastColumn = true;  // ContentType expands

        _tableView.Table = new DataTableSource(_dataTable);
    }
```

**Step 7: Update Clear method**

```csharp
    public void Clear()
    {
        _messages = [];
        _selectedSequenceNumbers.Clear();
        _dataTable.Rows.Clear();
        _tableView.Table = new DataTableSource(_dataTable);
        UpdateRequeueButtonVisibility();
    }
```

**Step 8: Run build to verify**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet build src/AsbExplorer/AsbExplorer.csproj --verbosity quiet
```

Expected: Build succeeds.

**Step 9: Run all tests**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --verbosity quiet
```

Expected: All tests pass.

**Step 10: Commit**

```bash
git add src/AsbExplorer/Views/MessageListView.cs
git commit -m "feat: add multi-select support to MessageListView for DLQ"
```

---

## Task 10: MainWindow Integration

Wire up the requeue functionality in MainWindow.

**Files:**
- Modify: `src/AsbExplorer/Views/MainWindow.cs`

**Step 1: Add service field**

Add after line 15 (`private readonly MessagePeekService _peekService;`):

```csharp
    private readonly IMessageRequeueService _requeueService;
```

**Step 2: Update constructor**

Add `IMessageRequeueService requeueService` parameter and assign it:

```csharp
    public MainWindow(
        ServiceBusConnectionService connectionService,
        ConnectionStore connectionStore,
        MessagePeekService peekService,
        IMessageRequeueService requeueService,  // Add this
        FavoritesStore favoritesStore,
        SettingsStore settingsStore,
        MessageFormatter formatter)
    {
        // ... existing code ...
        _peekService = peekService;
        _requeueService = requeueService;  // Add this
        // ... rest of constructor ...
```

**Step 3: Wire up events**

Add after line 123 (`_messageList.MessageSelected += OnMessageSelected;`):

```csharp
        _messageList.EditMessageRequested += OnEditMessageRequested;
        _messageList.RequeueSelectedRequested += OnRequeueSelectedRequested;
```

**Step 4: Update OnNodeSelected to set DLQ mode**

In the `OnNodeSelected` method, after setting `isDeadLetter` (around line 296), add:

```csharp
            _messageList.IsDeadLetterMode = isDeadLetter;
```

**Step 5: Add edit message handler**

```csharp
    private async void OnEditMessageRequested(PeekedMessage message)
    {
        if (_currentNode is null || _currentNode.ConnectionName is null)
        {
            return;
        }

        var isSubscription = _currentNode.NodeType == TreeNodeType.TopicSubscriptionDeadLetter;
        var entityName = isSubscription ? _currentNode.ParentEntityPath : _currentNode.EntityPath;

        _isModalOpen = true;
        var dialog = new EditMessageDialog(message, entityName ?? "unknown");
        Application.Run(dialog);
        _isModalOpen = false;

        if (!dialog.Confirmed)
        {
            return;
        }

        try
        {
            var modifiedBody = new BinaryData(dialog.EditedBody);

            // Send to original entity
            RequeueResult sendResult;
            if (isSubscription && _currentNode.ParentEntityPath is not null)
            {
                sendResult = await _requeueService.SendToTopicAsync(
                    _currentNode.ConnectionName,
                    _currentNode.ParentEntityPath,
                    message,
                    modifiedBody);
            }
            else if (_currentNode.EntityPath is not null)
            {
                sendResult = await _requeueService.SendToQueueAsync(
                    _currentNode.ConnectionName,
                    _currentNode.EntityPath,
                    message,
                    modifiedBody);
            }
            else
            {
                MessageBox.ErrorQuery("Error", "Could not determine destination entity", "OK");
                return;
            }

            if (!sendResult.Success)
            {
                MessageBox.ErrorQuery("Error", $"Failed to send message: {sendResult.ErrorMessage}", "OK");
                return;
            }

            // Complete original if Move was selected
            if (dialog.RemoveOriginal)
            {
                RequeueResult completeResult;
                if (isSubscription && _currentNode.ParentEntityPath is not null)
                {
                    completeResult = await _requeueService.CompleteFromSubscriptionDlqAsync(
                        _currentNode.ConnectionName,
                        _currentNode.ParentEntityPath,
                        _currentNode.EntityPath!,
                        message.SequenceNumber);
                }
                else
                {
                    completeResult = await _requeueService.CompleteFromQueueDlqAsync(
                        _currentNode.ConnectionName,
                        _currentNode.EntityPath!,
                        message.SequenceNumber);
                }

                if (!completeResult.Success)
                {
                    MessageBox.Query("Warning",
                        $"Message was sent but could not be removed from DLQ: {completeResult.ErrorMessage}",
                        "OK");
                }
            }

            // Refresh message list
            RefreshCurrentNode();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to requeue message: {ex.Message}", "OK");
        }
    }
```

**Step 6: Add bulk requeue handler**

```csharp
    private async void OnRequeueSelectedRequested()
    {
        if (_currentNode is null || _currentNode.ConnectionName is null)
        {
            return;
        }

        var selectedMessages = _messageList.GetSelectedMessages();
        if (selectedMessages.Count == 0)
        {
            return;
        }

        _isModalOpen = true;
        var confirmDialog = new RequeueConfirmDialog(selectedMessages.Count);
        Application.Run(confirmDialog);
        _isModalOpen = false;

        if (!confirmDialog.Confirmed)
        {
            return;
        }

        try
        {
            var isSubscription = _currentNode.NodeType == TreeNodeType.TopicSubscriptionDeadLetter;
            var topicName = isSubscription ? _currentNode.ParentEntityPath : null;

            var result = await _requeueService.RequeueMessagesAsync(
                _currentNode.ConnectionName,
                _currentNode.EntityPath!,
                topicName,
                selectedMessages,
                confirmDialog.RemoveOriginals);

            _isModalOpen = true;
            var resultDialog = new RequeueResultDialog(result);
            Application.Run(resultDialog);
            _isModalOpen = false;

            // Clear selection and refresh
            _messageList.ClearSelection();
            RefreshCurrentNode();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to requeue messages: {ex.Message}", "OK");
        }
    }
```

**Step 7: Run build to verify**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet build src/AsbExplorer/AsbExplorer.csproj --verbosity quiet
```

Expected: Build succeeds.

**Step 8: Run all tests**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --verbosity quiet
```

Expected: All tests pass.

**Step 9: Commit**

```bash
git add src/AsbExplorer/Views/MainWindow.cs
git commit -m "feat: integrate requeue functionality in MainWindow"
```

---

## Task 11: Final Integration Test

Run the application manually to verify the feature works end-to-end.

**Step 1: Run the application**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet run --project src/AsbExplorer/AsbExplorer.csproj
```

**Step 2: Manual test checklist**

1. Navigate to a queue's dead-letter queue
2. Verify checkbox column appears
3. Press Space to toggle selection on a message
4. Press Ctrl+A to select all
5. Press Ctrl+D to deselect all
6. Verify "Requeue X Selected" button appears when messages selected
7. Press Enter on a message to open edit dialog
8. Verify Duplicate/Move/Cancel buttons work
9. Click "Requeue Selected" button
10. Verify confirmation dialog with "Remove originals" checkbox
11. Verify results dialog shows success/failure counts

**Step 3: All tests pass**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/dlq-requeue
dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --verbosity quiet
```

Expected: All tests pass.

**Step 4: Commit final state**

```bash
git add -A
git commit -m "feat: DLQ message requeue feature complete"
```

---

## Summary

This plan implements:

1. **EntityPathHelper** - Utility for path resolution
2. **RequeueResult/BulkRequeueResult** - Result models
3. **IMessageRequeueService** - Service interface
4. **MessageRequeueService** - Azure SDK implementation
5. **EditMessageDialog** - Single message editor
6. **RequeueConfirmDialog** - Bulk operation confirmation
7. **RequeueResultDialog** - Operation results
8. **MessageListView changes** - Checkbox column, selection, keyboard shortcuts
9. **MainWindow integration** - Event wiring and handlers

Total: 11 tasks, ~30 steps following TDD (Red-Green-Refactor-Commit).
