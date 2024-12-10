
using System.Collections;
using System.Collections.Immutable;

namespace Pls.Core.Matching;

public interface IMatchable<TId, TContent> {
    void Deconstruct(out TId id, out IEnumerable<TContent> contents);
}

public abstract record Pattern<TId, TContent> : IMatchable<string, Pattern<TId, TContent>> where TContent : IMatchable<TId, TContent> {
    private Pattern() { }

    public void Deconstruct(out string id, out IEnumerable<Pattern<TId, TContent>> contents) {
        switch (this) {
            case Pattern<TId, TContent>.Atom x: 
                id = "atom";
                contents = [];
                break;
            case Pattern<TId, TContent>.Wild:
                id = "wild";
                contents = [];
                break;
            case Pattern<TId, TContent>.Capture x:
                id = "capture";
                contents = [];
                break;
            case Pattern<TId, TContent>.TemplateVar x:
                id = "template";
                contents = [];
                break;
            case Pattern<TId, TContent>.Exact x:
                id = "exact";
                contents = [];
                break;
            case Pattern<TId, TContent>.Contents x:
                id = "contents";
                contents = [];
                break;
            case Pattern<TId, TContent>.Kind x:
                id = "kind";
                contents = [];
                break;
            case Pattern<TId, TContent>.And x:
                id = "and";
                contents = [];
                break;
            case Pattern<TId, TContent>.Or x:
                id = "or";
                contents = [];
                break;
            case Pattern<TId, TContent>.PathNext:
                id = "next";
                contents = [];
                break;
            case Pattern<TId, TContent>.Path x:
                id = "path";
                contents = [];
                break;
            case Pattern<TId, TContent>.SubContentPath x:
                id = "subcontent";
                contents = [];
                break;
            case Pattern<TId, TContent>.Predicate x:
                id = "predicate";
                contents = [];
                break;
            case Pattern<TId, TContent>.MatchWith x:
                id = "matchwith";
                contents = [];
                break;
        }
    }

    public record Atom(TContent C) : Pattern<TId, TContent>;

    public record Wild : Pattern<TId, TContent>;

    public record Capture(string Name) : Pattern<TId, TContent>;
    public record TemplateVar(string Name) : Pattern<TId, TContent>;

    public record Exact(TId Id, ImmutableList<Pattern<TId, TContent>> Cs) : Pattern<TId, TContent>;
    public record Contents(ImmutableList<Pattern<TId, TContent>> Cs) : Pattern<TId, TContent>;
    public record Kind(TId Id) : Pattern<TId, TContent>;

    public record And(Pattern<TId, TContent> Left, Pattern<TId, TContent> Right) : Pattern<TId, TContent>;
    public record Or(Pattern<TId, TContent> Left, Pattern<TId, TContent> Right) : Pattern<TId, TContent>;

    public record PathNext : Pattern<TId, TContent>;
    public record Path(ImmutableList<Pattern<TId, TContent>> Ps) : Pattern<TId, TContent>;
    public record SubContentPath(ImmutableList<Pattern<TId, TContent>> Ps) : Pattern<TId, TContent>;

    public record Predicate(Func<TContent, bool> Pred) : Pattern<TId, TContent>;
    public record MatchWith(Func<IReadOnlyDictionary<string, TContent>, Pattern<TId, TContent>> Func) : Pattern<TId, TContent>;
}

public class PatternGen<TId, TContent> where TContent : IMatchable<TId, TContent> {
    public Pattern<TId, TContent> Wild() => new Pattern<TId, TContent>.Wild();
    public Pattern<TId, TContent> Exact(TId t, ImmutableList<Pattern<TId, TContent>> contents) => new Pattern<TId, TContent>.Exact(t, contents);
    public Pattern<TId, TContent> Contents(ImmutableList<Pattern<TId, TContent>> contents) => new Pattern<TId, TContent>.Contents(contents);
    public Pattern<TId, TContent> Kind(TId t) => new Pattern<TId, TContent>.Kind(t);
    public Pattern<TId, TContent> And(Pattern<TId, TContent> a, Pattern<TId, TContent> b) => new Pattern<TId, TContent>.And(a, b);
    public Pattern<TId, TContent> Or(Pattern<TId, TContent> a, Pattern<TId, TContent> b) => new Pattern<TId, TContent>.Or(a, b);
    public Pattern<TId, TContent> Capture(string s) => new Pattern<TId, TContent>.Capture(s);
    public Pattern<TId, TContent> TemplateVar(string s) => new Pattern<TId, TContent>.TemplateVar(s);
    public Pattern<TId, TContent> SubContentPath(ImmutableList<Pattern<TId, TContent>> contents) => new Pattern<TId, TContent>.SubContentPath(contents);
    public Pattern<TId, TContent> PathNext() => new Pattern<TId, TContent>.PathNext();
    public Pattern<TId, TContent> Path(ImmutableList<Pattern<TId, TContent>> contents) => new Pattern<TId, TContent>.Path(contents);
    public Pattern<TId, TContent> Predicate(Func<TContent, bool> predicate) => new Pattern<TId, TContent>.Predicate(predicate);
    public Pattern<TId, TContent> MatchWith(Func<IReadOnlyDictionary<string, TContent>, Pattern<TId, TContent>> func) => new Pattern<TId, TContent>.MatchWith(func);
}

