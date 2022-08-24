using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    internal interface IWorker 
    {
        Task<List<WorkerResult>> GetServerValues(List<int> inVals, WorkMode mode);
    }


    internal class Worker : IWorker
    {
        const int TryCount = 10;
        const int DelayTime = 2; //в секундах
        const int KeyDelayTime = 5;//в секундах
        readonly string Name;           
        Func< string, string, string>   GetNewKey;
        readonly IDataProvider DataProvider = new DataProvider();
        public Worker(int threadNum, Func<string, string, string> getNewKey)
        {            
            Name = $"Worker {threadNum}";            
            GetNewKey = getNewKey;
        }
        public async Task<List<WorkerResult>> GetServerValues(List<int> inVals,WorkMode mode)
        {
            return await Task.Run(() =>
            {
                
                var list = inVals.AsParallel().Select(x => GetServerValue(x,mode).Result).ToList();
                return list;
            }
            );
        }

        async Task<WorkerResult> GetServerValue(int val, WorkMode mode)
        {           
            var result = new WorkerResult
            {
                InVal = val,
                OutVal = -1,
            };
            var tryCnt = 0;
            
            var tmpKey = mode == WorkMode.Advanced ? GetNewKey(Name,string.Empty) : string.Empty;
            while (tryCnt < TryCount)
            {
                if(mode == WorkMode.Base)
                    Console.WriteLine($"Worker {Name}: попытка {tryCnt} получения данных от сервера для значения {val}");
                else
                    Console.WriteLine($"Worker {Name}: попытка {tryCnt} получения данных от сервера для значения {val} c ключом {tmpKey}");
                try
                {
                    var serverValue = mode == WorkMode.Base ? await DataProvider.GetValueFromServer(val) :
                        await DataProvider.GetAdvancedValueFromServer(val, tmpKey);
                    result.OutVal = serverValue;
                    if (serverValue > 0)
                    {
                        Console.WriteLine($"Worker {Name}: для {val} ответ {serverValue}");
                        return result;
                    }
                }
                catch (ExpiredKeyException eex)
                {
                    Console.WriteLine($"Worker {Name}: протух ключ {eex.Message}");
                    //обновляем ключ                                                          
                    tmpKey = GetNewKey(Name,tmpKey);
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Worker {Name}: ошибка {ex.Message}");
                    //потормозим перед следующей попыткой
                    await Task.Delay(DelayTime * 1000 * (tryCnt + 1));
                }                
                tryCnt++;
            }
            return result;
        }

        async Task<string> GetAdvancedKeyFromServer()
        {            
            var tmpkey = string.Empty;
            var tryCnt = 0;
            while (tryCnt < TryCount)
            {
                Console.WriteLine($"Worker {Name} - попытка {tryCnt} получения ключа");
                try
                {
                    tmpkey = await DataProvider.GetAdvancedKey();
                    Console.WriteLine($"Worker {Name} - Новый ключ: {tmpkey}");
                    return tmpkey;
                }
                catch (RateLimitException rlEx)
                {
                    Console.WriteLine($"Worker {Name} -Слишком много запросов ключа: {rlEx.Message}");
                    await Task.Delay(KeyDelayTime * 1000 * (tryCnt + 1));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Worker {Name} - Ошибка запроса ключа: {ex.Message}");
                    await Task.Delay(DelayTime * 1000 * (tryCnt + 1));
                }
                tryCnt++;
            }
            return string.Empty;
        }        

    }

    internal enum WorkMode 
    {         
        Base,
        Advanced
    }

    internal class WorkerResult 
    { 
        public int InVal { get; set; }
        public int OutVal { get; set; }
    }
}
