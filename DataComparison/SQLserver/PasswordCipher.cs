using System;
using System.Text;

namespace DataComparison.SQLserver
{
    /// <summary>
    /// 密碼簡易混淆:Base64 編碼後,除了 '=' 以外的字元一律轉為位元補數。
    /// 補數運算對稱,加解密都呼叫同一個 Complement 方法。
    /// </summary>
    public static class PasswordCipher
    {
        public static string Encode(string plainText)
        {
            if (plainText == null)
            {
                plainText = string.Empty;
            }

            var base64Text = Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
            return Complement(base64Text);
        }

        public static string Decode(string encodedText)
        {
            if (string.IsNullOrEmpty(encodedText))
            {
                return string.Empty;
            }

            var base64Text = Complement(encodedText);
            var bytes = Convert.FromBase64String(base64Text);
            return Encoding.UTF8.GetString(bytes);
        }

        private static string Complement(string text)
        {
            var chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] != '=')
                {
                    chars[i] = (char)(~chars[i] & 0xFF);
                }
            }

            return new string(chars);
        }
    }
}
