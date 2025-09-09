using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace TestTask;
[ApiController]
[Route("targetad")]
public class TargetedAdController() : Controller
{
    static Dictionary<string, IEnumerable<string>> _lookup = []; 

    [HttpPost()]
    public IActionResult GetTargetsByKeyword(string region)
    {
        if (region is null)
            return BadRequest();
        
        IEnumerable<string> resp_body;

        var got_resp = _lookup.TryGetValue(region, out resp_body!);

        if (!got_resp)
            return NotFound();

        return Ok(resp_body);
    }

    [HttpPut]
    public IActionResult RewriteTargets(IFormFile file)
    {
        string result;

        using (var reader = new StreamReader(file.OpenReadStream()))
        {
            result = reader.ReadToEnd();
        }
        
        Console.WriteLine(result);
        var new_lookup = FiledataToDict(result);
        
        if (new_lookup is null)
            return BadRequest("Couldn't update from this file");

        _lookup = new_lookup;

        return Ok("Target updated");
    }

    Dictionary<string, IEnumerable<string>>? FiledataToDict(string filedata)
    {
        filedata = Regex.Replace(filedata, @"[^\P{C}\n]+", String.Empty); // удаление всех control символов кроме новой строки


        var real_list = // Список пар локация \ площадка данные в входном файле
            filedata
            .Split("\n")
            .SelectMany(line =>
                line
                .Split(":")[1]
                .Split(",")
                .Select(loc => new Tuple<string, string>(loc.Replace(" ", String.Empty), line.Split(":")[0]))); // Удаление всех пробелов в локациях

        var locations_in_real_list =
            real_list
            .Select(pair => pair.Item1);

        var iter_list = // Список всех возможных локаций, включает те, что не указаны в файле отдельно, но являются более глобальными чем те что в файле
            real_list
            .SelectMany(pair =>
                Prefixes(pair.Item1)
                .Select(v => new Tuple<string, string>(v, String.Empty)))
            .Where(pair => !locations_in_real_list.Contains(pair.Item1))
            .Concat(real_list)
            .OrderBy(p => p.Item1.Split("/").Length);

        Dictionary<string, HashSet<string>> res = [];

        foreach (var pair in iter_list) // Т.к локации отсортированы по сужению области то для каждой из них достаточно добавить только их собственные площадки и площадки на один уровень выше
        {
            if (!res.ContainsKey(pair.Item1)) // Если локация не внесена в словарь, то вносим
                res[pair.Item1] = [];

            if (pair.Item2 != String.Empty)
                res[pair.Item1].Add(pair.Item2); // Добавляем к площадкам локации те, что напрямую для нее указаны в файле

            var prefix = pair.Item1[..pair.Item1.LastIndexOf("/")];

            if (res.ContainsKey(prefix)) // Если есть префикс, то добавляем все его площадки
                foreach (var target in res[prefix])
                    res[pair.Item1].Add(target);
        }
        
        return
                res.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.AsEnumerable());
    }

    static IEnumerable<string> Prefixes(string location)
    {
        IEnumerable<string> res = [location];

        string curr = location;

        var last_ind = curr.LastIndexOf("/"); 

        while (last_ind > 0)
        {
            curr = curr[..last_ind];
            last_ind = curr.LastIndexOf("/");
            res = res.Append(curr);
        }

        return res.Reverse();
    }
}