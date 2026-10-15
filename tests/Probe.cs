using System;using System.IO;using CiyuanWindows;
class Probe {static int Main(string[] args){try{using(var c=new ContentClient()){object value=c.InvokeAsync(args[0],J.Obj(J.Parse(args[1]))).GetAwaiter().GetResult();File.WriteAllText(args[2],J.Stringify(value));Console.WriteLine("saved "+args[2]);return 0;}}catch(Exception e){Console.WriteLine(e.GetBaseException().Message);return 1;}}}
