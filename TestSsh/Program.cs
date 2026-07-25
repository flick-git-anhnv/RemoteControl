using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Renci.SshNet;

class Program {
    static void Main() {
        try {
            using var ssh = new SshClient("192.168.21.230", "kztek", "123456");
            ssh.Connect();
            Console.WriteLine("SSH connected.");
            
            // Start tcpdump in background on Linux
            var cmd = ssh.CreateCommand("echo '123456' | sudo -S tcpdump -i any udp port 9 -c 5 -w /tmp/wol.cap & echo $!");
            cmd.Execute();
            Console.WriteLine("tcpdump started.");
            
            Thread.Sleep(2000);
            
            // Send WOL packet
            string macAddress = "04:2b:58:05:06:a2";
            string cleanedMac = Regex.Replace(macAddress, "[: -]", "");
            byte[] macBytes = new byte[6];
            for (int i = 0; i < 6; i++) {
                macBytes[i] = Convert.ToByte(cleanedMac.Substring(i * 2, 2), 16);
            }
            byte[] magicPacket = new byte[6 + (16 * 6)];
            for (int i = 0; i < 6; i++) magicPacket[i] = 0xFF;
            for (int i = 1; i <= 16; i++) Buffer.BlockCopy(macBytes, 0, magicPacket, i * 6, 6);
            
            using var client = new UdpClient();
            client.EnableBroadcast = true;
            client.Send(magicPacket, magicPacket.Length, new IPEndPoint(IPAddress.Broadcast, 9));
            Console.WriteLine("Magic packet sent to 255.255.255.255.");
            
            Thread.Sleep(3000);
            
            // Check if tcpdump captured anything
            var readCmd = ssh.CreateCommand("echo '123456' | sudo -S tcpdump -r /tmp/wol.cap");
            readCmd.Execute();
            Console.WriteLine("PCAP RESULT: " + readCmd.Result);
            
            var killCmd = ssh.CreateCommand("echo '123456' | sudo -S killall tcpdump");
            killCmd.Execute();
            
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
