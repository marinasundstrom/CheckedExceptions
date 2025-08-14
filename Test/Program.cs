IEnumerable<string> strings = ["1", "2", "3", "4"];
var numbers = strings.Select(int.Parse);
var query = numbers.Where((x) => x % 2 == 1);
var r = query.First();
