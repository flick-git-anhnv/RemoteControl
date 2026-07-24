using System;
using System.Security.Cryptography;
using System.Text;

class Program
{
    static void Main()
    {
        using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048))
        {
            string publicKey = rsa.ToXmlString(false);
            string privateKey = rsa.ToXmlString(true);

            Console.WriteLine("Public Key:");
            Console.WriteLine(publicKey);
            Console.WriteLine("\nPrivate Key (Keep Secret):");
            Console.WriteLine(privateKey);
        }
    }
}