public static class PatternExt {
    public static IEnumerable<IEnumerable<(string Name, TContent Item)>> Find<TId, TContent>(this TContent data, Pattern<TId, TContent> pattern) where TContent : IMatchable<TId, TContent> =>
        new PatternEnumerable<TId, TContent>(data, pattern);

    public static IEnumerable<IDictionary<string, TContent>> FindDict<TId, TContent>(this TContent data, Pattern<TId, TContent> pattern) where TContent : IMatchable<TId, TContent> =>
        data.Find(pattern).Select(x => x.ToDictionary(k => k.Name, v => v.Item));

    public class PatternEnumerable<TId, TContent>(TContent data, Pattern<TId, TContent> pattern) : IEnumerable<IEnumerable<(string Name, TContent Item)>> where TContent : IMatchable<TId, TContent> {
        private PatternEnumerator<TId, TContent> Enumerator() => new PatternEnumerator<TId, TContent>(data, pattern);
        public IEnumerator<IEnumerable<(string Name, TContent Item)>> GetEnumerator() => Enumerator();
        IEnumerator IEnumerable.GetEnumerator() => Enumerator();
    }

    public class PatternEnumerator<TId, TContent> : IEnumerator<IEnumerable<(string Name, TContent Item)>> where TContent : IMatchable<TId, TContent> {

        private readonly TContent _data;
        private readonly Pattern<TId, TContent> _pattern;

        private Stack<(TContent, Pattern<TId, TContent>)> _work = new();
        private List<(string, TContent)> _captures = new();
        private List<TContent> _nexts = new();
        private Stack<(List<(string, TContent)> Captures, Stack<(TContent, Pattern<TId, TContent>)> Work, List<TContent> Nexts)> _alternatives = new();

        public PatternEnumerator(TContent data, Pattern<TId, TContent> pattern) {
            _work.Push((data, pattern));
            _data = data;
            _pattern = pattern;
        }

        public IEnumerable<(string Name, TContent Item)> Current => _captures;

