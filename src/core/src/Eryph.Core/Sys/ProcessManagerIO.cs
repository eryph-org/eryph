using System;
using System.ComponentModel;
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
        // ArgumentException: no process with the given id is running (e.g. a stale
        // pidfile). InvalidOperationException: the process exited while we inspected it.
        // Win32Exception: the process cannot be inspected (e.g. access denied). In every
        // case we cannot confirm the id belongs to a daemon, so report it as not present
        // rather than letting the exception fail the upgrade.
        catch (Exception ex) when (
            ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return None;
        }
    }

    public bool StopProcess(int processId, TimeSpan timeout)
    {
        // Process.WaitForExit takes an int; clamp so a large timeout cannot overflow.
        var timeoutMs = (int)Math.Min(Math.Max(timeout.TotalMilliseconds, 0), int.MaxValue);
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
            return process.WaitForExit(timeoutMs);
        }
        // The process already exited (ArgumentException: gone before the lookup;
        // InvalidOperationException: gone between the lookup and the kill).
        catch (Exception ex) when (
            ex is ArgumentException or InvalidOperationException)
        {
            return true;
        }
        // The process could not be terminated (e.g. access denied). Do not fail the
        // upgrade here: report that it is still running so the caller can log it, and
        // the subsequent database drop fails loudly if the lock is still held.
        catch (Exception ex) when (ex is Win32Exception or NotSupportedException)
        {
            return false;
        }
    }
}
