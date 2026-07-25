using System;
using Renci.SshNet;

class Program
{
    static void Main()
    {
        using (var client = new SshClient("192.168.21.230", 22, "kztek", "kztek123456"))
        {
            client.Connect();
            Console.WriteLine("Connected!");
            var cmd = client.RunCommand("ps aux | grep ZcuAgent");
            Console.WriteLine("PROCESSES:");
            Console.WriteLine(cmd.Result);
            
            var cmd2 = client.RunCommand("ls -la /opt/ || ls -la ~/");
            Console.WriteLine("FILES:");
            Console.WriteLine(cmd2.Result);
            
            client.Disconnect();
        }
    }
}