        public bool MoveNext() {
            if (_work.Count == 0 && _alternatives.Count == 0) {
                return false;
            }

            if (_work.Count == 0) {
                SwitchToAlternative();
            }

            while (_work.Count != 0) {
                var (data, pattern) = _work.Pop();
                switch (pattern) {
                    case Pattern<TId, TContent>.Wild: break;

                    case Pattern<TId, TContent>.Atom a: {
                        if (!a.Equals(data)) {
                            if (_alternatives.Count > 0) {
                                SwitchToAlternative();
                            }
                            else {
                                return false;
                            }
                        }
                        break;
                    }

                    case Pattern<TId, TContent>.Capture c:
                        _captures.Add((c.Name, data));
                        break;

                    case Pattern<TId, TContent>.TemplateVar t: {
                        var (_, item) = _captures.Find(c => c.Item1.Equals(t.Name));
                        if (item is null || !item.Equals(data)){
                            // Note:  If the item is null then we've been given an non-existent variable name.
                            // Note:  Switching to alternative on failure.
                            if (_alternatives.Count > 0) {
                                SwitchToAlternative();
                            }
                            else {
                                return false;
                            }
                        }
                        break;
                    }

                    case Pattern<TId, TContent>.Exact e: {
                        var (id, cs) = data;
                        var dataContents = cs.ToList();
                        if (object.Equals(id, e.Id) && e.Cs.Count == dataContents.Count) {
                            foreach( var w in dataContents.Zip(e.Cs).Reverse() ) {
                                _work.Push(w);
                            }
                        }
                        else if(_alternatives.Count > 0) {
                            SwitchToAlternative();
                        }
                        else { 
                            return false;
                        }
                        break;
                    }

                    case Pattern<TId, TContent>.Contents c: {
                        var (_, cs) = data;
                        var dataContents = cs.ToList();
                        if (dataContents.Count == c.Cs.Count) {
                            foreach( var w in cs.Zip(c.Cs).Reverse() ) {
                                _work.Push(w);
                            }
                        }
                        else if(_alternatives.Count > 0) {
                            SwitchToAlternative();
                        }
                        else { 
                            return false;
                        }
                        break;
                    }

                    case Pattern<TId, TContent>.Kind k: {
                        var (id, _) = data;
                        if (!object.Equals(id, k.Id)) {
                            if (_alternatives.Count > 0) {
                                SwitchToAlternative();
                            }
                            else {
                                return false;
                            }
                        }
                        break;
                    }

                    case Pattern<TId, TContent>.And a:
                        _work.Push((data, a.Right));
                        _work.Push((data, a.Left));
                        break;

                    case Pattern<TId, TContent>.Or o: {
                        var w = Dup(_work);
                        w.Push((data, o.Right));
                        AddAlternative(w);
                        _work.Push((data, o.Left));
                        break;
                    }

                    case Pattern<TId, TContent>.PathNext:
                        _nexts.Add(data);
                        break;

                    case Pattern<TId, TContent>.Path(var ps) when ps.Count == 0: break;
                    case Pattern<TId, TContent>.Path(var ps): {
                        // Note:  Current work cloned off for alternatives
                        var altWork = Dup(_work);
                        var e = (PatternEnumerator<TId, TContent>)data.Find(ps[0]).GetEnumerator();

                        // Note:  Inject existing _captures into e's _captures so that
                        // template variables work inside of the inner Find.
                        e._captures.AddRange(_captures);

                        // Note: A failure to move next means that the entire Path has failed
                        if (!e.MoveNext()) { 
                            if (_alternatives.Count > 0) {
                                SwitchToAlternative();
                                // Note:  SwitchToAlternative has to be the last thing done in a case!
                                break;
                            }
                            else {
                                return false;
                            }
                        }

                        // Note:  e.Current contains all of the existing _captures, so to
                        // avoid duplicate captures assign the Current instead of appending it
                        _captures = e.Current.ToList();

                        if (e._nexts.Count > 0) {
                            var nextPathData = e._nexts[0];
                            var nextPathPattern = new Pattern<TId, TContent>.Path(ps.Skip(1).ToImmutableList());

                            foreach( var next in e._nexts[1..] ) {
                                var w = Dup(_work);
                                w.Push((next, nextPathPattern));
                                AddAlternative(w, nexts: []);
                            }

                            _work.Push((nextPathData, nextPathPattern));
                        }

                        // Note:  For each alternative of e, stuff all of them into alternatives
                        while (e.MoveNext()) {
                            var captures = e.Current.ToList();

                            if (e._nexts.Count > 0) {
                                var nextPathPattern = new Pattern<TId, TContent>.Path(ps.Skip(1).ToImmutableList());

                                foreach( var next in e._nexts ) {
                                    var w = Dup(altWork);
                                    w.Push((next, nextPathPattern));
                                    AddAlternative(w, captures: captures, nexts: []);
                                }
                            }
                            else {
                                AddAlternative(Dup(altWork), captures: captures, nexts: []);
                            }

                        }
                        
                        break;
                    }

                    case Pattern<TId, TContent>.SubContentPath(var ps): {
                        var (_, cs) = data;
                        var dataContents = cs.ToList();
                        if (ps.Count <= dataContents.Count) {
                            foreach( var index in Enumerable.Range(1, (dataContents.Count - ps.Count)).Reverse() ) { 
                                var w = Dup(_work);

                                var targetData = dataContents[index..(index + ps.Count)]; 

                                foreach( var x in targetData.Zip(ps).Reverse() ) {
                                    w.Push(x);
                                }

                                AddAlternative(w);
                            }

                            {
                                var targetData = dataContents[0..ps.Count];
                                foreach( var x in targetData.Zip(ps).Reverse() ) {
                                    _work.Push(x);
                                }
                            }
                        }
                        else {
                            if (_alternatives.Count > 0) {
                                SwitchToAlternative();
                            }
                            else {
                                return false;
                            }
                        }

                        break;
                    }

                    case Pattern<TId, TContent>.Predicate(var p): {
                        if (!p(data)) {
                            if (_alternatives.Count > 0) {
                                SwitchToAlternative();
                            }
                            else {
                                return false;
                            }
                        }
                        break;
                    }

                    case Pattern<TId, TContent>.MatchWith(var f): {
                        var p = f(_captures.ToDictionary(c => c.Item1, c => c.Item2));
                        _work.Push((data, p));
                        break;
                    }

                    default:
                        if (_alternatives.Count > 0) {
                            SwitchToAlternative();
                        }
                        else {
                            return false;
                        }
                        break;
                }
            }

            return true;
        }

        object IEnumerator.Current => _captures;

        public void Reset() {
            _captures = new();
            _nexts = new();
            _alternatives = new();
            _work = new();
            _work.Push((_data, _pattern));
        }

        public void Dispose() { }

        private void AddAlternative(Stack<(TContent, Pattern<TId, TContent>)> work, List<(String, TContent)>? captures = null, List<TContent>? nexts = null) {
            var c = captures ?? Dup(_captures);
            var n = nexts ?? Dup(_nexts);
            _alternatives.Push((c, work, n));
        }

        private void SwitchToAlternative() {
            var alt = _alternatives.Pop();
            _work = alt.Work;
            _captures = alt.Captures;
            _nexts = alt.Nexts;
        }

        private static Stack<X> Dup<X>(Stack<X> s) => new (s);
        private static List<X> Dup<X>(List<X> l) => new (l);
    }
}
