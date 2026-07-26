using System;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;

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

        // Q10: kiểm tra hex tường minh — nếu chỉ check Length==12, chuỗi 12 ký tự
        // không-hex sẽ khiến Convert.ToByte ném FormatException (sai với cam kết
        // ArgumentException của hàm này).
        if (!Regex.IsMatch(cleanedMac, "^[0-9A-Fa-f]{12}$"))
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

        // Gửi UDP Broadcast trên tất cả các subnet
        var endpoints = new List<IPEndPoint>();
        endpoints.Add(new IPEndPoint(IPAddress.Broadcast, port));
        
        try
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    foreach (var ip in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork && ip.IPv4Mask != null)
                        {
                            var addressBytes = ip.Address.GetAddressBytes();
                            var maskBytes = ip.IPv4Mask.GetAddressBytes();
                            
                            if (addressBytes.Length == 4 && maskBytes.Length == 4)
                            {
                                var broadcastBytes = new byte[4];
                                for (int i = 0; i < 4; i++)
                                {
                                    broadcastBytes[i] = (byte)(addressBytes[i] | ~maskBytes[i]);
                                }
                                endpoints.Add(new IPEndPoint(new IPAddress(broadcastBytes), port));
                            }
                        }
                    }
                }
            }
        }
        catch { }

        foreach (var endpoint in endpoints)
        {
            try
            {
                using var client = new UdpClient();
                client.EnableBroadcast = true;
                await client.SendAsync(magicPacket, magicPacket.Length, endpoint);
            }
            catch { }
        }
    }
}
