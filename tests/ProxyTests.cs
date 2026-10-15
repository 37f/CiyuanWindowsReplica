using System;using System.Linq;using System.Text;using System.Security.Cryptography;using System.Net;using System.Net.Sockets;using System.IO;using System.Threading.Tasks;using CiyuanWindows;
class ProxyTests {static int Main(){try{
 if(!BridgePolicy.Trusted("https://app.ciyuan.local/index.html")||BridgePolicy.Trusted("https://evil.invalid/index.html")||BridgePolicy.Trusted("https://app.ciyuan.local.evil.invalid/index.html"))throw new Exception("bridge origin violation");
 byte[] clear=new byte[80];clear[0]=255;clear[1]=216;clear[2]=255;
 byte[] wrapped;using(var aes=Aes.Create()){aes.Key=Encoding.UTF8.GetBytes("0B6666A0-BB59-1381-B746-a0E4C9AC").Concat(new byte[32]).Take(32).ToArray();aes.IV=Enumerable.Range(0,16).Select(x=>(byte)x).ToArray();using(var enc=aes.CreateEncryptor())wrapped=aes.IV.Concat(enc.TransformFinalBlock(clear,0,clear.Length)).ToArray();}
 if(!AssetProxy.DecodeImage(wrapped).SequenceEqual(clear))throw new Exception("image wrapper decode failed");
 Console.WriteLine("PASS remote bridge denied, ordinary image wrapper decoded");
 var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;
 var observed=Task.Run(()=>{using(var socket=listener.AcceptTcpClient())using(var stream=socket.GetStream()){var request=new StringBuilder();int ch;while((ch=stream.ReadByte())>=0){request.Append((char)ch);if(request.ToString().EndsWith("\r\n\r\n"))break;}byte[] answer=Encoding.ASCII.GetBytes("HTTP/1.1 206 Partial Content\r\nContent-Type: video/mp4\r\nContent-Length: 8\r\nContent-Range: bytes 100-107/100000000\r\nConnection: close\r\n\r\n12345678");stream.Write(answer,0,answer.Length);return request.ToString();}});
 using(var proxy=new AssetProxy()){var result=proxy.FetchAsync("http://127.0.0.1:"+port+"/large.mp4","http://127.0.0.1/","bytes=100-").GetAwaiter().GetResult();if(result.Status!=206||result.Body.Length!=8)throw new Exception("Partial MP4 response lost");}
 string headers=observed.GetAwaiter().GetResult();listener.Stop();var range=System.Text.RegularExpressions.Regex.Match(headers,@"Range: bytes=100-(\d+)\r\n",System.Text.RegularExpressions.RegexOptions.IgnoreCase);if(!range.Success||Int64.Parse(range.Groups[1].Value)>5000000)throw new Exception("Open-ended large video request was not bounded");
 Console.WriteLine("PASS large MP4 uses bounded partial range");return 0;
 }catch(Exception e){Console.WriteLine("FAIL "+e.Message);return 1;}}}
