using System;
using System.IO;
using System.Windows.Forms;
namespace CiyuanWindows {
 static class Program {
  [STAThread] static void Main(string[] args) {
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CiyuanWindowsReplica"),testScript="";
   for(int i=0;i<args.Length;i++){if(args[i]=="--data-dir"&&i+1<args.Length)data=Path.GetFullPath(args[++i]);else if(args[i]=="--test-script"&&i+1<args.Length)testScript=Path.GetFullPath(args[++i]);}
   if(testScript!=""){Directory.CreateDirectory(data);Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);Application.ThreadException+=(s,e)=>{File.WriteAllText(Path.Combine(data,"test-unhandled.txt"),e.Exception.ToString());Environment.Exit(2);};}
   try {Application.Run(new MainWindow(data,testScript));}catch(Exception e){MessageBox.Show("程序启动失败："+e.GetBaseException().Message,"次元库 Windows 复刻版");}
  }
 }
}
