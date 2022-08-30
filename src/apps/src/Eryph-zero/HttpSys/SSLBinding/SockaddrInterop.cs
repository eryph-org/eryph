using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
// ReSharper disable IdentifierTypo

// based on https://github.com/segor/SslCertBinding.Net
// licensed under MIT license by https://github.com/segor

namespace Eryph.Runtime.Zero.HttpSys.SSLBinding;

internal class SockaddrInterop
{
    /// <summary>
    ///     Creates an unmanaged sockaddr structure to pass to a WinAPI function.
    /// </summary>
    /// <param name="ipEndPoint">IP address and port number</param>
    /// <returns>a handle for the structure. Use the AddrOfPinnedObject Method to get a stable pointer to the object. </returns>
    /// <remarks>
    ///     When the handle goes out of scope you must explicitly release it by calling the Free method; otherwise, memory
    ///     leaks may occur.
    /// </remarks>
    public static GCHandle CreateSockaddrStructure(IPEndPoint ipEndPoint)
    {
        var socketAddress = ipEndPoint.Serialize();

        // use an array of bytes instead of the sockaddr structure 
        var sockAddrStructureBytes = new byte[socketAddress.Size];
        var sockAddrHandle = GCHandle.Alloc(sockAddrStructureBytes, GCHandleType.Pinned);
        for (var i = 0; i < socketAddress.Size; ++i) sockAddrStructureBytes[i] = socketAddress[i];
        return sockAddrHandle;
    }


    /// <summary>
    ///     Reads the unmanaged sockaddr structure returned by a WinAPI function
    /// </summary>
    /// <param name="pSockaddrStructure">pointer to the unmanaged sockaddr structure</param>
    /// <returns>IP address and port number</returns>
    public static IPEndPoint ReadSockaddrStructure(IntPtr pSockaddrStructure)
    {
        var sAddressFamily = Marshal.ReadInt16(pSockaddrStructure);
        var addressFamily = (AddressFamily)sAddressFamily;

        int sockAddrSructureSize;
        IPEndPoint ipEndPointAny;
        switch (addressFamily)
        {
            case AddressFamily.InterNetwork:
                // IP v4 address
                sockAddrSructureSize = 16;
                ipEndPointAny = new IPEndPoint(IPAddress.Any, 0);
                break;
            case AddressFamily.InterNetworkV6:
                // IP v6 address
                sockAddrSructureSize = 28;
                ipEndPointAny = new IPEndPoint(IPAddress.IPv6Any, 0);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pSockaddrStructure), "Unknown address family");
        }


        // get bytes of the sockadrr structure
        var sockAddrSructureBytes = new byte[sockAddrSructureSize];
        Marshal.Copy(pSockaddrStructure, sockAddrSructureBytes, 0, sockAddrSructureSize);

        // create SocketAddress from bytes
        var socketAddress = new SocketAddress(AddressFamily.Unspecified, sockAddrSructureSize);
        for (var i = 0; i < sockAddrSructureSize; i++) socketAddress[i] = sockAddrSructureBytes[i];

        // create IPEndPoint from SocketAddress
        var result = (IPEndPoint)ipEndPointAny.Create(socketAddress);

        return result;
    }
}