using System.Net;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Modules.VmHostAgent.Networks;
using Microsoft.Extensions.Logging;
using Moq;
using Vanara.PInvoke;

namespace Eryph.Modules.VmHostAgent.Test.Networks;

public class ArpUpdateRequestedEventHandlerTests
{
    private readonly Mock<IWindowsArpCache> _mockArpCache;
    private readonly ArpUpdateRequestedEventHandler _handler;

    public ArpUpdateRequestedEventHandlerTests()
    {
        Mock<ILogger> mockLogger = new();
        _mockArpCache = new Mock<IWindowsArpCache>();
        _handler = new ArpUpdateRequestedEventHandler(mockLogger.Object, _mockArpCache.Object);
    }

    [Theory]
    [InlineData("192.168.1.1", "00:11:22:33:44:55", "192.168.1.1", "00:11:22:33:44:55", false)]
    [InlineData("192.168.1.2", "00:11:22:33:44:56", "192.168.1.1", "00:11:22:33:44:10", false)]
    [InlineData("192.168.1.2", "00:11:22:33:44:56", "192.168.1.2", "00:11:22:33:44:10", true)]
    [InlineData("192.168.1.3", "", "192.168.1.3", "00:11:22:33:40:55", true)]
    public async Task Handle_ShouldProcessArpRecordsCorrectly(string ipAddress, 
        string macAddress, string existingIp, string existingMacAddress, bool shouldDelete)
    {
        // Arrange
        var arpUpdateRequestedEvent = new ArpUpdateRequestedEvent
        {
            UpdatedAddresses = new[] { new ArpRecord { IpAddress = ipAddress, MacAddress = macAddress } }
        };

        var arpTableRows = new List<IpHlpApi.MIB_IPNETROW>
        {
            new()
            {
                dwAddr = new Ws2_32.IN_ADDR(IPAddress.Parse(existingIp).GetAddressBytes()),
                bPhysAddr = existingMacAddress.Split(':')
                    .Select(s => Convert.ToByte(s, 16)).Append(new byte[2]).ToArray(),
                dwType = IpHlpApi.MIB_IPNET_TYPE.MIB_IPNET_TYPE_DYNAMIC
            }
        };
        _mockArpCache.Setup(m => m.GetIpNetTable()).Returns(arpTableRows.ToArray());

        // Act
        await _handler.Handle(arpUpdateRequestedEvent);

        // Assert
        if (!shouldDelete)
        {
            _mockArpCache.Verify(m => 
                m.DeleteIpNetEntry(It.IsAny<IpHlpApi.MIB_IPNETROW>()), Times.Never);
        }
        else
        {
            _mockArpCache.Verify(m => 
                m.DeleteIpNetEntry(It.IsAny<IpHlpApi.MIB_IPNETROW>()), Times.Once);
        }
    }

    [Fact]
    public async Task Should_process_large_arp_table_efficiently()
    {
        // Arrange
        var arpUpdateRequestedEvent = new ArpUpdateRequestedEvent
        {
            UpdatedAddresses = Enumerable.Range(0, 1000).Select(i => new ArpRecord
            {
                IpAddress = $"192.168.{i / 256}.{i % 256}", MacAddress = "00:11:22:33:44:55"
            }).ToArray()
        };

        var arpTableRows = Enumerable.Range(0, 10000).Select(i => new IpHlpApi.MIB_IPNETROW
        {
            dwAddr = new Ws2_32.IN_ADDR(IPAddress.Parse($"192.168.{i / 256}.{i % 256}").GetAddressBytes()),
            bPhysAddr = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 },
            dwType = IpHlpApi.MIB_IPNET_TYPE.MIB_IPNET_TYPE_DYNAMIC
        }).ToList();

        _mockArpCache.Setup(m => m.GetIpNetTable()).Returns(arpTableRows.ToArray());

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _handler.Handle(arpUpdateRequestedEvent);
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 2000, "Processing took too long");
    }
}