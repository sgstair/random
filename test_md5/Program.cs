using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace test_md5
{
    class Program
    {
        static void Main(string[] args)
        {
            FileStream fs = File.Open("test_md5_log.txt", FileMode.Append, FileAccess.Write, FileShare.Read);
            StreamWriter sw = new StreamWriter(fs);

            sw.WriteLine("Launching {0}", DateTime.Now);
            sw.Flush();

            Test t = new Test("29c986a49abf80e9edf2ffe8efb7e040");

            int groupsize = 500000;
            int i = 0;
            int found = 0;
            DateTime seg = DateTime.Now;
            int maxlen = 0;
            foreach(string value in LetterGen(10))
            {
                int len = t.HashSame(value);

                if(len > maxlen)
                {
                    maxlen = len;
                }
                if (len > 107)
                {
                    Console.WriteLine("{0} {1}", value, len);
                    sw.WriteLine("{0} {1}", value, len);
                    sw.Flush();
                    found++;
                }

                i++;
                if(i>=groupsize)
                {
                    DateTime mark = DateTime.Now;
                    i = 0;
                    double span = mark.Subtract(seg).TotalSeconds;
                    Console.WriteLine("{0:n2} MH/s, maxlen {1}, {2}", (groupsize / span) / 1000000, maxlen, value);
                    seg = mark;
                }
            }

            sw.Close();
                
        }

        static IEnumerable<string> LetterGen(int chars)
        {
            char[] chardata = new char[chars];

            for (int i = 0; i < chars; i++)
            {
                chardata[i] = 'a';
            }

            while (true)
            {
                for (int i = 0; i < chars; i++)
                {
                    if (chardata[i] == 'z')
                    {
                        chardata[i] = 'a';
                    }
                    else
                    {
                        chardata[i]++;
                        break;
                    }
                }
                yield return new string(chardata);
            }

        }

    }

    class Test
    {
        UInt64 comp1, comp2;
        MD5 md5gen;
        public Test(string comp_md5)
        {
            byte[] data = new byte[16];
            for(int i=0;i<16;i++)
            {
                data[i] = Convert.ToByte(comp_md5.Substring(i * 2, 2), 16);
            }
            comp1 = BitConverter.ToUInt64(data, 0);
            comp2 = BitConverter.ToUInt64(data, 8);
            md5gen = MD5.Create();
        }

        public int BitsSame(byte[] hash)
        {
            UInt64 hash1, hash2;
            hash1 = BitConverter.ToUInt64(hash, 0);
            hash2 = BitConverter.ToUInt64(hash, 8);

            UInt64 test1, test2;

            test1 = ~(hash1 ^ comp1);
            test2 = ~(hash2 ^ comp2);


            test1 = (test1 & 0x5555555555555555L) + ((test1 & 0xAAAAAAAAAAAAAAAAL) >> 1);
            test1 = (test1 & 0x3333333333333333L) + ((test1 & 0xCCCCCCCCCCCCCCCCL) >> 2);
            test1 = (test1 & 0x0F0F0F0F0F0F0F0FL) + ((test1 & 0xF0F0F0F0F0F0F0F0L) >> 4);
            test1 = (test1 & 0x00FF00FF00FF00FFL) + ((test1 & 0xFF00FF00FF00FF00L) >> 8);
            test1 = (test1 & 0x0000FFFF0000FFFFL) + ((test1 & 0xFFFF0000FFFF0000L) >> 16);
            test1 = (test1 & 0x00000000FFFFFFFFL) + ((test1 & 0xFFFFFFFF00000000L) >> 32);

            test2 = (test2 & 0x5555555555555555L) + ((test2 & 0xAAAAAAAAAAAAAAAAL) >> 1);
            test2 = (test2 & 0x3333333333333333L) + ((test2 & 0xCCCCCCCCCCCCCCCCL) >> 2);
            test2 = (test2 & 0x0F0F0F0F0F0F0F0FL) + ((test2 & 0xF0F0F0F0F0F0F0F0L) >> 4);
            test2 = (test2 & 0x00FF00FF00FF00FFL) + ((test2 & 0xFF00FF00FF00FF00L) >> 8);
            test2 = (test2 & 0x0000FFFF0000FFFFL) + ((test2 & 0xFFFF0000FFFF0000L) >> 16);
            test2 = (test2 & 0x00000000FFFFFFFFL) + ((test2 & 0xFFFFFFFF00000000L) >> 32);

            return (int)(test1 + test2);
        }

        public int HashSame(string src)
        {
            byte[] hash = md5gen.ComputeHash(src.ToCharArray().Select(c => (byte)c).ToArray());

            return BitsSame(hash);
        }

    }
}
