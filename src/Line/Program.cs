
using Pls.Core.Matching;

public static class Program {


    private class Blarg(string name, List<Blarg> bs) : IMatchable<string, Blarg> {
        public List<Blarg> Bs { get; } = bs;

        public void Deconstruct(out string id, out IEnumerable<Blarg> contents) {
            id = name;
            contents = Bs;
        }

        public override string ToString() => $"{name}({string.Join(",", Bs)})";
    }

/*
    m(X, [X|_]).
    m(X, [_|R]) :- m(X, R).

    cat(lynx).
    cat(bob).
    cat(tiger).

    parents(bob, lisa, tim).
    parents(anna, lisa, alan).
    parents(lilly, sarah, alan).
    parents(bill, sarah, alan).

*/

    public static void Main() {
        var gen = new PatternGen<string, Pattern<string, Blarg>>();

        /*var db = new Blarg("db", []);
        List<string> cats = ["lynx", "bob", "tiger"];
        db.Bs.AddRange(cats.Select(x => new Blarg("cat", [new Blarg(x, [])])));

        


        foreach(var env in db.FindDict(gen.SubContentPath([gen.Capture("x")]))) {
            foreach(var kvp in env) {
                Console.WriteLine($"{kvp.Key} => {kvp.Value}");
            }
            Console.WriteLine("===");
        }*/
    }
}
