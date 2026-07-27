using System;
using Renci.SshNet;

class Program {
    static void Main() {
        try {
            using var ssh = new SshClient("192.168.21.16", "kztek", "123456");
            ssh.Connect();
            Console.WriteLine("SSH connected");
            
            string cmdStr = "ls -la /usr/bin/*kiosk* /usr/local/bin/*kiosk* 2>/dev/null";
            var cmd = ssh.CreateCommand(cmdStr);
            var result = cmd.Execute();
            Console.WriteLine("Output:\n" + result);
            if (!string.IsNullOrEmpty(cmd.Error)) {
                Console.WriteLine("Error Output:\n" + cmd.Error);
            }
        } catch (Exception ex) {
            Console.WriteLine("Exception: " + ex.Message);
        }
    }
}
