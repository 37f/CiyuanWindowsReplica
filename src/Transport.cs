using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CiyuanWindows {
    public static class Transport {
        // This is the supplied app's transport encoding, not a user credential.
        private const string TransportPhrase = "%ZZJ_ciyuan_2025!@";
        public static string Decode(string body) {
            if(body == null) throw new FormatException("服务器返回了空内容");
            string text = body.TrimStart('\uFEFF').Trim();
            if(text.StartsWith("Dddm",StringComparison.Ordinal)) return Encoding.UTF8.GetString(Convert.FromBase64String(text.Substring(4)));
            if(!text.StartsWith("CYK2026",StringComparison.Ordinal)) return text;
            string[] parts = text.Substring(7).Split(':');
            if(parts.Length != 2) throw new FormatException("服务器加密响应格式不完整");
            byte[] iv = Convert.FromBase64String(parts[0]);
            byte[] encrypted = Convert.FromBase64String(parts[1]);
            if(iv.Length != 16 || encrypted.Length == 0 || encrypted.Length % 16 != 0) throw new FormatException("服务器加密响应长度无效");
            byte[] key;
            using(var sha = SHA256.Create()) key = sha.ComputeHash(Encoding.UTF8.GetBytes(TransportPhrase));
            using(var aes = Aes.Create()) {
                aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using(var decryptor = aes.CreateDecryptor()) return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(encrypted,0,encrypted.Length));
            }
        }
    }
}
