using System;
using IPGS.RemoteControl.CcuClient;
class Program {
    static void Main() {
        var store = new ComputerProfileStore();
        var profiles = store.GetAll();
        Console.WriteLine("Total profiles: " + profiles.Count);
        if (profiles.Count > 0) {
            var p = profiles[0];
            Console.WriteLine("MAC before: " + p.MacAddress);
            p.MacAddress = "04:2b:58:05:06:a2";
            store.Save(p);
            Console.WriteLine("Saved.");
        }
    }
}
