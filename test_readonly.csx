using System;
using System.Linq;
using System.Reflection;

var type = typeof(Microsoft.Build.Framework.AbsolutePath);
Console.WriteLine("IsValueType: " + type.IsValueType);

// Check type-level attributes
foreach (var attr in type.GetCustomAttributes())
    Console.WriteLine("Type attr: " + attr.GetType().FullName);

// Check instance methods
var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
foreach (var m in methods.Where(m => !m.IsSpecialName))
{
    Console.WriteLine("Method: " + m.Name);
    Console.WriteLine("  ReturnParam modifiers: " + string.Join(",", m.ReturnParameter.GetRequiredCustomModifiers().Select(t => t.FullName)));
    Console.WriteLine("  Method attrs: " + string.Join(",", m.GetCustomAttributes().Select(a => a.GetType().FullName)));
    foreach (var p in m.GetParameters())
    {
        Console.WriteLine("  Param " + p.Name + " modifiers: " + string.Join(",", p.GetRequiredCustomModifiers().Select(t => t.FullName)));
    }
}
