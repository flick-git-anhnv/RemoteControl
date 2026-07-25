using System;
using System.Reflection;
using System.Linq;

class Program {
    static void Main() {
        var asm = typeof(Avalonia.Input.Platform.IClipboard).Assembly;
        var types = asm.GetTypes().Where(t => t.Name.Contains("Clipboard")).ToList();
        foreach (var t in types) {
            Console.WriteLine(t.FullName);
            foreach (var m in t.GetMethods()) {
                Console.WriteLine("  " + m.Name);
            }
        }
    }
}
