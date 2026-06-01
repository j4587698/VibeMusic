using System;
using System.Threading.Tasks;
using KuGou.Lite;

namespace Tester
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var client = new KugouLiteClient();
            var idsToTest = new[] { 0, 1, 2, 3, 4, 5, 6, 8, 10, 15, 20, 24, 25, 26, 30, 40 };

            foreach (var id in idsToTest)
            {
                Console.WriteLine($"\nTesting categoryId: {id}");
                try
                {
                    var page1 = await client.GetTopPlaylistsTypedAsync(id, 1, 30);
                    Console.WriteLine($"  Page 1 items count: {page1?.Items?.Count ?? 0}");

                    if (page1?.Items?.Count > 0)
                    {
                        var page2 = await client.GetTopPlaylistsTypedAsync(id, 2, 30);
                        Console.WriteLine($"  Page 2 items count: {page2?.Items?.Count ?? 0}");
                        
                        var page3 = await client.GetTopPlaylistsTypedAsync(id, 3, 30);
                        Console.WriteLine($"  Page 3 items count: {page3?.Items?.Count ?? 0}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error: {ex.Message}");
                }
            }
        }
    }
}
