using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MicrophoneManager.WinUI.Services;

/// <summary>
/// Provides a dedicated STA thread for COM operations to prevent UI thread blocking.
/// COM IPolicyConfig requires STA apartment state, so this service ensures proper
/// threading while allowing async/await integration.
/// </summary>
public class ComThreadService : IDisposable
{
    private readonly Thread _comThread;
    private readonly BlockingCollection<WorkItem> _workQueue;
    private readonly CancellationTokenSource _shutdownToken;
    private bool _disposed;

    public ComThreadService()
    {
        _workQueue = new BlockingCollection<WorkItem>();
        _shutdownToken = new CancellationTokenSource();

        _comThread = new Thread(ComThreadProc)
        {
            Name = "COM Worker Thread",
            IsBackground = false
        };
        _comThread.SetApartmentState(ApartmentState.STA);
        _comThread.Start();
    }

    /// <summary>
    /// Invokes an action on the COM thread and returns a task that completes when the action finishes.
    /// </summary>
    public Task InvokeAsync(Action action)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ComThreadService));
        }

        var tcs = new TaskCompletionSource<bool>();
        var workItem = new WorkItem(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        _workQueue.Add(workItem);
        return tcs.Task;
    }

    /// <summary>
    /// Invokes a function on the COM thread and returns a task with the result.
    /// </summary>
    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ComThreadService));
        }

        var tcs = new TaskCompletionSource<T>();
        var workItem = new WorkItem(() =>
        {
            try
            {
                var result = func();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        _workQueue.Add(workItem);
        return tcs.Task;
    }

    private void ComThreadProc()
    {
        // Process work items until shutdown is requested
        try
        {
            foreach (var workItem in _workQueue.GetConsumingEnumerable(_shutdownToken.Token))
            {
                workItem.Execute();
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested, exit gracefully
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _shutdownToken.Cancel();
        _workQueue.CompleteAdding();

        // Wait for the thread to finish (max 1 second)
        if (!_comThread.Join(1000))
        {
            System.Diagnostics.Debug.WriteLine("ComThreadService: Thread did not exit within timeout");
        }

        _workQueue.Dispose();
        _shutdownToken.Dispose();
    }

    private class WorkItem
    {
        private readonly Action _action;

        public WorkItem(Action action)
        {
            _action = action;
        }

        public void Execute()
        {
            _action();
        }
    }
}
