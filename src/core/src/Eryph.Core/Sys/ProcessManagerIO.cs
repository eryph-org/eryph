using System;
using System.Diagnostics;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Eryph.Core.Sys;

public interface ProcessManagerIO
{
    /// <summary>
    /// The image name (without file extension) of the running process with the given
    /// <paramref name="processId"/>, or <c>None</c> if no process with that id is
    /// currently running.
    /// </summary>
    Option<string> GetProcessName(int processId);

    /// <summary>
    /// Terminates the process tree of the process with the given
    /// <paramref name="processId"/> and waits up to <paramref name="timeout"/> for it to
    /// exit. Returns <c>true</c> if the process is gone (it had already exited or was
    /// terminated within the timeout), <c>false</c> if it was still running when the
    /// timeout elapsed.
    /// </summary>
    bool StopProcess(int processId, TimeSpan timeout);
}

public readonly struct LiveProcessManagerIO : ProcessManagerIO
{
    public static readonly ProcessManagerIO Default = new LiveProcessManagerIO();

    public Option<string> GetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            // No process with the given id is running (e.g. a stale pidfile).
            return None;
        }
    }

    public bool StopProcess(int processId, TimeSpan timeout)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
            return process.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            // The process already exited; there is nothing to terminate.
            return true;
        }
        catch (InvalidOperationException)
        {
            // The process exited between the lookup and the kill.
            return true;
        }
    }
}
