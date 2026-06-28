// Copyright © 2026 José Luis Bautista Martín. Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SilentRunner;

/// <summary>
/// Encapsula un Windows Job Object con JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE.
/// Todos los procesos asignados son terminados automáticamente por Windows en cuanto
/// este objeto se libera — incluso si el launcher es eliminado con el Administrador de tareas.
/// </summary>
internal sealed class ProcessJob : IDisposable
{
    private readonly nint _hJob;
    private bool _disposed;

    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit, JobMemoryLimit;
        public nuint PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateJobObject(nint lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        nint hJob, int JobObjectInfoClass,
        ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInfo,
        int cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(nint hJob, nint hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint hObject);

    internal ProcessJob()
    {
        _hJob = CreateJobObject(0, null);
        if (_hJob == 0)
            throw new InvalidOperationException(
                $"CreateJobObject falló (Win32 error {Marshal.GetLastWin32Error()})");

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

        if (!SetInformationJobObject(_hJob, JobObjectExtendedLimitInformation,
                ref info, Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()))
        {
            CloseHandle(_hJob);
            throw new InvalidOperationException(
                $"SetInformationJobObject falló (Win32 error {Marshal.GetLastWin32Error()})");
        }
    }

    /// <summary>
    /// Intenta asignar el proceso al job. Devuelve <c>false</c> si ya terminó o falló la asignación.
    /// No lanza excepción — un fallo de asignación se registra en el log del llamador.
    /// </summary>
    internal bool TryAssign(Process proceso) =>
        !_disposed && !proceso.HasExited &&
        AssignProcessToJobObject(_hJob, proceso.Handle);

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            CloseHandle(_hJob);
        }
    }
}