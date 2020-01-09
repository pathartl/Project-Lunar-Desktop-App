using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LunarLib
{
    public static class Utilities
    {
        public static string Md5Hash(string path)
        {
            using (FileStream stream = File.OpenRead(path.ToString()))
            {
                return Md5Hash(stream);
            }
        }

        public static string Md5Hash(Stream input)
        {
            var md5Hash = MD5.Create();
            byte[] data = md5Hash.ComputeHash(input);

            var stringBuilder = new StringBuilder();

            for (int i = 0; i < data.Length; i++)
            {
                stringBuilder.Append(data[i].ToString("x2"));
            }

            return stringBuilder.ToString();
        }
    }
}
