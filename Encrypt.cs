using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace encrypt
{
    class EncryptClass
    {
        string key = "@DotNET Encrypt String@";

        public EncryptClass()
        {
        }

        #region EncryptPassword
        public static string EncryptPassword(string password)
        {
            string outParam = null;

            try
            {
                byte[] keyMD5Hash;
                byte[] passwordBytes;

                MD5CryptoServiceProvider hashMD5 = new MD5CryptoServiceProvider();
                keyMD5Hash = hashMD5.ComputeHash(ASCIIEncoding.ASCII.GetBytes(key));
                hashMD5 = null;

                TripleDESCryptoServiceProvider tripleDES = new TripleDESCryptoServiceProvider();
                tripleDES.Key = keyMD5Hash;
                tripleDES.Mode = CipherMode.ECB;

                passwordBytes = ASCIIEncoding.ASCII.GetBytes(password);

                outParam = Convert.ToBase64String(tripleDES.CreateEncryptor().TransformFinalBlock(passwordBytes, 0, passwordBytes.Length));

                tripleDES = null;
            }
            catch (Exception)
            {
                outParam = null;
            }

            return outParam;
        }
        #endregion

        #region DecryptPassword
        public static string DecryptPassword(string password)
        {
            string outParam = null;

            try
            {
                byte[] keyMD5Hash;
                byte[] passwordBytes;

                MD5CryptoServiceProvider hashMD5 = new MD5CryptoServiceProvider();
                keyMD5Hash = hashMD5.ComputeHash(ASCIIEncoding.ASCII.GetBytes(key));
                hashMD5 = null;

                TripleDESCryptoServiceProvider tripleDES = new TripleDESCryptoServiceProvider();
                tripleDES.Key = keyMD5Hash;
                tripleDES.Mode = CipherMode.ECB;

                passwordBytes = Encoding.Unicode.GetBytes(password);

                byte[] bytes = tripleDES.CreateDecryptor().TransformFinalBlock(passwordBytes, 0, passwordBytes.Length);

                outParam = Convert.ToBase64String(tripleDES.CreateEncryptor().TransformFinalBlock(passwordBytes, 0, passwordBytes.Length));

                tripleDES = null;
            }
            catch (Exception)
            {
                outParam = null;
            }

            return outParam;
        }
        #endregion
    }
}