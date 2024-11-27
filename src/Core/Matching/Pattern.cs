
using System.Collections;
using System.Collections.Immutable;

namespace Pls.Core.Matching;

public interface IMatchable<TID, TContent> {
    void Deconstruct(out TID id, out IEnumerable<TContent> contents);
}

public abstract record Pattern<TID, TContent> where TContent : IMatchable<TID, TContent> {
    private Pattern() { }

    public record Wild : Pattern<TID, TContent>;

    public record Capture(string Name) : Pattern<TID, TContent>;
    public record TemplateVar(string Name) : Pattern<TID, TContent>;

    public record Exact(TID Id, ImmutableList<Pattern<TID, TContent>> Cs) : Pattern<TID, TContent>;
    public record Contents(ImmutableList<Pattern<TID, TContent>> Cs) : Pattern<TID, TContent>;
    public record Kind(TID Id) : Pattern<TID, TContent>;

    public record And(Pattern<TID, TContent> Left, Pattern<TID, TContent> Right) : Pattern<TID, TContent>;
    public record Or(Pattern<TID, TContent> Left, Pattern<TID, TContent> Right) : Pattern<TID, TContent>;

    public record PathNext : Pattern<TID, TContent>;
    public record Path(ImmutableList<Pattern<TID, TContent>> Ps) : Pattern<TID, TContent>;
    public record SubContentPath(ImmutableList<Pattern<TID, TContent>> Ps) : Pattern<TID, TContent>;

    public record Predicate(Func<TContent, bool> Pred) : Pattern<TID, TContent>;
    public record MatchWith(Func<IReadOnlyDictionary<string, TContent>, Pattern<TID, TContent>> Func) : Pattern<TID, TContent>;
}

public static class Pattern {
    public static IEnumerable<IEnumerable<(string Name, TContent Item)>> Find<TID, TContent>(this TContent data, Pattern<TID, TContent> pattern) where TContent : IMatchable<TID, TContent> =>
        new PatternEnumerable<TID, TContent>(data, pattern);

    public static IEnumerable<IDictionary<string, TContent>> FindDict<TID, TContent>(this TContent data, Pattern<TID, TContent> pattern) where TContent : IMatchable<TID, TContent> =>
        data.Find(pattern).Select(x => x.ToDictionary(k => k.Name, v => v.Item));

    public class PatternEnumerable<TID, TContent>(TContent data, Pattern<TID, TContent> pattern) : IEnumerable<IEnumerable<(string Name, TContent Item)>> where TContent : IMatchable<TID, TContent> {
        private PatternEnumerator<TID, TContent> Enumerator() => new PatternEnumerator<TID, TContent>(data, pattern);
        public IEnumerator<IEnumerable<(string Name, TContent Item)>> GetEnumerator() => Enumerator();
        IEnumerator IEnumerable.GetEnumerator() => Enumerator();
    }

    public class PatternEnumerator<TID, TContent> : IEnumerator<IEnumerable<(string Name, TContent Item)>> where TContent : IMatchable<TID, TContent> {

        private readonly TContent _data;
        private readonly Pattern<TID, TContent> _pattern;

        private Stack<(TContent, Pattern<TID, TContent>)> _work = new();
        private List<(string, TContent)> _captures = new();
        private List<TContent> _nexts = new();
        private Stack<(List<(string, TContent)> Captures, Stack<(TContent, Pattern<TID, TContent>)> Work, List<TContent> Nexts)> _alternatives = new();

        public PatternEnumerator(TContent data, Pattern<TID, TContent> pattern) {
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
                    case Pattern<TID, TContent>.Wild: break;

                    case Pattern<TID, TContent>.Capture c:
                        _captures.Add((c.Name, data));
                        break;

                    case Pattern<TID, TContent>.TemplateVar t: {
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

                    case Pattern<TID, TContent>.Exact e: {
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

                    case Pattern<TID, TContent>.Contents c: {
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

                    case Pattern<TID, TContent>.Kind k: {
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

                    case Pattern<TID, TContent>.And a:
                        _work.Push((data, a.Right));
                        _work.Push((data, a.Left));
                        break;

                    case Pattern<TID, TContent>.Or o: {
                        var w = Dup(_work);
                        w.Push((data, o.Right));
                        AddAlternative(w);
                        _work.Push((data, o.Left));
                        break;
                    }

                    case Pattern<TID, TContent>.PathNext:
                        _nexts.Add(data);
                        break;

                    case Pattern<TID, TContent>.Path(var ps) when ps.Count == 0: break;
                    case Pattern<TID, TContent>.Path(var ps): {
                        // Note:  Current work cloned off for alternatives
                        var altWork = Dup(_work);
                        var e = (PatternEnumerator<TID, TContent>)data.Find(ps[0]).GetEnumerator();

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
                            var nextPathPattern = new Pattern<TID, TContent>.Path(ps.Skip(1).ToImmutableList());

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
                                var nextPathPattern = new Pattern<TID, TContent>.Path(ps.Skip(1).ToImmutableList());

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

                    case Pattern<TID, TContent>.SubContentPath(var ps): {
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

                    case Pattern<TID, TContent>.Predicate(var p): {
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

                    case Pattern<TID, TContent>.MatchWith(var f): {
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

        private void AddAlternative(Stack<(TContent, Pattern<TID, TContent>)> work, List<(String, TContent)>? captures = null, List<TContent>? nexts = null) {
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
