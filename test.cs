using System;
using System.Reflection;
public class Test {
    public static void Run() {
        Assembly asm = Assembly.LoadFile(@"E:\KZTEK\Code_Git\6.RemoteControlTool\IPGS.RemoteControl.CcuUI\bin\Debug\net8.0\KztekComponentAvalonia.dll");
        Type t = asm.GetType("KztekComponentAvalonia.Controls.KzTextBox");
        if (t == null) { Console.WriteLine("Type not found"); return; }
        Console.WriteLine("Base: " + t.BaseType.Name);
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
            Console.WriteLine("Prop: " + p.Name + " (" + p.PropertyType.Name + ")");
        }
    }
}
