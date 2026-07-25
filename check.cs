using System;
using System.Reflection;

class Program {
    static void Main() {
        var asm = Assembly.LoadFrom(@"E:\KZTEK\Code_Git\5.BaseUI\KztekComponentAvalonia\KztekComponentAvalonia\bin\Debug\net8.0\Avalonia.Base.dll");
        var type = asm.GetType("Avalonia.Input.Platform.IClipboard");
        if (type == null) {
            Console.WriteLine("Type not found.");
            return;
        }
        foreach (var m in type.GetMethods()) {
            Console.WriteLine(m.Name);
        }
    }
}
