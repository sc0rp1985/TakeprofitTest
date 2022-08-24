using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    internal interface IDataProvider 
    {
        Task<int> GetValueFromServer(int inputValue);
        Task<int> GetAdvancedValueFromServer(int inputValue, string key);
        Task<string> GetAdvancedKey();
        Task<string> CheckMedian(double median, WorkMode mode);
    }

    internal class DataProvider : IDataProvider
    {
        public const string KeyHasExpared = "Key has expired";
        public const string RateLimit = "Rate limit. Please wait some time then repeat";

        public async Task<int> GetValueFromServer(int inputValue)
        {
            var strFromServer = await ReadDataFromServer($"{inputValue}\n");
            Console.WriteLine($"исходное значение {inputValue} - ответ сервера {strFromServer}");
            if (strFromServer == string.Empty)
                return -1;
            var intVal = ParseStr(strFromServer);
            return intVal;
        }

        public async Task<int> GetAdvancedValueFromServer(int inputValue, string key)
        {            
            var strFromServer = await ReadDataFromServer($"{key}|{inputValue}\n");
            Console.WriteLine($"исходное значение {inputValue} - ответ сервера {strFromServer}");
            if (strFromServer == string.Empty)                
                return -1;
            if (strFromServer.Contains(KeyHasExpared))
                throw new ExpiredKeyException(KeyHasExpared);

            var intVal = ParseStr(strFromServer);
            return intVal;
        }

        public async Task<string> GetAdvancedKey()
        {
            var strFromServer = await ReadDataFromServer($"Register\n");            
            Console.WriteLine($"Получение ключа - {strFromServer}");
            if (strFromServer.Contains(RateLimit))
                throw new RateLimitException(RateLimit);
            return strFromServer.Trim().Substring(3);
        }

        public async Task<string> CheckMedian(double median, WorkMode mode)
        {
            var command = mode == WorkMode.Base ? $"Check {median}\n" : $"Check_Advanced {median}\n";
            return await ReadDataFromServer(command);
        }

        static int ParseStr(string str)
        {
            if (str.Last() != '\n')
                return -1;            
            var digits = string.Join("", str.Where(x => char.IsDigit(x)).ToList());
            var intVal = -1;
            if (Int32.TryParse(digits,out intVal))            
                return intVal;
            return -1;            
        }

        async Task<string> ReadDataFromServer(string command)
        {
            IPEndPoint ipe = new IPEndPoint(IPAddress.Parse("88.212.241.115"), 2013);
            using var tempSocket =
                   new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            tempSocket.ReceiveTimeout = 1000;
            tempSocket.SendTimeout = 1000;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);            
            try
            {
                await tempSocket.ConnectAsync(ipe);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Ошибка соединения", ex);
            }

            Byte[] bytesReceived = new Byte[256];
            int bytes = 0;
            var page = string.Empty;
            if (tempSocket.Connected)
            {
                try
                {
                    await tempSocket.SendAsync(Encoding.ASCII.GetBytes(command), 0);
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Ошибка отправки команды на сервер: {ex.Message}");                    
                }
                do
                {
                    try
                    {
                        bytes = await tempSocket.ReceiveAsync(bytesReceived, SocketFlags.None);
                    }
                    catch (Exception ex)
                    {
                        throw new ApplicationException($"Ошибка получения ответа от сервера: {ex.Message}");                     
                    }
                    var encoding = Encoding.GetEncoding("koi8-r");
                    page = page + encoding.GetString(bytesReceived, 0, bytes);
                }
                while (bytes > 0);                
                return page;               
            }
            return string.Empty;
        }
    }

    public class ExpiredKeyException : Exception
    {

        public ExpiredKeyException()
        {
        }

        public ExpiredKeyException(string message) : base(message)
        {
        }

        public ExpiredKeyException(string message, Exception inner) : base(message, inner)
        {
        }

    }

    public class RateLimitException : Exception 
    {
        public RateLimitException()
        {
        }

        public RateLimitException(string message) : base(message)
        {
        }

        public RateLimitException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
