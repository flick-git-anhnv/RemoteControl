using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IPGS.RemoteControl.CcuClient.Services;

/// <summary>
/// Dịch vụ gửi tín hiệu Wake-on-LAN (Magic Packet) để bật nguồn máy tính từ xa.
/// </summary>
public static class WakeOnLanService
{
    /// <summary>
    /// Gửi Magic Packet đến một địa chỉ MAC.
    /// </summary>
    /// <param name="macAddress">Địa chỉ MAC (định dạng: XX:XX:XX:XX:XX:XX hoặc XX-XX-XX-XX-XX-XX)</param>
    /// <param name="port">Cổng UDP (thường là 7 hoặc 9)</param>
    public static async Task SendMagicPacketAsync(string macAddress, int port = 9)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
            throw new ArgumentException("Địa chỉ MAC không được để trống.", nameof(macAddress));

        // Xóa các ký tự phân cách (: hoặc - hoặc khoảng trắng)
        string cleanedMac = Regex.Replace(macAddress, "[: -]", "");

        if (cleanedMac.Length != 12)
            throw new ArgumentException("Địa chỉ MAC không hợp lệ. Phải gồm 12 ký tự hex.", nameof(macAddress));

        byte[] macBytes = new byte[6];
        for (int i = 0; i < 6; i++)
        {
            macBytes[i] = Convert.ToByte(cleanedMac.Substring(i * 2, 2), 16);
        }

        // Tạo gói tin Magic Packet
        // Gồm 6 byte FF, theo sau là 16 lần lặp lại của địa chỉ MAC (6 byte) => tổng cộng 102 byte
        byte[] magicPacket = new byte[6 + (16 * 6)];
        
        for (int i = 0; i < 6; i++)
        {
            magicPacket[i] = 0xFF;
        }
        
        for (int i = 1; i <= 16; i++)
        {
            Buffer.BlockCopy(macBytes, 0, magicPacket, i * 6, 6);
        }

        // Gửi UDP Broadcast
        using var client = new UdpClient();
        client.EnableBroadcast = true;
        
        var targetEndpoint = new IPEndPoint(IPAddress.Broadcast, port);
        
        await client.SendAsync(magicPacket, magicPacket.Length, targetEndpoint);
    }
}
