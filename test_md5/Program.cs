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
            foreach(string value in LetterGen(12))
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

            // Randomize starting point
            Random r = new Random();
            for (int i = 0; i < chars; i++)
            {
                chardata[i] = (char)('a' + r.Next(26));
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


            K = new UInt32[64];
            for(int i=0;i<64;i++)
            {
                K[i] = (UInt32)Math.Floor(Math.Abs(Math.Sin(i + 1)) * Math.Pow(2, 32));
            }

        }

        UInt32[] K;
        byte[] s = { 7, 12, 17, 22,  7, 12, 17, 22,  7, 12, 17, 22,  7, 12, 17, 22,
                     5,  9, 14, 20,  5,  9, 14, 20,  5,  9, 14, 20,  5,  9, 14, 20,
                     4, 11, 16, 23,  4, 11, 16, 23,  4, 11, 16, 23,  4, 11, 16, 23,
                     6, 10, 15, 21,  6, 10, 15, 21,  6, 10, 15, 21,  6, 10, 15, 21 };


        public int BitsSame(byte[] hash)
        {
            UInt64 hash1, hash2;
            hash1 = BitConverter.ToUInt64(hash, 0);
            hash2 = BitConverter.ToUInt64(hash, 8);

            return BitsSame(hash1, hash2);
        }

        public int BitsSame(UInt64 hash1, UInt64 hash2)
        {

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
            byte[] data = src.ToCharArray().Select(c => (byte)c).ToArray();
//            byte[] hash = md5gen.ComputeHash(data);


            UInt64 hash1, hash2;
            GenerateMd5(data, out hash1, out hash2);

#if false
            // Debug check
            
            UInt64 hash1a, hash2a;
            hash1a = BitConverter.ToUInt64(hash, 0);
            hash2a = BitConverter.ToUInt64(hash, 8);

            if(hash1a != hash1 || hash2a != hash2) throw new Exception("Hash does not match expected value!");

#endif

            return BitsSame(hash1,hash2);
        }


        UInt32[] message = new UInt32[16];

        void GenerateMd5(byte[] input, out UInt64 hash1, out UInt64 hash2)
        {
            Array.Clear(message, 0, 16);
            for (int i = 0; i < input.Length - 3; i += 4)
            {
                message[i / 4] = BitConverter.ToUInt32(input, i);
            }
            for (int i = input.Length&(~3); i < input.Length; i++)
            {
                message[i / 4] |= (uint)(input[i] << ((i & 3) * 8));
            }

            message[14] = (UInt32)(input.Length * 8);
            // add trailing 1 bit
            message[(input.Length) / 4] |= (0x80U << (((input.Length) % 4) * 8));

            UInt32 A, B, C, D;
            UInt32 h0, h1, h2, h3;

            h0 = 0x67452301;   //A
            h1 = 0xefcdab89;   //B
            h2 = 0x98badcfe;   //C
            h3 = 0x10325476;   //D

            A = h0; B = h1; C = h2; D = h3;

            UInt32 F,oldD;
            int g;
            for(int i=0; i<64;i++)
            {
                if(i<16)
                {
                    F = (B & C) | (~B & D);
                    g = i;
                } 
                else if (i<32)
                {
                    F = (D & B) | (~D & C);
                    g = (5 * i + 1) % 16;
                } 
                else if(i<48)
                {
                    F = B ^ C ^ D;
                    g = (3 * i + 5) % 16;
                } 
                else
                {
                    F = C ^ (B | ~D);
                    g = (7 * i) % 16;
                }

                UInt32 T = A + F + K[i] + message[g];
                T = (T >> (32-s[i])) | (T << (s[i]));

                oldD=D;
                D = C;
                C = B;
                B = B + T;
                A = oldD;
            }

            h0 += A;
            h1 += B;
            h2 += C;
            h3 += D;

            hash1 = h0 | ((UInt64)h1 << 32);
            hash2 = h2 | ((UInt64)h3 << 32);
        }



    }
}
