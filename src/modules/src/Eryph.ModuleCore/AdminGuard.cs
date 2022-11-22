﻿using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Eryph.ModuleCore
{
    public static class AdminGuard
    {

        public static bool IsElevated => IsElevatedProcess();

        private static bool IsElevatedProcess()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return geteuid() == 0;


            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            return principal.IsInRole(WindowsBuiltInRole.Administrator);

        }

        public static Task<int> CommandIsElevated(Func<Task<int>> IsElevated) =>
            InElevatedProcess(() =>
            {
                Console.Error.WriteLine("This operation requires administrative privileges.");
                return Task.FromResult(-10);
            }, IsElevated);


        // ReSharper disable once ParameterHidesMember
        // ReSharper disable InconsistentNaming
        public static TOut InElevatedProcess<TOut>(Func<TOut> NotElevated, Func<TOut> IsElevated) => AdminGuard.IsElevated ? IsElevated() : NotElevated();


        // ReSharper disable once StringLiteralTypo
        [DllImport("libc")]
        // ReSharper disable once IdentifierTypo
        public static extern uint geteuid();
    }
}
