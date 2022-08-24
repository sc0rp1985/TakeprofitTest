// See https://aka.ms/new-console-template for more information
using ConsoleApp1;


const int threadCnt = 100;
const int endVal = 2018;
var lockObj = new object();
var dataProvider = new DataProvider();

var mode = WorkMode.Base;
do
{
    Console.WriteLine("Выбор режима работы: 1 - base, 2 - advanced");
    var modeKey = Console.ReadKey();
    if (modeKey.KeyChar != '1' && modeKey.KeyChar != '2')
    {
        Console.WriteLine("Не верный ввод режима");
    }
    else 
    {
        mode = modeKey.KeyChar == '1' ? WorkMode.Base : WorkMode.Advanced;
        break;
    }
}
while (true);

Console.WriteLine($"Выбран режим {mode}");

var currentKey = mode == WorkMode.Advanced ? GetAdvancedKeyFromServer("Init") : string.Empty ;
var inputValList = Enumerable.Range(1, endVal).ToList();
var workerResultList = new List<WorkerResult>(endVal);
do
{
    var tmpList = GetValuesFromServer(inputValList,mode);
    foreach (var tmp in tmpList)
    {
        var obj = workerResultList.FirstOrDefault(x => x.InVal == tmp.InVal);
        if (obj == null)
            workerResultList.Add(tmp);
        else
            obj.OutVal = tmp.OutVal;
    }
    inputValList = tmpList.Where(x => x.OutVal == -1).Select(x=>x.InVal).ToList();
}
while (inputValList.Any());

using (var sw = new StreamWriter(@"d:\values.csv"))
{
    foreach (var obj in workerResultList)
        sw.WriteLine($"{obj.InVal};{obj.OutVal}");
}

var median = Median(workerResultList.Select(x=>x.OutVal));
Console.WriteLine($"{median}");
var check = dataProvider.CheckMedian(median,mode).Result;
Console.WriteLine($"{check}");
Console.ReadKey();



List<WorkerResult> GetValuesFromServer(List<int> inputSequence, WorkMode mode)
{
    var part = inputSequence.Count / threadCnt;
    var extra = inputSequence.Count % threadCnt;

    if (extra > 0)
        part += 1;

    var workerList = new List<Task<List<WorkerResult>>>();    
    var num = 1;
    
    var key = string.Empty;
    foreach (var chunk in inputSequence.Chunk(part))
    {
        //тут по-хорошему надо прикрутить DI контейнер и резолвить объекты из него 
        IWorker worker = new Worker(num,GetKey);
        workerList.Add(worker.GetServerValues(chunk.ToList(),mode));
        num++;
    }
    var arr = workerList.ToArray();
    Task.WaitAll(arr);

    return arr.SelectMany(x => x.Result).ToList();
}



static double Median(IEnumerable<int> items)
{
    var i = (items.Count() + 1) / 2.0;
    var d = i - Math.Truncate(i);
    var values = items.ToList();
    values.Sort();    
    if (d > 0)
    {
        var index = (int)Math.Truncate(i)-1;
        var item1 = values[index];
        var item2 = values[index+1];
        return (item1 + item2) / 2.0;
    }
    else
    {
        return values[(int)i];
    }   
}

string GetKey(string name, string workersKey)
{
    lock (lockObj)
    {
        var tmpKey = workersKey == string.Empty || currentKey != workersKey  ? currentKey : GetAdvancedKeyFromServer(name);
        currentKey = tmpKey;
        return tmpKey;
    }
}

string GetAdvancedKeyFromServer(string Name)
{    
    var TryCount = 10;
    var tmpkey = string.Empty;
    var tryCnt = 0;
    lock (lockObj)
    {
        while (tryCnt < TryCount)
        {
            Console.WriteLine($"Worker {Name} - попытка {tryCnt} получения ключа");
            try
            {
                tmpkey = dataProvider.GetAdvancedKey().Result;
                Console.WriteLine($"Worker {Name} - Новый ключ: {tmpkey}");
                return tmpkey;
            }
            catch (RateLimitException rlEx)
            {
                Console.WriteLine($"Worker {Name} -Слишком много запросов ключа: {rlEx.Message}");
                //Task.Delay(KeyDelayTime * 1000 * (tryCnt + 1));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Worker {Name} - Ошибка запроса ключа: {ex.Message}");
                //Task.Delay(DelayTime * 1000 * (tryCnt + 1));
            }
            tryCnt++;
        }
    }
    return string.Empty;
}




