# AssemblyComparer
A library allowing you to get the fundamental differences between two managed .NET Assemblies

# Example Usage
```cs
Comparator comparator = new Comparator();

byte[] oldAssemblyBytes = File.ReadAllBytes($".../old.dll");
byte[] newAssemblyBytes = File.ReadAllBytes($".../new.dll");
var oldMemoryStream = new MemoryStream(oldAssemblyBytes);
var newMemoryStream = new MemoryStream(newAssemblyBytes);

Difference[] differences = comparator.Compare(oldMemoryStream, newMemoryStream);

foreach (var difference in differences)
{
    Console.WriteLine(difference.Subject.ToString() + ":");
    Console.WriteLine($"\tType:        {difference.Type.ToString()}");
    Console.WriteLine($"\tOld Value:   {difference.OldValue}");
    Console.WriteLine($"\tNew Value:   {difference.NewValue}");
}
```
