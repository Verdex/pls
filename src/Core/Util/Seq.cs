
namespace Pls.Core.Util;

public interface ISeqable<T> {
    T Self();
    IEnumerable<T> Next();
}

public static class Seq {
    public static IEnumerable<T> ToSeq<T>(this ISeqable<T> target) where T : ISeqable<T> {
        var s = new List<T> { target.Self() };
        while(s.Count > 0) {
            var t = s[^1];
            s.RemoveAt(s.Count - 1);
            var ns = t.Next();
            s.AddRange(ns);
            yield return t;
        }
    }
}